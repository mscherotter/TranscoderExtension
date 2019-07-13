using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;

namespace CreationService
{
    /// <summary>
    /// Background task to turn a Jouranlist file into an animated GIF image
    /// </summary>
    public sealed class JournalistTask : IBackgroundTask
    {
        #region Fields
        private BackgroundTaskDeferral _deferral;
        #endregion

        #region Methods
        /// <summary>
        /// Run the background task
        /// </summary>
        /// <param name="taskInstance">the background task instance</param>
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            if (taskInstance == null) throw new ArgumentNullException(nameof(taskInstance));

            _deferral = taskInstance.GetDeferral();

            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;

            if (details != null)
            {
                details.AppServiceConnection.RequestReceived += AppServiceConnection_RequestReceived;
                details.AppServiceConnection.ServiceClosed += AppServiceConnection_ServiceClosed;
            }
            taskInstance.Canceled += TaskInstance_Canceled;
        }
        #endregion

        #region Implementation
        private void AppServiceConnection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            if (_deferral != null)
            {
                _deferral.Complete();
            }

            _deferral = null;
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (_deferral != null)
            {
                _deferral.Complete();
            }

            _deferral = null;
        }

        private async void AppServiceConnection_RequestReceived(
            AppServiceConnection sender,
            AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();

            try
            {
                var request = args.Request;

                var token = request.Message["FileToken"] as string;

                var data = request.Message["Data"] as string;

                if (data != null && token != null)
                {
                    var jsonData = JsonObject.Parse(data);

                    var framesPerSecond = jsonData["FramesPerSecond"].GetNumber();

                    var secondsPerFrame = 1.0 / framesPerSecond;

                    var delayTime = Convert.ToUInt16(secondsPerFrame * 1000);

                    var file = await SharedStorageAccessManager.RedeemTokenForFileAsync(token);

                    var temporaryFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
                    var gifFile = await temporaryFolder.CreateFileAsync(file.DisplayName + ".gif",
                        Windows.Storage.CreationCollisionOption.ReplaceExisting);

                    using (var stream = await file.OpenStreamForReadAsync())
                    {
                        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                        {
                            var journalXmlEntry = archive.GetEntry("journal.xml");

                            using (var journalStream = journalXmlEntry.Open())
                            {
                                await EncodeImagesAsync(delayTime, gifFile, archive, journalStream);
                            }
                        }
                    }

                    var gifToken = SharedStorageAccessManager.AddFile(gifFile);

                    var responseMessage = new ValueSet
                    {
                        ["FileToken"] = gifToken
                    };

                    await request.SendResponseAsync(responseMessage);
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private static async Task EncodeImagesAsync(ushort delayTime, Windows.Storage.StorageFile gifFile, ZipArchive archive, Stream journalStream)
        {
            var document = XDocument.Load(journalStream);

            var ns = document.Root.Name.Namespace;

            var pages = document.Document.Element(ns + "Journal").Element(ns + "Pages").Elements(ns + "JournalPage");

            var entries = (from page in pages
                           let entryName = page.Element(ns + "ThumbnailImage").Value
                           select archive.GetEntry(entryName)).ToList();

            using (var outputStream = await gifFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.GifEncoderId, outputStream);

                foreach (var entry in entries)
                {
                    await EncodeImageAsync(
                        delayTime,
                        encoder,
                        entry);
                }
            }
        }

        private static async Task EncodeImageAsync(ushort delayTime, BitmapEncoder encoder, ZipArchiveEntry entry)
        {
            using (var imageStream = entry.Open())
            {
                using (var pixelStream = new MemoryStream())
                {
                    await imageStream.CopyToAsync(pixelStream);

                    pixelStream.Seek(0, SeekOrigin.Begin);

                    var decoder = await BitmapDecoder.CreateAsync(pixelStream.AsRandomAccessStream());

                    var pixels = await decoder.GetPixelDataAsync();

                    encoder.SetPixelData(
                        decoder.BitmapPixelFormat,
                        BitmapAlphaMode.Ignore,
                        decoder.PixelWidth,
                        decoder.PixelHeight,
                        decoder.DpiX,
                        decoder.DpiY,
                        pixels.DetachPixelData());

                    var properties = new BitmapPropertySet
                                            {
                                                {
                                                    "/grctlext/Delay",
                                                    new BitmapTypedValue(delayTime / 10, PropertyType.UInt16)
                                                }
                                            };

                    await encoder.BitmapProperties.SetPropertiesAsync(properties);

                    await encoder.GoToNextFrameAsync();
                }
            }
        }
        #endregion
    }
}
