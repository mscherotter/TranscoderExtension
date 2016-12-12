// <copyright file="MainPage.xaml.cs" company="Michael S. Scherotter">
// Copyright (c) 2016 Michael S. Scherotter All Rights Reserved
// </copyright>
// <author>Michael S. Scherotter</author>
// <email>synergist@outlook.com</email>
// <date>2016-04-04</date>
// <summary>MainPage code behind</summary>

namespace AnimatedGifCreator
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using System.Threading.Tasks;
    using CreationService;
    using Windows.ApplicationModel.Activation;
    using Windows.ApplicationModel.Resources;
    using Windows.Foundation;
    using Windows.Storage;
    using Windows.Storage.FileProperties;
    using Windows.Storage.Pickers;
    using Windows.System;
    using Windows.UI.Core;
    using Windows.UI.Popups;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;

    /// <summary>
    ///     MainPage code behind
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Default frame rate
        /// </summary>
        private const double DefaultFrameRate = 24.0;

        /// <summary>
        /// the transcode action
        /// </summary>
        private IAsyncActionWithProgress<double> _action;

        /// <summary>
        /// the source files
        /// </summary>
        private readonly ObservableCollection<VideoFile> _sourceFiles = new ObservableCollection<VideoFile>();

        /// <summary>
        ///     Initializes a new instance of the MainPage class.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            FileList.ItemsSource = _sourceFiles;
        }

        internal async void Activate(FileActivatedEventArgs args)
        {
            var files = args.Files.OfType<StorageFile>();

            await SetSourceFilesAsync(files);
        }

        private async void OnSelectFile(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };

            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".avi");

            var files = await picker.PickMultipleFilesAsync();

            await SetSourceFilesAsync(files);
        }

        private async Task SetSourceFilesAsync(IEnumerable<StorageFile> files)
        {
            _sourceFiles.Clear();

            foreach (var file in files)
            {
                var videoProperties = await file.Properties.GetVideoPropertiesAsync();

                if (videoProperties.Height == 0)
                {
                    continue;
                }

                var videoFile = new VideoFile
                {
                    Name = file.Name,
                    File = file,
                    Width = videoProperties.Width,
                    Height = videoProperties.Height,
                    FrameRate = DefaultFrameRate
                };

                try
                {
                    var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.VideosView);

                    var image = new BitmapImage();

                    await image.SetSourceAsync(thumbnail);

                    videoFile.Thumbnail = image;
                }
                catch (System.Exception se)
                {
                    System.Diagnostics.Debug.WriteLine(se.Message);
                }

                _sourceFiles.Add(videoFile);
            }

            ConvertButton.IsEnabled = _sourceFiles.Any();
        }

        private async void OnConvert(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_sourceFiles.Count == 1)
                {
                    await TranscodeSingleFileAsync();
                }
                else if (_sourceFiles.Count > 1)
                {
                    await TranscodeMultipleFilesAsync();
                }
            }
            catch (System.Exception se)
            {
                var resources = ResourceLoader.GetForCurrentView();

                var content = string.Format(
                    CultureInfo.CurrentCulture, 
                    resources.GetString("ErrorFormat"),
                    se.Message);

                var title = resources.GetString("ErrorTitle");

                var messageBox = new MessageDialog(content, title);

                await messageBox.ShowAsync();
            }
        }

        private async Task TranscodeMultipleFilesAsync()
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.List,
            };

            picker.FileTypeFilter.Add(".gif");

            var folder = await picker.PickSingleFolderAsync();

            if (folder == null)
            {
                return;
            }

            Initialize();

            try

            {
                _action = AsyncInfo.Run(async delegate (CancellationToken token, IProgress<double> progress)
                {
                    var progressPerFile = 100 / Convert.ToDouble(_sourceFiles.Count);

                    progress.Report(0.0);

                    var fileIndex = 0.0;

                    foreach (var item in _sourceFiles)
                    {
                        var sourceFile = item.File;

                        var desiredName = Path.ChangeExtension(item.Name, ".gif");

                        var destinationFile = await folder.CreateFileAsync(desiredName, CreationCollisionOption.GenerateUniqueName);

                        try
                        {
                            var gifCreator = new GifCreator();

                            var action = gifCreator.TranscodeGifAsync(sourceFile, destinationFile, item.Width,
                                item.Height, item.FrameRate);

                            action.Progress = delegate (IAsyncActionWithProgress<double> a, double v)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    action.Cancel();
                                }

                                progress.Report((fileIndex * progressPerFile) + progressPerFile * v / 100.0);
                            };

                            await action;
                        }
                        catch (System.Exception se)
                        {
                            System.Diagnostics.Debug.WriteLine(se.Message);
                        }

                        fileIndex++;
                    }
                });

                _action.Progress = OnProgress;

                await _action;

                await Launcher.LaunchFolderAsync(folder);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>
        /// Clean up interface after transcode
        /// </summary>
        private void Cleanup()
        {
            _action = null;
            ConvertButton.IsEnabled = true;
            ProgressRing.IsActive = false;
            CancelButton.Visibility = Visibility.Collapsed;
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressText.Visibility = Visibility.Collapsed;
        }

        private async Task TranscodeSingleFileAsync()
        {
            var firstVideo = _sourceFiles.First();

            var sourceFile = firstVideo.File;

            var gifCreator = new GifCreator();

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                DefaultFileExtension = ".gif",
                SuggestedFileName = Path.ChangeExtension(sourceFile.Name, ".gif")
            };

            var resources = ResourceLoader.GetForCurrentView();

            picker.FileTypeChoices.Add(resources.GetString("GIFImages"), new[] { ".gif" }.ToList());

            var destinationFile = await picker.PickSaveFileAsync();

            if (destinationFile != null)
            {
                Initialize();

                var videoProperties = await sourceFile.Properties.GetVideoPropertiesAsync();

                try
                {
                    _action = gifCreator.TranscodeGifAsync(sourceFile, destinationFile, firstVideo.Width,
                        firstVideo.Height, firstVideo.FrameRate);

                    _action.Progress = OnProgress;

                    await _action;

                    if (_action.Status == AsyncStatus.Completed)
                    {
                        await Launcher.LaunchFileAsync(destinationFile);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                finally
                {
                    Cleanup();
                }
            }
        }

        /// <summary>
        /// Initialize Progress UI
        /// </summary>
        private void Initialize()
        {
            ProgressBar.Value = 0.0;

            ProgressRing.IsActive = true;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressText.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            ConvertButton.IsEnabled = false;
        }

        /// <summary>
        /// Progress changed handler
        /// </summary>
        /// <param name="asyncInfo">the async information</param>
        /// <param name="progressInfo">the progress information (0-100)</param>
        private async void OnProgress(IAsyncActionWithProgress<double> asyncInfo, double progressInfo)
        {
            await
                ProgressBar.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    delegate 
                    {
                        ProgressBar.Value = progressInfo;
                        ProgressText.Text = (progressInfo / 100.0).ToString("p0", CultureInfo.CurrentCulture);
                    });
        }

        /// <summary>
        /// Cancel the transcode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            if (_action != null && _action.Status == AsyncStatus.Started)
            {
                _action.Cancel();
            }
        }
    }
}