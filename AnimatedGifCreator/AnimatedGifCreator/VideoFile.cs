namespace AnimatedGifCreator
{
    using Windows.Storage;
    using Windows.UI.Xaml.Media;

    /// <summary>
    /// a video file
    /// </summary>
    public class VideoFile
    {
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
    }
}
