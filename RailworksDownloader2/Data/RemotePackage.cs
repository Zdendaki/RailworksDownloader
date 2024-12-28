using System.ComponentModel.DataAnnotations;

#nullable disable
namespace RailworksDownloader2.Data
{
    class RemotePackage
    {
        [Key]
        public int ID { get; set; }

        [MaxLength(260)]
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

        public int Paid { get; set; }

        public int SteamAppID { get; set; }

        [MaxLength(1024)]
        public string SteamDev { get; set; }
    }
}
