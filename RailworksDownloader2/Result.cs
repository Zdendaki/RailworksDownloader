using System.Text.Json.Serialization;

#nullable disable
namespace RailworksDownloader2
{
    public class Result<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("content")]
        public T Content { get; set; }

        public Result() { }

        public Result(int code, string message)
        {
            Code = code;
            Message = message;
        }

        public Result(int code, string message, T content)
        {
            Code = code;
            Message = message;
            Content = content;
        }
    }
}
