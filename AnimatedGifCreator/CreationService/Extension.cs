// <copyright file="Extension.cs" company="Michael S. Scherotter">
// Copyright (c) 2016 Michael S. Scherotter All Rights Reserved
// </copyright>
// <author>Michael S. Scherotter</author>
// <email>synergist@outlook.com</email>
// <date>2016-04-04</date>
// <summary>Transcoder extension</summary>

using Windows.Services.Store;

namespace Transcoder
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.ApplicationModel;
    using Windows.ApplicationModel.AppService;
    using Windows.ApplicationModel.Background;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.Data.Json;
    using Windows.Foundation;
    using Windows.Foundation.Collections;
    using Windows.Storage;

    /// <summary>
    /// Transcoder extension
    /// </summary>
    internal class Extension
    {
        #region Fields
        private BackgroundTaskDeferral _backgroundTaskDeferral;
        private bool _canceled;
        private IAsyncAction _action;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the Extension class.
        /// </summary>
        internal Extension()
        {
            var version = Package.Current.Id.Version;

            var versionString = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.{2}.{3}",
                version.Major,
                version.Minor,
                version.Build,
                version.Revision);

            DisplayName = Package.Current.DisplayName;
            PublisherName = Package.Current.PublisherDisplayName;
            Version = versionString;
            Price = "0";
        }
        #endregion

        #region Properties
        public bool IsCancellationRequested
        {
            get
            {
                return _canceled;
            }
        }
        /// <summary>
        /// Gets or sets the source formats
        /// </summary>
        public IEnumerable<string> SourceFormats { get; set; }

        /// <summary>
        /// Gets or sets the destination formats
        /// </summary>
        public IEnumerable<string> DestinationFormats { get; set; }

        /// <summary>
        /// Gets or sets the display name (default is Package.Current.DisplayName)
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the publisher name (default is Package.Current.PublisherDisplayName)
        /// </summary>
        public string PublisherName { get; set; }

        /// <summary>
        /// Gets or sets the price (default is $0)
        /// </summary>
        public string Price { get; set; }

        /// <summary>
        /// Gets or sets the version (default is 0.1)
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the logo file
        /// </summary>
        public StorageFile LogoFile { get; set; }

        /// <summary>
        /// Gets or sets the transcode function
        /// </summary>
        public Func<StorageFile, StorageFile, ValueSet, IAsyncAction> TranscodeAsync { get; set; }

        /// <summary>
        /// Gets the source type ("Audio" or "Video")
        /// </summary>
        public string SourceType { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Run the Transcoder extension. This should be called from IBackgroundTask.Run()
        /// </summary>
        /// <param name="taskInstance">the task instance</param>
        /// <param name="deferral">the deferral</param>
        public void Run(IBackgroundTaskInstance taskInstance, BackgroundTaskDeferral deferral)
        {
            _backgroundTaskDeferral = deferral;

            taskInstance.Canceled += OnTaskCanceled; // Associate a cancellation handler with the background task.

            // Retrieve the app service connection and set up a listener for incoming app service requests.
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;

            if (details == null)
            {
                return;
            }

            var appServiceconnection = details.AppServiceConnection;
            appServiceconnection.RequestReceived += OnRequestReceived;
        }

        /// <summary>
        /// Gets the formatted price of the app
        /// </summary>
        /// <returns>an async task with the formatted price of the app as a string</returns>
        internal static async Task<string> GetPriceAsync()
        {
            var storeContext = StoreContext.GetDefault();

            if (storeContext == null)
            {
                return string.Empty;
            }

            var product = await storeContext.GetStoreProductForCurrentAppAsync();

            return product?.Product?.Price == null ? string.Empty : product.Product.Price.FormattedPrice;
        }

        #endregion

        #region Implementation
        /// <summary>
        /// App Service request received
        /// </summary>
        /// <param name="sender">the app service connection</param>
        /// <param name="args">the app service request received event arguments</param>
        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            _canceled = false;

            var deferral = args.GetDeferral();

            try
            {
                var command = args.Request.Message["Command"] as string;

                var response = new ValueSet();

                switch (command)
                {
                    case "GetSourceFormats":
                        response["Formats"] = string.Join(",", SourceFormats);
                        break;

                    case "GetDestinationFormats":
                        response["Formats"] = string.Join(",", DestinationFormats);
                        break;

                    case "Transcode":
                        var input = args.Request.Message["FileTokens"] as string;
                        if (input != null)
                        {
                            var jsonArray = JsonArray.Parse(input);

                            var sourceTokens = from item in jsonArray
                                select item.GetObject();

                            await TranscodeJsonAsync(sourceTokens, args.Request.Message);

                            if (_canceled)
                            {
                                throw new TaskCanceledException();
                            }
                            response["Status"] = "OK";
                        }
                        break;

                    case "GetDescription":
                        GetDescription(response);
                        break;
                }

                await args.Request.SendResponseAsync(response);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                deferral.Complete();
            }
        }

        /// <summary>
        /// Gets the Transcoder extension description
        /// </summary>
        /// <param name="response"></param>
        private void GetDescription(ValueSet response)
        {
            response["DisplayName"] = DisplayName;
            response["PublisherName"] = PublisherName;
            response["Version"] = Version;
            response["Price"] = Price;
            response["LogoFileToken"] = SharedStorageAccessManager.AddFile(LogoFile);
            response["SourceType"] = SourceType;
        }

        /// <summary>
        /// Transcode the source files
        /// </summary>
        /// <param name="fileTokens">the source files as a JsonArray</param>
        /// <param name="message">the transcode parameters</param>
        /// <returns>a list of tokens</returns>
        private async Task TranscodeJsonAsync(IEnumerable<JsonObject> fileTokens, ValueSet message)
        {
            //System.Diagnostics.Debug.WriteLine("TranscodeJsonAsync");

            foreach (var item in fileTokens)
            {
                await PrepareTranscodeAsync(message, item);

                if (_canceled)
                {
                    throw new TaskCanceledException();
                }
            }
        }

        /// <summary>
        /// Prepare for transcoding
        /// </summary>
        /// <param name="message">the transcode parameters</param>
        /// <param name="fileTokens">a JsonValue containing the token</param>
        /// <returns>an async task with a JsonValue</returns>
        private async Task PrepareTranscodeAsync(ValueSet message, JsonObject fileTokens)
        {
            var sourceToken = fileTokens["SourceToken"].GetString();
            var destinationToken = fileTokens["DestinationToken"].GetString();

            var sourceFile = await SharedStorageAccessManager.RedeemTokenForFileAsync(sourceToken);

            if (_canceled)
            {
                throw new TaskCanceledException();
            }

            var destinationFile = await SharedStorageAccessManager.RedeemTokenForFileAsync(destinationToken);

            _action = TranscodeAsync(sourceFile, destinationFile, message);

            await _action;

            if (_canceled)
            {
                throw new TaskCanceledException();
            }

            _action = null;
        }

        /// <summary>
        /// Complete the deferral when the task is cancelled.
        /// </summary>
        /// <param name="sender">the background task instance</param>
        /// <param name="reason">the cancellation reason</param>
        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _canceled = true;

            if (_action != null && _action.Status == AsyncStatus.Started)
            {
                _action.Cancel();
            }

            if (_backgroundTaskDeferral != null)
            {
                // Complete the service deferral.
                _backgroundTaskDeferral.Complete();

                _backgroundTaskDeferral = null;
            }
        }
        #endregion
    }
}
