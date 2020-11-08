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
        public int category { get; set; }
        public int era { get; set; }
        public int country { get; set; }
        public int version { get; set; }
        public int owner { get; set; }
        public string created { get; set; }
        public string description { get; set; }
        public string target_path { get; set; }
        public bool paid { get; set; }
        public int steamappid { get; set; }
        public string[] files { get; set; }
        public int[] dependencies { get; set; }
    }

    public class GetAllFilesResult
    {
        public int code { get; set; }
        public string message { get; set; }
        public string[] content { get; set; }
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
            if (response.IsSuccessStatusCode && response.StatusCode > 0)
                return new Package(JsonConvert.DeserializeObject<QueryResult>(await response.Content.ReadAsStringAsync()).content);

            return null;
        }

        public async Task<HashSet<string>> GetAllFiles()
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "listFiles", null } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
            {
                GetAllFilesResult jsonObject = JsonConvert.DeserializeObject<GetAllFilesResult>(await response.Content.ReadAsStringAsync());
                if (jsonObject.code > 0)
                {
                    HashSet<string> buffer = new HashSet<string>();

                    for (int i = 0; i < jsonObject.content.Length; i++)
                    {
                        buffer.Add(Railworks.NormalizePath(jsonObject.content[i]));
                    }

                    return buffer;
                }
            }

            return null;
        }

        public async Task<HashSet<string>> GetPaidFiles()
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "listPaid", null } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
            {
                GetAllFilesResult jsonObject = JsonConvert.DeserializeObject<GetAllFilesResult>(await response.Content.ReadAsStringAsync());
                if (jsonObject.code > 0)
                {
                    HashSet<string> buffer = new HashSet<string>();

                    for (int i = 0; i < jsonObject.content.Length; i++)
                    {
                        buffer.Add(Railworks.NormalizePath(jsonObject.content[i]));
                    }

                    return buffer;
                }
            }

            return null;
        }

        public static async Task ReportDLC(List<SteamManager.DLC> dlcList, Uri apiUrl)
        {
            StringContent encodedContent = new StringContent(JsonConvert.SerializeObject(dlcList), Encoding.UTF8, "application/json");

            await client.PostAsync(apiUrl + "reportDLC", encodedContent);
        }
    }
}
