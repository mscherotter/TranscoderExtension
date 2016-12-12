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

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Width"));

                    UpdateAspect();

                    if (Aspect > 0)
                    {
                        Height = Convert.ToUInt32(Convert.ToSingle(value) / Aspect);
                    }
                }
            }
        }

        private void UpdateAspect()
        {
            if (Width * Height > 0 && Aspect == 0)
            {
                Aspect = System.Convert.ToSingle(Width) / System.Convert.ToSingle(Height);
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

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Height"));

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
        /// Property changed event handler
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
