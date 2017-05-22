using System;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Storage;

namespace AnimatedGifCreator.Commands
{
    /// <summary>
    /// Share command
    /// </summary>
    public class ShareCommand : ICommand
    {
        /// <summary>
        /// the file to share
        /// </summary>
        private StorageFile _fileToShare;

        /// <summary>
        /// Initializes a new instance of the ShareCommand class
        /// </summary>
        public ShareCommand()
        {
            DataTransferManager.GetForCurrentView().DataRequested += ShareCommand_DataRequested;
        }

        /// <summary>
        /// Can execute changed event handler
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Can the command execute?
        /// </summary>
        /// <param name="parameter">a <see cref="StorageFile"/></param>
        /// <returns>true if the parameter is a <see cref="StorageFile"/></returns>
        public bool CanExecute(object parameter)
        {
            return parameter is StorageFile;
        }

        /// <summary>
        /// Show the share UI
        /// </summary>
        /// <param name="parameter">a <see cref="StorageFile"/></param>
        public void Execute(object parameter)
        {
            if (parameter is StorageFile file)
            {
                _fileToShare = file;

                DataTransferManager.ShowShareUI();
            }
        }

        private void ShareCommand_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var resources = ResourceLoader.GetForCurrentView();

            args.Request.Data.Properties.Title = _fileToShare.DisplayName;

            args.Request.Data.Properties.Description = resources.GetString("Animated GIF file");

            args.Request.Data.SetStorageItems(new IStorageItem[] { _fileToShare });
        }
    }
}
