using Newtonsoft.Json;

namespace DownloadStationClient.API.Data
{
    public class ObjectResult<T>
    {
        [JsonProperty("code")]
        public int Code { get; set; } = -1;

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("content")]
        public T? Content { get; set; }

        public ObjectResult() { }

        public ObjectResult(int code, string message)
        {
            Code = code;
            Message = message;
        }

        public ObjectResult(int code, string message, T content)
        {
            Code = code;
            Message = message;
            Content = content;
        }
    }
}
