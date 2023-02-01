using Newtonsoft.Json;
using System.Collections.Generic;

namespace DownloadStationClient.API.Data
{
    public class QueryContent
    {
        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("file_name")]
        public string FileName { get; set; } = null!;

        [JsonProperty("display_name")]
        public string DisplayName { get; set; } = null!;

        [JsonProperty("category")]
        public int Category { get; set; }

        [JsonProperty("era")]
        public int Era { get; set; }

        [JsonProperty("country")]
        public int Country { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("owner")]
        public int Owner { get; set; }

        [JsonProperty("created")]
        public string Created { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("target_path")]
        public string TargetPath { get; set; } = null!;

        [JsonProperty("paid")]
        public bool Paid { get; set; }

        [JsonProperty("steamappid")]
        public int? SteamAppID { get; set; }

        [JsonProperty("files")]
        public List<string> Files { get; set; } = new();

        [JsonProperty("dependencies")]
        public List<int> Dependencies { get; set; } = new();
    }
}
