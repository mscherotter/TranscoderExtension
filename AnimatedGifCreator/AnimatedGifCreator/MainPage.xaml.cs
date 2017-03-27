// <copyright file="MainPage.xaml.cs" company="Michael S. Scherotter">
// Copyright (c) 2016 Michael S. Scherotter All Rights Reserved
// </copyright>
// <author>Michael S. Scherotter</author>
// <email>synergist@outlook.com</email>
// <date>2016-04-04</date>
// <summary>MainPage code behind</summary>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
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
using CreationService;
using Microsoft.Services.Store.Engagement;

namespace AnimatedGifCreator
{
    /// <summary>
    ///     MainPage code behind
    /// </summary>
    public sealed partial class MainPage
    {
        /// <summary>
        ///     Default frame rate
        /// </summary>
        private const double DefaultFrameRate = 24.0;

        /// <summary>
        ///     the source files
        /// </summary>
        private readonly ObservableCollection<VideoFile> _sourceFiles = new ObservableCollection<VideoFile>();

        /// <summary>
        ///     the transcode action
        /// </summary>
        private IAsyncOperationWithProgress<bool, double> _operation;

        /// <summary>
        ///     Initializes a new instance of the MainPage class.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            FileList.ItemsSource = _sourceFiles;

            FeedbackButton.Visibility = StoreServicesFeedbackLauncher.IsSupported()
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        internal async void Activate(FileActivatedEventArgs args)
        {
            var files = args.Files.OfType<StorageFile>();

            await SetSourceFilesAsync(files);
        }

        private async void OnSelectFile(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            if (button != null)
            {
                button.IsEnabled = false;

                try
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
                finally
                {
                    button.IsEnabled = true;
                }
            }
        }

        private async Task SetSourceFilesAsync(IEnumerable<StorageFile> files)
        {
            _sourceFiles.Clear();

            foreach (var file in files)
            {
                var videoProperties = await file.Properties.GetVideoPropertiesAsync();

                if (videoProperties.Height == 0)
                    continue;

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
                catch (Exception se)
                {
                    Debug.WriteLine(se.Message);
                }

                _sourceFiles.Add(videoFile);
            }

            ConvertButton.IsEnabled = _sourceFiles.Any();
        }

        private async void OnConvert(object sender, RoutedEventArgs e)
        {
            ConvertButton.IsEnabled = false;

            try
            {
                if (_sourceFiles.Count == 1)
                    await TranscodeSingleFileAsync();
                else if (_sourceFiles.Count > 1)
                    await TranscodeMultipleFilesAsync();
            }
            catch (Exception se)
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
                ViewMode = PickerViewMode.List
            };

            picker.FileTypeFilter.Add(".gif");

            var folder = await picker.PickSingleFolderAsync();

            if (folder == null)
                return;

            Initialize();

            try

            {
                _operation = AsyncInfo.Run(async delegate(CancellationToken token, IProgress<double> progress)
                {
                    var progressPerFile = 100 / Convert.ToDouble(_sourceFiles.Count);

                    progress.Report(0.0);

                    var fileIndex = 0.0;

                    foreach (var item in _sourceFiles)
                    {
                        var sourceFile = item.File;

                        var desiredName = Path.ChangeExtension(item.Name, ".gif");

                        var destinationFile = await folder.CreateFileAsync(desiredName,
                            CreationCollisionOption.GenerateUniqueName);

                        if (token.IsCancellationRequested)
                            return false;

                        try
                        {
                            var gifCreator = new GifCreator();

                            var action = gifCreator.TranscodeGifAsync(sourceFile, destinationFile, item.Width,
                                item.Height, item.FrameRate);

                            var index = fileIndex;

                            action.Progress = delegate(IAsyncOperationWithProgress<bool, double> a, double v)
                            {
                                if (token.IsCancellationRequested)
                                    action.Cancel();

                                progress.Report(index * progressPerFile + progressPerFile * v / 100.0);
                            };

                            await action;

                            if (token.IsCancellationRequested)
                                return false;
                        }
                        catch (Exception se)
                        {
                            Debug.WriteLine(se.Message);
                        }

                        fileIndex++;
                    }

                    return true;
                });

                _operation.Progress = OnProgress;

                await _operation;

                await Launcher.LaunchFolderAsync(folder);

                var logger = StoreServicesCustomEventLogger.GetDefault();

                logger.Log("Transcode multiple files");
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
        ///     Clean up interface after transcode
        /// </summary>
        private void Cleanup()
        {
            _operation = null;
            ConvertButton.IsEnabled = true;
            ProgressRing.IsActive = false;
            CancelButton.Visibility = Visibility.Collapsed;
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressText.Visibility = Visibility.Collapsed;
            FileList.IsEnabled = true;
            SelectFileButton.IsEnabled = true;
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

            picker.FileTypeChoices.Add(resources.GetString("GIFImages"), new[] {".gif"}.ToList());

            var destinationFile = await picker.PickSaveFileAsync();

            if (destinationFile != null)
            {
                Initialize();

                ////var videoProperties = await sourceFile.Properties.GetVideoPropertiesAsync();

                try
                {
                    _operation = gifCreator.TranscodeGifAsync(sourceFile, destinationFile, firstVideo.Width,
                        firstVideo.Height, firstVideo.FrameRate);

                    _operation.Progress = OnProgress;

                    var succeeded = await _operation;

                    if (succeeded && _operation.Status == AsyncStatus.Completed)
                        await Launcher.LaunchFileAsync(destinationFile);

                    var logger = StoreServicesCustomEventLogger.GetDefault();

                    logger.Log("Transcode single file");
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
        ///     Initialize Progress UI
        /// </summary>
        private void Initialize()
        {
            ProgressBar.Value = 0.0;

            ProgressRing.IsActive = true;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressText.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            CancelButton.IsEnabled = true;
            ConvertButton.IsEnabled = false;
            FileList.IsEnabled = false;
            SelectFileButton.IsEnabled = false;
        }

        /// <summary>
        ///     Progress changed handler
        /// </summary>
        /// <param name="asyncInfo">the async information</param>
        /// <param name="progressInfo">the progress information (0-100)</param>
        private async void OnProgress(IAsyncOperationWithProgress<bool, double> asyncInfo, double progressInfo)
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
        ///     Cancel the transcode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            if (_operation != null && _operation.Status == AsyncStatus.Started)
            {
                CancelButton.IsEnabled = false;
                _operation.Cancel();
            }
        }

        private async void OnFeedback(object sender, RoutedEventArgs e)
        {
            if (StoreServicesFeedbackLauncher.IsSupported())
                await StoreServicesFeedbackLauncher.GetDefault().LaunchAsync();
        }
    }
}