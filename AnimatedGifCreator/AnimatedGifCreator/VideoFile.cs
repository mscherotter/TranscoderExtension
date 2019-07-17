namespace AnimatedGifCreator
{
    using System;
    using System.ComponentModel;
    using Windows.Storage;
    using Windows.UI.Xaml.Media;

    /// <summary>
    /// a video file
    /// </summary>
    public class VideoFile : INotifyPropertyChanged
    {
        private uint _width;
        private uint _height;
        private StorageFile _destinationFile;

        /// <summary>
        /// Gets or sets the name of the video file
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail image of the video file
        /// </summary>
        public ImageSource Thumbnail { get; set; }

        /// <summary>
        /// Gets or sets the file
        /// </summary>
        public StorageFile File { get; set; }

        /// <summary>
        /// Gets or sets the width
        /// </summary>
        public uint Width
        {
            get
            {
                return _width;
            }
            set
            {
                if (_width != value)
                {
                    _width = value;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Width)));

                    UpdateAspect();

                    if (Aspect > 0)
                    {
                        Height = Convert.ToUInt32(Convert.ToSingle(value) / Aspect);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the frame rate
        /// </summary>
        public double FrameRate { get; set; }

        private void UpdateAspect()
        {
            if (Width * Height > 0 && Aspect == 0)
            {
                Aspect = Convert.ToSingle(Width) / Convert.ToSingle(Height);
            }
        }

        /// <summary>
        /// Gets or sets the height
        /// </summary>
        public uint Height
        {
            get
            {
                return _height;
            }

            set
            {
                if (_height != value)
                {
                    _height = value;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Height)));

                    UpdateAspect();

                    if (Aspect > 0)
                    {
                        Width = Convert.ToUInt32(Convert.ToSingle(value) * Aspect);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the aspect ratio
        /// </summary>
        public float Aspect { get; set; }

        /// <summary>
        /// Gets or sets the destination file
        /// </summary>
        public StorageFile DestinationFile
        {
            get
            {
                return _destinationFile;
            }

            internal set
            {
                if (_destinationFile != value)
                {
                    _destinationFile = value;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DestinationFile)));
                }
            }
        }

        /// <summary>
        /// Property changed event handler
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
