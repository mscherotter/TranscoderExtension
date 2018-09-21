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
    using Windows.Media.MediaProperties;
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
            var deferral = taskInstance.GetDeferral(); 

            var logoFile = await Package.Current.InstalledLocation.GetFileAsync("Assets\\Logo.png");

            var extension = new Transcoder.Extension
            {
                Price = await Transcoder.Extension.GetPriceAsync(), 
                SourceType = "Video",
                SourceFormats = new[] { ".mp4", ".mov", ".wmv", ".avi" },
                DestinationFormats = new[] { ".gif" },
                LogoFile = logoFile,
                TranscodeAsync = TranscodeAsync
            };

            extension.Run(taskInstance, deferral);
        }

        /// <summary>
        /// Transcode a GIF file
        /// </summary>
        /// <param name="source">the source video file</param>
        /// <param name="destination">the destination .gif file</param>
        /// <param name="width">the destination image width</param>
        /// <param name="height">the destination image height</param>
        /// <param name="fps">the frames per second to encode at</param>
        /// <returns>an async operation with boolean result and double (0-100) progress.</returns>
        public IAsyncOperationWithProgress<bool, double> TranscodeGifAsync(StorageFile source, StorageFile destination, uint width, uint height, double fps)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source", "source cannot be null.");
            }

            if (destination == null)
            {
                throw new ArgumentNullException("destination", "destination cannot be null.");
            }

            if (width == 0)
            {
                throw new ArgumentOutOfRangeException("width", "width cannot be 0.");
            }

            if (height == 0)
            {
                throw new ArgumentOutOfRangeException("height", "height cannot be 0.");
            }

            if (fps <= 0)
            {
                throw new ArgumentOutOfRangeException("fps", "fps must be greater than 0.");
            }

            return AsyncInfo.Run(async delegate (CancellationToken token, IProgress<double> progress)
            {
                var composition = new MediaComposition();

                System.Diagnostics.Debug.WriteLine(source.Path);

                var clip = await MediaClip.CreateFromFileAsync(source);

                if (token.IsCancellationRequested)
                {
                    return false;
                }

                progress.Report(10);

                composition.Clips.Add(clip);

                using (var outputStream = await destination.OpenAsync(FileAccessMode.ReadWrite))
                {
                    
                    if (token.IsCancellationRequested)
                    {
                        return false;
                    }

                    progress.Report(20);

                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.GifEncoderId, outputStream);

                    if (token.IsCancellationRequested)
                    {
                        return false;
                    }

                    progress.Report(30);

                    var increment = TimeSpan.FromSeconds(1.0 / fps);

                    var timesFromStart = new List<TimeSpan>();

                    for (var timeCode = TimeSpan.FromSeconds(0); timeCode < composition.Duration; timeCode += increment)
                    {
                        timesFromStart.Add(timeCode);
                    }

                    var thumbnails = await composition.GetThumbnailsAsync(
                        timesFromStart,
                        Convert.ToInt32(width),
                        Convert.ToInt32(height),
                        VideoFramePrecision.NearestFrame);

                    progress.Report(40);

                    if (token.IsCancellationRequested)
                    {
                        return false;
                    }

                    var index = 0;

                    var progressPerStep = (90.0 - 40.0) / thumbnails.Count / 3.0;

                    var currentProgress = 40.0;

                    foreach (var thumbnail in thumbnails)
                    {
                        var decoder = await BitmapDecoder.CreateAsync(thumbnail);

                        if (token.IsCancellationRequested)
                        {
                            return false;
                        }

                        progress.Report(currentProgress+=progressPerStep);

                        var pixels = await decoder.GetPixelDataAsync();

                        if (token.IsCancellationRequested)
                        {
                            return false;
                        }

                        progress.Report(currentProgress += progressPerStep);

                        encoder.SetPixelData(
                            BitmapPixelFormat.Rgba8,
                            BitmapAlphaMode.Ignore,
                            decoder.PixelWidth,
                            decoder.PixelHeight,
                            decoder.DpiX,
                            decoder.DpiY,
                            pixels.DetachPixelData());

                        var delayTime = Convert.ToUInt16(increment.TotalSeconds * 1000);

                        var properties = new BitmapPropertySet
                        {
                            {
                                "/grctlext/Delay",
                                new BitmapTypedValue(delayTime / 10, PropertyType.UInt16)
                            }
                        };

                        await encoder.BitmapProperties.SetPropertiesAsync(properties);



                        if (index < thumbnails.Count - 1)
                        {
                            await encoder.GoToNextFrameAsync();

                            if (token.IsCancellationRequested)
                            {
                                return false;
                            }

                            progress.Report(currentProgress += progressPerStep);
                        }
                        index++;
                    }

                    await encoder.FlushAsync();

                    if (token.IsCancellationRequested)
                    {
                        return false;
                    }

                    progress.Report(100);

                    return true;
                }
        });
        }

        /// <summary>
        /// Transcode a video file to an animated GIF image
        /// </summary>
        /// <param name="source">a video file</param>
        /// <param name="destination">an empty GIF image</param>
        /// <param name="arguments">transcode parameters</param>
        /// <returns>an async action</returns>
        public IAsyncAction TranscodeAsync(StorageFile source, StorageFile destination, ValueSet arguments)
        {
            return AsyncInfo.Run(async delegate (CancellationToken token)
            {
                object value;

                var videoProperties = await source.Properties.GetVideoPropertiesAsync();

                if (token.IsCancellationRequested)
                {
                    return;
                }

                var width = videoProperties.Width;
                var height = videoProperties.Height;
                    
                if (arguments != null && arguments.TryGetValue("Quality", out value))
                {
                    var qualityString = value.ToString();

                    VideoEncodingQuality quality;

                    if (Enum.TryParse(qualityString, out quality))
                    {
                        var profile = MediaEncodingProfile.CreateMp4(quality);

                        if (profile != null && profile.Video != null)
                        {
                            width = profile.Video.Width;
                            height = profile.Video.Height;
                        }
                    }
                }

                var operation = TranscodeGifAsync(source, destination, width, height, 10.0);

                if (token.IsCancellationRequested)
                {
                    operation.Cancel();
                }

                await operation;
            });
        }


        #endregion
    }
}
