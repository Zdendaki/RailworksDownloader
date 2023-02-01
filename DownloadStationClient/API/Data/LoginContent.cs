using Newtonsoft.Json;

namespace DownloadStationClient.API.Data
{
    public class LoginContent
    {
        [JsonProperty("userid")]
        public int UserID { get; set; }

        [JsonProperty("realname")]
        public string RealName { get; set; } = null!;

        [JsonProperty("email")]
        public string Email { get; set; } = null!;

        [JsonProperty("privileges")]
        public int Privileges { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; } = null!;
    }
}
