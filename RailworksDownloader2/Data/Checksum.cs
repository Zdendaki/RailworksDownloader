using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

#nullable disable
namespace RailworksDownloader2.Data
{
    [Index(nameof(Folder), IsUnique = true)]
    class Checksum
    {
        public long ID { get; set; }

        [MaxLength(32)]
        public string Folder { get; set; }

        [MaxLength(32), Unicode(false)]
        public string Chcksum { get; set; }

        public DateTime LastWrite { get; set; }
    }
}
