using System;
using System.Windows.Input;
using Windows.Storage;
using Windows.System;

namespace AnimatedGifCreator.Commands
{
    /// <summary>
    /// Launch file command
    /// </summary>
    public class LaunchFileCommand : ICommand
    {
        /// <summary>
        /// Can Execute Changed event handler
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Is the parameter a <see cref="StorageFile"/>
        /// </summary>
        /// <param name="parameter">a <see cref="StorageFile"/></param>
        /// <returns>true if the parameter is a <see cref="StorageFile"/></returns>
        public bool CanExecute(object parameter)
        {
            return parameter is StorageFile;
        }

        /// <summary>
        /// Launch the file
        /// </summary>
        /// <param name="parameter">a <see cref="StorageFile"/></param>
        public async void Execute(object parameter)
        {
            if (parameter is StorageFile file)
            {
                await Launcher.LaunchFileAsync(file);
            }
        }
    }
}
