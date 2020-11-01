using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RailworksDownloader
{

    public class QueryResult
    {
        public int code { get; set; }
        public string message { get; set; }
        public QueryContent content { get; set; }
    }

    public class QueryContent
    {
        public int id { get; set; }
        public string file_name { get; set; }
        public string display_name { get; set; }
        public int version { get; set; }
        public int owner { get; set; }
        public string created { get; set; }
        public string[] files { get; set; }
        public int[] dependencies { get; set; }
    }

    internal class WebWrapper
    {
        private Uri ApiUrl { get; set; }

        private static readonly HttpClient client = new HttpClient();
        public WebWrapper(Uri apiUrl)
        {
            ApiUrl = apiUrl;
        }

        public async Task<Package> SearchForFile(string fileToFind)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "file", fileToFind } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
                return new Package(JsonConvert.DeserializeObject<QueryResult>(await response.Content.ReadAsStringAsync()).content);

            return null;
        }
    }
}
