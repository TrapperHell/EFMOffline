using System.Collections.Generic;

namespace EFMOffline.Models
{
    public class MediaPage
    {
        public MediaPagePagination Pagination { get; set; }

        public List<Media> Media { get; set; }
    }
}
