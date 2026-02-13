using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telhai.DotNet.PlayerProject.Models
{
    public class SongMetadata
    {
        public string FilePath { get; set; } = "";
        public string? TrackName { get; set; }
        public string? ArtistName { get; set; }
        public string? AlbumName { get; set; }
        public string? CoverImageBase64 { get; set; }

        public string? EditedTitle { get; set; }
        public List<string> ImagePaths { get; set; } = new();
    }
}

