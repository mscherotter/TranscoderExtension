// <copyright file="GifCreator.cs" company="Michael S. Scherotter">
// Copyright (c) 2016 Michael S. Scherotter All Rights Reserved
// </copyright>
// <author>Michael S. Scherotter</author>
// <email>synergist@outlook.com</email>
// <date>2016-04-04</date>
// <summary>GIF Creator Application service</summary>

namespace CreationService
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using Windows.ApplicationModel;
    using Windows.ApplicationModel.Background;
    using Windows.Foundation;
    using Windows.Foundation.Collections;
    using Windows.Graphics.Imaging;
    using Windows.Media.Editing;
    using Windows.Storage;

    /// <summary>
    /// Animated GIF Creator
    /// </summary>
    public sealed class GifCreator : IBackgroundTask
    {
        #region Methods
        /// <summary>
        /// Background task entry point
        /// </summary>
        /// <param name="taskInstance">the task instance</param>
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var logoFile = await Package.Current.InstalledLocation.GetFileAsync("Assets\\Logo.png");

            var extension = new Transcoder.Extension
            {
                Price = await Transcoder.Extension.GetPriceAsync(), 
                SourceFormats = new string[] { ".mp4", ".mov", ".wmv", ".avi" },
                DestinationFormats = new string[] { ".gif" },
                LogoFile = logoFile,
                TranscodeAsync = this.TranscodeGifAsync
            };

            extension.Run(taskInstance);
        }

        /// <summary>
        /// Transcode a video file to an animated GIF image
        /// </summary>
        /// <param name="source">a video file</param>
        /// <param name="destination">an empty GIF image</param>
        /// <param name="arguments">transcode parameters</param>
        /// <returns>an async action</returns>
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
        #endregion
    }
}
