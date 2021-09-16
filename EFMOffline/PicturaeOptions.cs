namespace EFMOffline
{
    public class PicturaeOptions
    {
        public string ApiKey { get; set; }

        public string PublicationSearchParam { get; set; }

        public int PageSize { get; set; } = 25;

        /// <summary>
        /// The max image zoom level is usually between 12 and 13, however an image is served even if the zoom level exceeds the max value
        /// </summary>
        public int ImageZoomLevel { get; set; }
    }
}
