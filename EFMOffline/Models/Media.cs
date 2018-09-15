using System;
using System.Collections.Generic;

namespace EFMOffline.Models
{
    public class Media
    {
        public Guid Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public List<MediaAsset> Asset { get; set; }

        public List<MediaMetadata> Metadata { get; set; }



        public override string ToString()
            => Title;
    }
}
