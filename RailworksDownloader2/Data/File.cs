using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

#nullable disable
namespace RailworksDownloader2.Data
{
    [Index(nameof(PackageID), nameof(FileName), IsUnique = true)]
    class File
    {
        [Key]
        public int ID { get; set; }

        public virtual Package Package { get; set; }

        public int PackageID { get; set; }

        [MaxLength(260)]
        public string FileName { get; set; }
    }
}
