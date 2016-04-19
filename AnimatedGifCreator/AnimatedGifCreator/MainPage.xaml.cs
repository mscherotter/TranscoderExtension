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
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace AnimatedGifCreator
{
    /// <summary>
    /// MainPage code behind
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private StorageFile sourceFile;
        private IAsyncAction _action;

        /// <summary>
        /// Initializes a new instance of the MainPage class.
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
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
            this.sourceFile = file;

            if (file == null)
            {
                FilenameText.Text = string.Empty;
                ConvertButton.IsEnabled = false;
            }
            else
            {
                FilenameText.Text = file.Name;
                ConvertButton.IsEnabled = true;

                var thumbnail = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.VideosView);

                var image = new BitmapImage();

                await image.SetSourceAsync(thumbnail);

                this.SourceThumbnail.Source = image;
            }
        }

        private async void OnConvert(object sender, RoutedEventArgs e)
        {
            var gifCreator = new CreationService.GifCreator();

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                DefaultFileExtension = ".gif",
                SuggestedFileName = Path.ChangeExtension(this.sourceFile.Name, ".gif"),
            };

            var resources = ResourceLoader.GetForCurrentView();

            picker.FileTypeChoices.Add(resources.GetString("GIFImages"), new string[] { ".gif" }.ToList());

            var destinationFile = await picker.PickSaveFileAsync();

            if (destinationFile != null)
            {
                this.ProgressRing.IsActive = true;

                this.CancelButton.Visibility = Visibility.Visible;

                try
                {
                    this._action = gifCreator.TranscodeGifAsync(this.sourceFile, destinationFile, null);

                    await _action;


                    await Launcher.LaunchFileAsync(destinationFile);
                }
                catch (System.Exception se)
                {
                }
                finally
                {
                    _action = null;

                    this.ProgressRing.IsActive = false;
                    this.CancelButton.Visibility = Visibility.Collapsed;
                }
            }
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
