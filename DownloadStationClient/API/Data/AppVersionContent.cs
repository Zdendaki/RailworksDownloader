using Newtonsoft.Json;
using System;

namespace DownloadStationClient.API.Data
{
    public class AppVersionContent
    {
        [JsonProperty("version_name")]
        public string VersionName { get; set; } = null!;

        [JsonProperty("deployed")]
        public DateTime Deployed { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; } = null!;

        [JsonProperty("file_path")]
        public string FilePath { get; set; } = null!;

        [JsonProperty("report_errors")]
        public bool ReportErrors { get; set; }
    }
}
