using System.Text.Json.Serialization;

#nullable disable
namespace RailworksDownloader2.Data
{
    class LoginContent
    {
        [JsonPropertyName("userid")]
        public int UserID { get; set; }

        [JsonPropertyName("realname")]
        public string RealName { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("privileges")]
        public int Privileges { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}
