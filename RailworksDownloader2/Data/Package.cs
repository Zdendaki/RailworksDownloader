using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

#nullable disable
namespace RailworksDownloader2.Data
{
    class Package
    {
        [Key]
        [JsonPropertyName("id")]
        public int ID { get; set; }

        [MaxLength(260)]
        [JsonPropertyName("file_name")]
        public string FileName { get; set; }

        [MaxLength(1000)]
        public string DisplayName { get; set; }

        public PackageCategory Category { get; set; }

        public PackageEra Era { get; set; }

        public int Country { get; set; }

        public int Version { get; set; }

        public int Owner { get; set; }

        public DateTime DateTime { get; set; }

        [MaxLength(10000)]
        public string Description { get; set; }

        [MaxLength(260)]
        public string TargetPath { get; set; }

        public virtual ICollection<File> Files { get; set; }

        public virtual ICollection<File> InstalledFiles { get; set; }
    }
}
