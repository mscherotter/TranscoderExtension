// <copyright file="MainPage.xaml.cs" company="Michael S. Scherotter">
// Copyright (c) 2016 Michael S. Scherotter All Rights Reserved
// </copyright>
// <author>Michael S. Scherotter</author>
// <email>synergist@outlook.com</email>
// <date>2016-04-04</date>
// <summary>MainPage code behind</summary>

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using CreationService;

namespace AnimatedGifCreator
{
    /// <summary>
    ///     MainPage code behind
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private IAsyncActionWithProgress<double> _action;
        private StorageFile sourceFile;

        /// <summary>
        ///     Initializes a new instance of the MainPage class.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();
        }

        internal async void Activate(FileActivatedEventArgs args)
        {
            await SetSourceFileAsync(args.Files.FirstOrDefault() as StorageFile);
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

            var file = await picker.PickSingleFileAsync();

            await SetSourceFileAsync(file);
        }

        private async Task SetSourceFileAsync(StorageFile file)
        {
            sourceFile = file;

            if (file == null)
            {
                FilenameText.Text = string.Empty;
                ConvertButton.IsEnabled = false;
            }
            else
            {
                FilenameText.Text = file.Name;
                ConvertButton.IsEnabled = true;

                var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.VideosView);

                var image = new BitmapImage();

                await image.SetSourceAsync(thumbnail);

                SourceThumbnail.Source = image;
            }
        }

        private async void OnConvert(object sender, RoutedEventArgs e)
        {
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
                ProgressBar.Value = 0.0;

                ProgressRing.IsActive = true;
                ProgressBar.Visibility = Visibility.Visible;

                CancelButton.Visibility = Visibility.Visible;

                var videoProperties = await sourceFile.Properties.GetVideoPropertiesAsync();

                try
                {
                    _action = gifCreator.TranscodeGifAsync(sourceFile, destinationFile, videoProperties.Width,
                        videoProperties.Height);

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
                    _action = null;

                    ProgressRing.IsActive = false;
                    CancelButton.Visibility = Visibility.Collapsed;
                    ProgressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void OnProgress(IAsyncActionWithProgress<double> asyncInfo, double progressInfo)
        {
            await
                ProgressBar.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    delegate { ProgressBar.Value = progressInfo; });
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            if (_action != null && _action.Status == AsyncStatus.Started)
            {
                _action.Cancel();
            }
        }
    }
}