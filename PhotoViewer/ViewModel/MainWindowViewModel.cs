﻿using System.Collections.ObjectModel;
using Prism.Mvvm;
using PhotoViewer.Model;
using System.Windows.Input;
using Prism.Commands;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System;
using PhotoViewer.View;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PhotoViewer.ViewModel
{
    class MainWindowViewModel:BindableBase
    {
        #region Image Binding Parameter
        private BitmapSource _viewImageSource;
        /// <summary>
        /// 画像表示部に表示するイメージのBitmapSource
        /// </summary>
        public BitmapSource ViewImageSource
        {
            get { return _viewImageSource; }
            set { SetProperty(ref _viewImageSource, value); }
        }

        private string _viewMovieSource;
        /// <summary>
        /// 動画表示部に表示する動画ソースのURI
        /// </summary>
        public string ViewMovieSource
        {
            get { return _viewMovieSource; }
            set { SetProperty(ref _viewMovieSource, value); }
        }

        private MediaContentInfo _selectedMediaInfo;
        /// <summary>
        /// 選択されたメディア情報
        /// </summary>
        public MediaContentInfo SelectedMediaInfo
        {
            get { return _selectedMediaInfo; }
            set { SetProperty(ref _selectedMediaInfo, value); }
        }

        private MediaContentInfo.MediaType _mediaTypeOfSelected;
        /// <summary>
        /// 選択されたメディアのタイプ
        /// </summary>
        public MediaContentInfo.MediaType MediaTypeOfSelected
        {
            get { return _mediaTypeOfSelected; }
            set { SetProperty(ref _mediaTypeOfSelected, value); }
        }

        private PictureMediaContent _selectedPictureContent;
        /// <summary>
        /// 選択されたピクチャ情報
        /// </summary>
        public PictureMediaContent SelectedPictureContent
        {
            get { return _selectedPictureContent; }
            set { SetProperty(ref _selectedPictureContent, value); }
        }

        private MovieMediaContent _selectedMovieContent;
        /// <summary>
        /// 選択されたムービー情報
        /// </summary>
        public MovieMediaContent SelectedMovieContent
        {
            get { return _selectedMovieContent; }
            set { SetProperty(ref _selectedMovieContent, value); }
        }

        private string _selectedPath;
        /// <summary>
        /// 表示中のピクチャのフォルダパス
        /// </summary>
        public string SelectedPath
        {
            get { return _selectedPath; }
            set { SetProperty(ref _selectedPath, value); }
        }
        #endregion

        #region UI Binding Parameter
        private bool _saveButtonIsEnable;
        /// <summary>
        /// 保存ボタンの有効・無効
        /// </summary>
        public bool SaveButtonIsEnable
        {
            get { return _saveButtonIsEnable; }
            set { SetProperty(ref _saveButtonIsEnable, value); }
        }

        private bool _exifDeleteButtonIsEnable;
        /// <summary>
        /// Exif削除ボタンの有効・無効
        /// </summary>
        public bool ExifDeleteButtonIsEnable
        {
            get { return _exifDeleteButtonIsEnable; }
            set { SetProperty(ref _exifDeleteButtonIsEnable, value); }
        }
        #endregion

        // コマンドを定義
        public ICommand ReferenceButtonCommand { get; private set; }
        public ICommand ExifDeleteButtonCommand { get; private set; }
        public ICommand GearButtonCommand { get; private set; }
        public ICommand OpenFileExplorerCommand { get; private set; }

        // イベントを定義
        public event EventHandler ChangeSourceEvent;

        /// <summary>
        /// コマンドを設定する
        /// </summary>
        private void SetCommand()
        {
            ReferenceButtonCommand = new DelegateCommand(ReferenceButtonClicked);
            ExifDeleteButtonCommand = new DelegateCommand(ExifDeleteButtonClicked);
            GearButtonCommand = new DelegateCommand(GearButtonClicked);
            OpenFileExplorerCommand = new DelegateCommand(OpenFileExplorerButtonClicked);
        }

        /// <summary>
        /// メディア情報の読み込みスレッド
        /// </summary>
        /// <remarks>
        /// 写真、Exif情報を読み込む
        /// </remarks>
        private BackgroundWorker LoadPictureContentsBackgroundWorker;

        /// <summary>
        /// メディア情報の読み込みスレッドをリロードするかどうかのフラグ
        /// </summary>
        private bool LoadPictureContentsBackgroundWorker_Reload;

        // 情報を格納するリスト
        private ObservableCollection<MediaContentInfo> mediaInfoList = new ObservableCollection<MediaContentInfo>();
        public ObservableCollection<MediaContentInfo> MediaInfoList
        {
            get { return mediaInfoList; }
            set { mediaInfoList = value; }
        }

        private ObservableCollection<ExplorerTreeSourceViewModel> explorerTree = new ObservableCollection<ExplorerTreeSourceViewModel>();
        public ObservableCollection<ExplorerTreeSourceViewModel> ExplorerTree
        {
            get { return explorerTree; }
            set { explorerTree = value; }
        }

        private ObservableCollection<ContextMenuControl> contextMenuCollection = new ObservableCollection<ContextMenuControl>();
        public ObservableCollection<ContextMenuControl> ContextMenuCollection
        {
            get { return contextMenuCollection; }
            set { contextMenuCollection = value; }
        }

        private ObservableCollection<ExtraAppSetting> extraAppSettingCollection = new ObservableCollection<ExtraAppSetting>();
        public ObservableCollection<ExtraAppSetting> ExtraAppSettingCollection
        {
            get { return extraAppSettingCollection; }
            set { extraAppSettingCollection = value; }
        }

        /// <summary>
        /// 外部起動アプリのDictionary
        /// </summary>
        private Dictionary<string, string> ExtraAppPathDictionary;

        // 以前のディレクトリ保持
        private string PreviousFilePath;

        // メディアを読み込み中であるかどうかのフラグ
        public bool IsReadMedia;


        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindowViewModel()
        {
            // 変数の初期値を設定
            PreviousFilePath = "";
            SelectedPath = "";
            IsReadMedia = false;

            // コマンドを設定
            SetCommand();

            // 外部起動アプリのDictionaryを定義
            ExtraAppPathDictionary = new Dictionary<string, string>();

            // confファイルからExtraAppSettingを取得(ない場合はスルー)
            ExtraAppSetting.Import(ExtraAppSettingCollection);

            // デフォルトのコンテキストメニューの「メディア削除」を追加
            // ExtraAppSettingCollectionが取得できている場合は、それも追加
            UpdateContextMenuFromExtraAppSetting(ExtraAppSettingCollection);

            // 連携アプリ設定画面からのEvent設定
            LinkageProgramViewModel.LinkageEvent += UpdateLinkageContents;
            LinkageProgramViewModel.DeleteAppEvent += DeleteLinkageContents;
            LinkageProgramViewModel.AllDeleteEvent += AllDeleteLinkageContents;

            // エクスプローラのツリーをデフォルト状態で更新
            UpdateExplorerTreeSource();

            // 別スレッドでコンテンツの更新(デフォルトパスはPublicユーザーのピクチャーフォルダ)
            string _defaultPicturePath = Environment.GetFolderPath(System.Environment.SpecialFolder.CommonPictures);
            ChangeContentsList(_defaultPicturePath);
        }

        /// <summary>
        /// ContextMenuがクリックされたとき
        /// </summary>
        /// <param name="_item">コンテキストメニューで選択したメニュー情報</param>
        public void ExecuteContextMenu(MenuItem _item)
        {
            string _itemHeader = Convert.ToString(_item.Header);
            foreach (var _contextCollection in ContextMenuCollection)
            {
                // デフォルトのメディア削除だった場合
                if (_itemHeader == "メディア削除")
                {
                    DeleteMediaFromFolderClicked();
                    return;
                }

                try
                {
                    // それ以外の場合
                    if (_itemHeader == _contextCollection.DisplayName)
                    {
                        string _appName = _itemHeader.Replace("で開く", "");
                        string _appPath = ExtraAppPathDictionary[_appName];

                        // 外部連携アプリを起動する
                        System.Diagnostics.Process.Start(_appPath, SelectedMediaInfo.FilePath);

                        return;
                    }
                }
                catch (Exception _ex)
                {
                    // ログを吐き出す
                    App.LogException(_ex);
                    return;
                }
            }
        }

        /// <summary>
        /// コンテキストメニューに外部連携のアプリを設定する
        /// </summary>
        /// <remarks>デフォルトの"メディア削除"も追加する</remarks>
        /// <param name="_extraAppSettingCollection">外部連携のアプリ情報のリスト</param>
        private void UpdateContextMenuFromExtraAppSetting(ObservableCollection<ExtraAppSetting> _extraAppSettingCollection)
        {
            ExtraAppPathDictionary.Clear();
            ContextMenuCollection.Clear();

            // デフォルトのコンテキストメニューであるメディア削除を設定
            {
                var _contextMenuControl = new ContextMenuControl();

                // コンテキストメニューの表示名を設定
                const string _displayName = "メディア削除";
                _contextMenuControl.DisplayName = _displayName;

                // コンテキストメニューのメディア削除のアイコン画像を設定
                Uri _uri = new Uri("pack://application:,,,/Image/DeleteIcon.png");
                _contextMenuControl.ContextIcon = BitmapFrame.Create(_uri).Clone();

                // コンテキストメニューのコレクションに追加
                ContextMenuCollection.Add(_contextMenuControl);
            }

            // 外部連携アプリ設定が行われていて、すでに登録されている場合は1件以上あるはず
            if (_extraAppSettingCollection.Count > 0)
            {
                foreach (var _extraAppSetting in _extraAppSettingCollection)
                {
                    var _contextMenuControl = new ContextMenuControl();

                    // コンテキストメニューの表示名を設定("〇〇で開く"という表記で表示)
                    string _displayName = _extraAppSetting.Name + "で開く";
                    _contextMenuControl.DisplayName = _displayName;

                    // アイコン画像を読み込み
                    Icon _appIcon = Icon.ExtractAssociatedIcon(_extraAppSetting.Path);
                    using (var _iconStream = new WrappingStream(new MemoryStream()))
                    {
                        _appIcon.Save(_iconStream);
                        _contextMenuControl.ContextIcon = BitmapFrame.Create(_iconStream).Clone();
                    }

                    // 外部起動アプリのDictionaryにDisplayNameとそれに対応したPathを登録
                    ExtraAppPathDictionary.Add(_extraAppSetting.Name, _extraAppSetting.Path);

                    // コンテキストメニューのリストに追加
                    ContextMenuCollection.Add(_contextMenuControl);
                }
            }
        }

        /// <summary>
        /// 連携アプリの設定が行われた場合のイベント処理
        /// </summary>
        /// <param name="_e">連携したいアプリ情報を格納したイベント引数</param>
        private void UpdateLinkageContents(object _sender, LinkageEventArgs _e)
        {
            // 追加したい外部アプリ
            var _addExtraAppSettingCollection = _e._addExtraAppSettingCollection;   

            // 1つも登録されていないときは、全て追加
            if (ExtraAppSettingCollection.Count == 0)
            {
                // IDで並べ替えて、表示用のExtraAppSettingCollectionに代入
                var _orderedByIdCollection = new ObservableCollection<ExtraAppSetting>(_addExtraAppSettingCollection.OrderBy(n => n.Id));
                ExtraAppSettingCollection = _orderedByIdCollection;

                // ContextMenuの項目を更新
                UpdateContextMenuFromExtraAppSetting(ExtraAppSettingCollection);

                // Confファイルに書き出し
                ExtraAppSetting.Export(ExtraAppSettingCollection);
            }
            else
            {
                // 1つ以上登録されているとき
                foreach (var _extraAppSetting in _addExtraAppSettingCollection)
                {
                    // 既存の連携アプリに同じIDのものがあれば、そのアプリ情報を取得
                    ExtraAppSetting _containItem = ExtraAppSettingCollection.Where((i) => i.Id == _extraAppSetting.Id).FirstOrDefault();

                    if (_containItem != null)
                    {
                        // 同じIDが存在し、Pathも同じ場合は何もしない
                        if (_containItem.Path == _extraAppSetting.Path)
                        {
                            continue;
                        }

                        // 置き換え
                        ExtraAppSettingCollection[_containItem.Id - 1] = _extraAppSetting;
                    }
                    else
                    {
                        // 存在しない場合は既存のコレクションに追加してIDでソート
                        ExtraAppSettingCollection.Add(_extraAppSetting);
                        var _orderedById = new ObservableCollection<ExtraAppSetting>(ExtraAppSettingCollection.OrderBy(n => n.Id));
                        ExtraAppSettingCollection = _orderedById;
                    }
                }

                // ContextMenuの項目を更新
                UpdateContextMenuFromExtraAppSetting(ExtraAppSettingCollection);

                // Confファイルに外部アプリ連携の設定情報を書き出し
                ExtraAppSetting.Export(ExtraAppSettingCollection);
            }
        }

        /// <summary>
        /// 連携アプリの設定が削除されたときのイベント処理
        /// </summary>
        /// <param name="_e">連携を解除するアプリ情報を格納したイベント引数</param>
        private void DeleteLinkageContents(object _sender, DeleteEventArgs _e)
        {
            // 削除するIDを取得
            int _deleteId = _e.DeleteId;

            // 削除するIDに一致するアプリ情報を取得
            var _deleteAppSetting = extraAppSettingCollection.Where(appSetting => appSetting.Id == _deleteId).FirstOrDefault();
            Debug.Assert(_deleteAppSetting != null);

            try
            {
                // アプリ情報を削除
                ExtraAppSettingCollection.Remove(_deleteAppSetting);
            }
            catch (Exception _ex)
            {
                App.LogException(_ex);
                App.ShowErrorMessageBox("連携アプリ情報の削除に失敗しました。", "アプリ情報削除エラー");
                return;
            }

            // ContextMenuの項目を更新
            UpdateContextMenuFromExtraAppSetting(ExtraAppSettingCollection);

            // 設定ファイルに書き出し
            ExtraAppSetting.Export(ExtraAppSettingCollection);
        }

        /// <summary>
        /// 連携アプリの全削除が選択された場合のイベント処理
        /// </summary>
        private void AllDeleteLinkageContents(object _sender, EventArgs _e)
        {
            // すべての項目をリセット(外部連携のアプリ情報のリスト、外部連携のアプリパス情報の辞書、コンテキストメニュー)
            ExtraAppSettingCollection.Clear();
            ExtraAppPathDictionary.Clear();
            ContextMenuCollection.Clear();

            // Confファイルに書き出し
            ExtraAppSetting.Export(ExtraAppSettingCollection);
        }

        /// <summary>
        /// エクスプローラのツリーを更新する
        /// </summary>
        private void UpdateExplorerTreeSource()
        {
            ExplorerTree.Clear();

            // PCに接続されているドライブ情報をすべて取得
            DriveInfo[] _allDrives = DriveInfo.GetDrives();

            foreach(var _drive in _allDrives)
            {
                if (_drive.IsReady != true)
                {
                    // ドライブの準備ができていない場合は、飛ばして次へ
                    continue;
                }

                var _explorerTree = new ExplorerTreeSourceViewModel(_drive.Name, true);
                _explorerTree.ExplorerEvent += UpdatePictureContentsListFromExplorer;

                ExplorerTree.Add(_explorerTree);
            }
        }

        /// <summary>
        /// コンテンツリストを更新する
        /// </summary>
        /// <param name="_folder">選択されたフォルダパス</param>
        private void ChangeContentsList(string _folder)
        {
            if (_folder == SelectedPath)
            {
                // 選択されたフォルダパスが以前のフォルダパスと同じ場合は何もしない
                return;
            }

            // 以前のファイルパスを保持
            PreviousFilePath = SelectedPath;

            // ファイルパスの更新
            SelectedPath = _folder;

            // コンテンツのリスト更新処理
            UpdateContentsList();
        }

        /// <summary>
        /// 別スレッドでコンテンツの読み込みを行う
        /// </summary>
        private void UpdateContentsList()
        {
            if (LoadPictureContentsBackgroundWorker != null && LoadPictureContentsBackgroundWorker.IsBusy)
            {
                LoadPictureContentsBackgroundWorker_Reload = true;
                LoadPictureContentsBackgroundWorker.CancelAsync();
                return;
            }

            if (LoadPictureContentsBackgroundWorker_Reload)
            {
                return;
            }

            // 別スレッドでコンテンツの読み込み
            LoadContentsList();
        }

        /// <summary>
        /// コンテンツリストを非同期で取り込む
        /// </summary>
        private void LoadContentsList()
        {
            if (!Directory.Exists(SelectedPath))
            {
                // パスが見つからなければ以前のパスを指定
                SelectedPath = PreviousFilePath;

                // 以前のパスはリセット
                PreviousFilePath = "";
                return;
            }

            // メディア情報のリストをクリア
            MediaInfoList.Clear();

            // 読み込みスレッド(時間がかかるので、別スレッドで読み込む)
            var _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.WorkerSupportsCancellation = true;
            _backgroundWorker.DoWork += new DoWorkEventHandler(LoadContentsWorker_DoWork);
            _backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(LoadPictureContentsWorker_Completed);
            LoadPictureContentsBackgroundWorker = _backgroundWorker;

            // 読み込みスレッドの実行
            LoadPictureContentsBackgroundWorker.RunWorkerAsync();
        }

        /// <summary>
        /// メディアファイルを読み込むスレッドの処理
        /// </summary>
        private void LoadContentsWorker_DoWork(object _sender, DoWorkEventArgs _args)
        {
            try
            {
                LoadContentsWorker(_sender, _args);
            }
            catch (Exception _ex)
            {
                App.LogException(_ex);
                App.ShowErrorMessageBox("メディアの読み込みに失敗しました。", "読み込みエラー");
                return;
            }
        }

        /// <summary>
        /// 静止画・動画ファイルを読み込む
        /// </summary>
        private void LoadContentsWorker(object _sender, DoWorkEventArgs _args)
        {
            List<string> _filePathsList = new List<string>();

            int _tick = Environment.TickCount;

            // 選択されたフォルダ内に存在するサポートされる拡張子のファイルをすべて取得
            foreach (string _supportExt in MediaContentChecker.GetSupportExtensions())
            {
                // キャンセルチェック
                var _worker = _sender as BackgroundWorker;
                if (_worker.CancellationPending)
                {
                    _args.Cancel = true;
                    return;
                }

                var _filePaths = Directory.EnumerateFiles(SelectedPath, "*" + _supportExt);
                foreach (string _filePath in _filePaths)
                {
                    _filePathsList.Add(_filePath);
                }
            }

            var _readyFileList = new Queue<MediaContentInfo>();

            // 取得したファイルを順番に処理
            foreach(var _filePath in _filePathsList)
            {
                // キャンセルチェック
                var _worker = _sender as BackgroundWorker;
                if (_worker.CancellationPending)
                {
                    _args.Cancel = true;
                    return;
                }

                MediaContentInfo _mediaFileInfo = new MediaContentInfo();

                // ファイルパスを設定
                _mediaFileInfo.FilePath = _filePath;

                // ThumbnailImageの作成
                if (!_mediaFileInfo.CreateThumbnailImage())
                {
                    // Todo: エラー処理
                    return;
                }

                // 準備できたものからQueueに溜める
                int _count = 0;
                _readyFileList.Enqueue(_mediaFileInfo);
                _count++;

                int _duration = Environment.TickCount - _tick;
                if ((_count <= 100 && _duration > 500) || _duration > 1000)
                {
                    var _readyList = _readyFileList.ToArray();
                    _readyFileList.Clear();
                    App.Current.Dispatcher.Invoke((Action)(() => { MediaInfoList.AddRange(_readyList); }));
                    _tick = Environment.TickCount;
                }
            }

            if (_readyFileList.Count > 0)
            {
                App.Current.Dispatcher.Invoke((Action)(() => { foreach (var _readyFile in _readyFileList) MediaInfoList.Add(_readyFile); }));
            }
        }

        /// <summary>
        /// 読み込み完了後の処理
        /// </summary>
        private void LoadPictureContentsWorker_Completed(object _sender, RunWorkerCompletedEventArgs _args)
        {
            // 読み込み完了後に処理したいことを記述
            if (_args.Error != null)
            {
                return;
            }
            else if (_args.Cancelled == true)
            {
                var _worker = _sender as BackgroundWorker;
                _worker.Dispose();
            }
            else
            {
                var _worker = _sender as BackgroundWorker;
                _worker.Dispose();
            }

            if(LoadPictureContentsBackgroundWorker_Reload)
            {
                LoadPictureContentsBackgroundWorker_Reload = false;

                // 別スレッドでピクチャコンテンツの更新
                UpdateContentsList();
            }
        }

        /// <summary>
        /// 参照ボタンを押したときの動作
        /// </summary>
        private void ReferenceButtonClicked()
        {
            // SaveButtonとExifDeleteButtonの無効化
            const bool IsEnableFlag = false;
            SetIsEnableButton(IsEnableFlag);

            // フォルダ選択ダイアログの表示
            string _openFolderPath = ImageFileControl.GetFolderInDirectory();

            if (_openFolderPath == null)
            {
                // フォルダ選択ダイアログでキャンセルを押したので何もしない
                return;
            }

            // ピクチャコンテンツリストを変更
            ChangeContentsList(_openFolderPath);
        }

        /// <summary>
        /// Explorerから選択されたフォルダのメディアを取得するメソッド
        /// </summary>
        public void UpdatePictureContentsListFromExplorer(object _sender, ExplorerEventArgs _e)
        {
            // SaveButtonとExifDeleteButtonの無効化
            const bool IsEnableFlag = false;
            SetIsEnableButton(IsEnableFlag);

            string _folderPath = _e._directoryPath;
            ChangeContentsList(_folderPath);
        }

        /// <summary>
        /// 選択したメディアをロードする
        /// </summary>
        /// <param name="_info">選択したメディア情報</param>
        public void LoadContentSource(MediaContentInfo _info)
        {
            // 以前に選択されたMediaと同じ場合はロードしない
            if (SelectedMediaInfo == _info)
            {
                return;
            }

            // ファイルチェック
            if (!File.Exists(_info.FilePath))
            {
                App.ShowErrorMessageBox("ファイルは存在しません", "ファイルアクセスエラー");
                return;
            }

            // アクセス権チェック
            if (IsFileLocked(_info.FilePath))
            {
                // ロックされている場合
                App.ShowErrorMessageBox("ファイルにアクセスできません", "ファイルアクセスエラー");
                return;
            }

            // 素材の情報を設定する前に初期化
            // Viewにイベントを投げる(再生中のものがある場合は停止する)
            // Viewに設定されているものを一度削除。
            //
            ChangeSourceEvent(this, EventArgs.Empty);
            ViewImageSource = null;
            ViewMovieSource = null;

            // 選択されているMediaの情報とタイプを取得
            SelectedMediaInfo = _info;
            MediaTypeOfSelected = _info.ContentMediaType;

            switch (MediaTypeOfSelected)
            {
                case MediaContentInfo.MediaType.PICTURE:
                    // 静止画の場合
                    SelectedPictureContent = new PictureMediaContent(SelectedMediaInfo);
                    LoadViewImageSource(SelectedPictureContent);
                    break;
                case MediaContentInfo.MediaType.MOVIE:
                    // 動画の場合
                    SelectedMovieContent = new MovieMediaContent(SelectedMediaInfo);
                    LoadViewMovieSource(SelectedMovieContent);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Viewに拡大表示するImageSourceを読み込むメソッド
        /// </summary>
        /// <param name="_info">拡大表示するメディア情報</param>
        public async void LoadViewImageSource(PictureMediaContent _info)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // 画像を表示
                IsReadMedia = true;
                await Task.Run(() => SetPictureAndExifInfo(_info));
                IsReadMedia = false;

                // SaveButtonとExifDeleteButtonの有効化
                const bool IsEnableFlag = true;
                SetIsEnableButton(IsEnableFlag);
            }
            catch
            {
                App.ShowErrorMessageBox("ファイルアクセスでエラーが発生しました。", "ファイルアクセスエラー");
                return;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// 静止画とExif情報を設定する
        /// </summary>
        private void SetPictureAndExifInfo(PictureMediaContent _info)
        {
            BitmapSource _openImage = ImageFileControl.CreateViewImage(_info.FilePath);
            _openImage.Freeze();

            // 画像を表示
            App.Current.Dispatcher.Invoke(new Action(() =>
            {
                ViewImageSource = _openImage;

                // Exif情報を取得
                ExifParser.SetExifDataToMediaInfo(_info);
            }));

            _openImage = null;
            System.Threading.Thread.Sleep(50);
        }

        /// <summary>
        /// 動画素材の情報を設定する
        /// </summary>
        /// <param name="_info">拡大表示するメディア情報</param>
        public void LoadViewMovieSource(MovieMediaContent _info)
        {
            // Movieのソース(URI)を設定
            ViewMovieSource = _info.FilePath;
        }

        /// <summary>
        /// MediaInfoListの画像をダブルクリックしたときの動作
        /// </summary>
        /// <param name="_info">選択したメディア情報</param>
        public void MediaInfoListDoubleClicked(MediaContentInfo _info)
        {
            // TODO ファイルを別アプリで開くことを検討中
            string _selectFilePath = _info.FilePath;
        }

        /// <summary>
        /// フォルダを開くボタンをクリックしたときの動作
        /// </summary>
        private void OpenFileExplorerButtonClicked()
        {
            System.Diagnostics.Process.Start("EXPLORER.EXE", SelectedPath);
        }

        /// <summary>
        /// ListBoxItemの右クリックでメディア削除が押されたときの動作
        /// </summary>
        private void DeleteMediaFromFolderClicked()
        {          
            // メッセージボックスの確認でOKだった場合、フォルダからファイル削除
            if (App.ShowQuestionMessageBox("メディアファイルをフォルダから削除しますか？", "メディア削除の確認") == MessageBoxResult.OK)
            {
                try
                {
                    // ファイルチェック
                    if (!File.Exists(SelectedMediaInfo.FilePath))
                    {
                        App.ShowErrorMessageBox("ファイルは存在しません", "ファイルアクセスエラー");
                        return;
                    }

                    // アクセス権のチェック
                    if (!IsFileLocked(SelectedMediaInfo.FilePath))
                    {
                        // アクセス権があれば削除
                        File.Delete(SelectedMediaInfo.FilePath);

                        // 現在のディレクトリを再読込
                        UpdateContentsList();
                    }
                    else
                    {
                       App.ShowErrorMessageBox("ファイルはロックされています", "ファイルアクセスエラー");
                        return;
                    }
                }
                catch (Exception _ex)
                {
                    App.LogException(_ex);
                    App.ShowErrorMessageBox("ファイルの削除でエラーが発生しました。", "ファイル削除エラー");
                    return;
                }
            }
            else
            {
                // キャンセルだった場合、何もしない
                return;
            }
        }

        /// <summary>
        /// Exif削除ボタンが押されたときの動作
        /// </summary>
        private void ExifDeleteButtonClicked()
        {
            try
            {
                // ピクチャメディアであるか確認
                if (SelectedMediaInfo.ContentMediaType != MediaContentInfo.MediaType.PICTURE)
                {
                    // 何もしない
                    return;
                }
            }
            catch (Exception _ex)
            {
                App.LogException(_ex);
                App.ShowErrorMessageBox("ファイル読み込みに失敗しました。", "ファイルアクセスエラー");
                return;
            }

            string _filePath = SelectedMediaInfo.FilePath;

            // ファイルチェック
            if (!File.Exists(_filePath))
            {
                App.ShowErrorMessageBox("ファイルは存在しません", "ファイルアクセスエラー");
                return;
            }

            try
            {
                // アクセス権のチェック
                if (!IsFileLocked(_filePath))
                {
                    // Exif情報を削除して画像を保存
                    if (ImageFileControl.DeleteExifInfoAndSaveFile(_filePath))
                    {
                        // Exif情報を削除した画像を保存後、現在のディレクトリを再読込
                        UpdateContentsList();
                    }
                }
                else
                {
                    App.ShowErrorMessageBox("ファイルはロックされています", "ファイルアクセスエラー");
                    return;
                }
            }
            catch (Exception _ex)
            {
                App.LogException(_ex);
                App.ShowErrorMessageBox("ファイル保存時に発生しました。", "ファイル保存エラー");
                return;
            }
        }

        /// <summary>
        /// SaveButtonとExifDeleteButtonのフラグ設定メソッド
        /// </summary>
        /// <param name="_flag">ボタンのフラグ設定</param>
        private void SetIsEnableButton(bool _flag)
        {
            SaveButtonIsEnable = _flag;
            ExifDeleteButtonIsEnable = _flag;
        }

        /// <summary>
        /// 歯車ボタンが押されたときの動作
        /// </summary>
        private void GearButtonClicked()
        {
            PhotoAppInfoView _infoView = new PhotoAppInfoView();
            _infoView.Owner = Application.Current.MainWindow;
            _infoView.ShowDialog();
        }

        /// <summary>
        /// 指定されたファイルがロックされているかどうかを返す
        /// </summary>
        /// <param name="path">検証したいファイルへのフルパス</param>
        /// <returns>ロックされている場合はTrueを返す</returns>
        private bool IsFileLocked(string _filePath)
        {
            FileStream stream = null;

            try
            {
                stream = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

                // ファイルアクセスできる場合は、ロックされていない
                return false;
            }
            catch
            {
                // ファイルロックされている
                return true;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                    stream = null;
                }
            }
        }

        /// <summary>
        /// スレッドを停止するメソッド
        /// </summary>
        /// <return>スレッドが停止している場合はTrueを返す</return>
        public bool StopThreadAndTask()
        {
            if (LoadPictureContentsBackgroundWorker != null && LoadPictureContentsBackgroundWorker.IsBusy)
            {
                LoadPictureContentsBackgroundWorker.CancelAsync();
                return false;
            }

            return true;
        }
    }
}
