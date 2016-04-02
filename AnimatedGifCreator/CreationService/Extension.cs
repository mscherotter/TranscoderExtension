﻿namespace Transcoder
{
    using System;
    using System.Collections.Generic; 
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
    internal class Extension : IBackgroundTask
    {
        #region Fields
        /// <summary>
        /// the background task deferral
        /// </summary>
        private BackgroundTaskDeferral backgroundTaskDeferral;
        private bool _canceled;
        private IAsyncAction _action;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the Extension class.
        /// </summary>
        internal Extension()
        {
            DisplayName = Package.Current.DisplayName;
            PublisherName = Package.Current.PublisherDisplayName;
            Version = "0.1";
            Price = "$0";
        }
        #endregion

        #region Properties
        public bool IsCancellationRequested
        {
            get
            {
                return this._canceled;
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
        #endregion

        #region Methods
        /// <summary>
        /// Run the Transcoder extension. This should be called from IBackgroundTask.Run()
        /// </summary>
        /// <param name="taskInstance">the task instance</param>
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            this.backgroundTaskDeferral = taskInstance.GetDeferral(); // Get a deferral so that the service isn&#39;t terminated.
            taskInstance.Canceled += OnTaskCanceled; // Associate a cancellation handler with the background task.

            // Retrieve the app service connection and set up a listener for incoming app service requests.
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            var appServiceconnection = details.AppServiceConnection;
            appServiceconnection.RequestReceived += OnRequestReceived;
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
                        response["Formats"] = string.Join(",", this.SourceFormats);
                        break;

                    case "GetDestinationFormats":
                        response["Formats"] = string.Join(",", this.DestinationFormats);
                        break;

                    case "Transcode":
                        var jsonArray = JsonArray.Parse(args.Request.Message["FileTokens"] as string);

                        var sourceTokens = from item in jsonArray
                                           select item.GetString();

                        var fileTokens = await TranscodeJsonAsync(sourceTokens, args.Request.Message);

                        if (_canceled)
                        {
                            throw new TaskCanceledException();
                        }
                        response["FileTokens"] = fileTokens;
                        break;

                    case "GetDescription":
                        GetDescription(response);
                        break;

                    default:
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
            response["LogoFileToken"] = SharedStorageAccessManager.AddFile(this.LogoFile);
        }

        /// <summary>
        /// Transcode the source files
        /// </summary>
        /// <param name="sourceFiles">the source files as a JsonArray</param>
        /// <param name="message">the transcode parameters</param>
        /// <returns>a list of tokens</returns>
        private async Task<string> TranscodeJsonAsync(IEnumerable<string> sourceTokens, ValueSet message)
        {
            System.Diagnostics.Debug.WriteLine("TranscodeJsonAsync");
            var destinationFiles = new JsonArray();

            foreach (var item in sourceTokens)
            {
                var token = await PrepareTranscodeAsync(message, item);

                if (_canceled)
                {
                    throw new TaskCanceledException();
                }

                destinationFiles.Add(token);
            }

            return destinationFiles.Stringify();
        }

        /// <summary>
        /// Prepare for transcoding
        /// </summary>
        /// <param name="message">the transcode parameters</param>
        /// <param name="jsonValue">a JsonValue containing the token</param>
        /// <returns>an async task with a JsonValue</returns>
        private async Task<JsonValue> PrepareTranscodeAsync(ValueSet message, string token)
        {
            var sourceFile = await SharedStorageAccessManager.RedeemTokenForFileAsync(token);

            if (_canceled)
            {
                throw new TaskCanceledException();
            }

            var desiredFilename = string.Format(@"{0}.gif", System.IO.Path.GetFileNameWithoutExtension(sourceFile.Name));

            var destinationFile = await Windows.Storage.ApplicationData.Current.TemporaryFolder.CreateFileAsync(desiredFilename, CreationCollisionOption.GenerateUniqueName);

            this._action = TranscodeAsync(sourceFile, destinationFile, message);

            await this._action;

            if (_canceled)
            {
                throw new TaskCanceledException();
            }

            this._action = null;

            var destinationFileToken = SharedStorageAccessManager.AddFile(destinationFile);

            return JsonValue.CreateStringValue(destinationFileToken);
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

            if (this.backgroundTaskDeferral != null)
            {
                // Complete the service deferral.
                this.backgroundTaskDeferral.Complete();

                this.backgroundTaskDeferral = null;
            }
        }
        #endregion
    }
}
