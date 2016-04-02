using System;
using System.IO;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System; 
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AnimatedGifCreator
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private StorageFile sourceFile;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void OnSelectFile(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };

            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".avi");

            var file = await picker.PickSingleFileAsync();

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

            picker.FileTypeChoices.Add("GIF Images", new string[] { ".gif" }.ToList());

            var destinationFile = await picker.PickSaveFileAsync();

            if (destinationFile != null)
            {
                await gifCreator.TranscodeGifAsync(this.sourceFile, destinationFile, null);
            }

            await Launcher.LaunchFileAsync(destinationFile);
        }
    }
}
