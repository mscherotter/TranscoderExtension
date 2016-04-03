namespace CreationService
{
    using System.IO;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using Windows.ApplicationModel.Background;
    using Windows.Foundation;
    using Windows.Foundation.Collections;
    using Windows.Graphics.Imaging;
    using Windows.Media.Editing;
    using Windows.Storage;
    using Windows.Storage.Streams;
    using Windows.ApplicationModel;
    using System.Threading.Tasks;
    using Windows.ApplicationModel.Store;
    /// <summary>
    /// Animated GIF Creator
    /// </summary>
    public sealed class GifCreator : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var logoFile = await Package.Current.InstalledLocation.GetFileAsync("Assets\\StoreLogo.png");

            var extension = new Transcoder.Extension
            {
                Price = "0", // use = await GetPriceAsync() for apps that are not free
                Version = "1.0",
                SourceFormats = new string[] { ".mp4", ".wmv", ".avi" },
                DestinationFormats = new string[] { ".gif" },
                LogoFile = logoFile,
                TranscodeAsync = this.TranscodeGifAsync
            };

            extension.Run(taskInstance);
        }

        public IAsyncAction TranscodeGifAsync(StorageFile source, StorageFile destination, ValueSet arguments)
        {
            return AsyncInfo.Run(async delegate (CancellationToken token)
            {
                try
                {
                    var composition = new MediaComposition();

                    System.Diagnostics.Debug.WriteLine(source.Path);

                    var clip = await MediaClip.CreateFromFileAsync(source);

                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    composition.Clips.Add(clip);

                    using (var outputStream = await destination.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.GifEncoderId, outputStream);

                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        var increment = TimeSpan.FromSeconds(1.0 / 30.0);

                        var timesFromStart = new List<TimeSpan>();

                        for (var timeCode = TimeSpan.FromSeconds(0); timeCode < composition.Duration; timeCode += increment)
                        {
                            timesFromStart.Add(timeCode);
                        }

                        var thumbnails = await composition.GetThumbnailsAsync(timesFromStart, 640, 480, VideoFramePrecision.NearestFrame);

                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        var index = 0;

                        foreach (var thumbnail in thumbnails)
                        {
                            var decoder = await BitmapDecoder.CreateAsync(thumbnail);

                            if (token.IsCancellationRequested)
                            {
                                return;
                            }

                            var pixels = await decoder.GetPixelDataAsync();
                            
                            if (token.IsCancellationRequested)
                            {
                                return;
                            }

                            encoder.SetPixelData(
                                decoder.BitmapPixelFormat, 
                                BitmapAlphaMode.Ignore, 
                                decoder.PixelWidth, 
                                decoder.PixelHeight, 
                                decoder.DpiX, 
                                decoder.DpiY, 
                                pixels.DetachPixelData());

                            if (index < thumbnails.Count - 1)
                            {
                                await encoder.GoToNextFrameAsync();

                                if (token.IsCancellationRequested)
                                {
                                    return;
                                }
                            }
                            index++;
                        }

                        await encoder.FlushAsync();

                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
                catch (System.Exception se)
                {
                    System.Diagnostics.Debug.WriteLine(se.Message);
                }
            });
        }

        private async Task<string> GetPriceAsync()
        {
#if DEBUG
            var listingInformation = await CurrentAppSimulator.LoadListingInformationAsync();
#else
            var listingInformation = await CurrentApp.LoadListingInformationAsync();
#endif
            return listingInformation.FormattedPrice;
        }
    }
}
