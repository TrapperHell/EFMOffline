namespace EFMOffline
{
    public class Constants
    {
        public const string PicturaeApiKey = "9b2c2e2e-0d8d-4858-8194-457df5251e1e";

        public const string PublicationSearchParam = "Ja";

        public const int PageSize = 25;

        /// <summary>
        /// The max image zoom level is usually between 12 and 13, however an image is served even if the zoom level exceeds the max value
        /// </summary>
        public const int ImageZoomLevel = 20;
    }
}
