using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static RailworksDownloader.Utils;

namespace RailworksDownloader
{
    public class ObjectResult
    {
        public int code { get; set; } = -1;
        public string message { get; set; } = string.Empty;
        public object content { get; set; }

        public ObjectResult() {}

        public ObjectResult(int code, string message)
        {
            this.code = code;
            this.message = message;
        }

        public ObjectResult(int code, string message, object content)
        {
            this.code = code;
            this.message = message;
            this.content = content;
        }
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

    public class LoginContent
    {
        public int userid { get; set; }
        public string realname { get; set; }
        public string email { get; set; }
        public int privileges { get; set; }
        public string token { get; set; }
    }

    public class ArrayResult
    {
        public int code { get; set; }
        public string message { get; set; }
        public string[] content { get; set; }
    }

    internal class WebWrapper
    {
        private Uri ApiUrl { get; set; }

        private static HttpClient Client { get; set; }

        public WebWrapper(Uri apiUrl)
        {
            ApiUrl = apiUrl;

            Client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });
        }

        public async Task<ObjectResult> DownloadPackage(int packageId, string token)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "token", token }, { "package_id", packageId.ToString() } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode && response.StatusCode > 0)
            {
                if (response.Headers.GetValues("Content-Type").FirstOrDefault() == "application/json")
                {
                    return JsonConvert.DeserializeObject<ObjectResult>(await response.Content.ReadAsStringAsync());
                }
                else
                {
                    string tempFname = Path.GetTempFileName();
                    ushort BUFF_SIZE = 16 * 1024;

                    using (FileStream oStream = File.OpenWrite(tempFname))
                    {
                        using (Stream iStream = await response.Content.ReadAsStreamAsync())
                        {
                            byte[] buffer = new byte[BUFF_SIZE];
                            int bytesRead;

                            while ((bytesRead = iStream.Read(buffer, 0, BUFF_SIZE)) > 0)
                            {
                                oStream.Write(buffer, 0, bytesRead);
                            }
                        }
                    }

                    return new ObjectResult(1, tempFname);
                }
            }

            return new ObjectResult();
        }

        public async Task<Package> SearchForFile(string fileToFind)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "file", fileToFind } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
                return new Package((QueryContent)JsonConvert.DeserializeObject<ObjectResult>(await response.Content.ReadAsStringAsync()).content);

            return null;
        }

        public static async Task<ObjectResult> Login(string email, string password, Uri ApiUrl)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "email", email }, { "password", password} };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "login", encodedContent);
            if (response.IsSuccessStatusCode)
                return JsonConvert.DeserializeObject<ObjectResult>(await response.Content.ReadAsStringAsync());

            return null;
        }

        public async Task<HashSet<string>> QueryArray(string query)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { query, null } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
            {
                ArrayResult jsonObject = JsonConvert.DeserializeObject<ArrayResult>(await response.Content.ReadAsStringAsync());
                if (jsonObject.code > 0)
                {
                    HashSet<string> buffer = new HashSet<string>();

                    for (int i = 0; i < jsonObject.content.Length; i++)
                    {
                        buffer.Add(NormalizePath(jsonObject.content[i]));
                    }

                    return buffer;
                }
            }

            return null;
        }

        public static async Task ReportDLC(List<SteamManager.DLC> dlcList, Uri apiUrl)
        {
            StringContent encodedContent = new StringContent(JsonConvert.SerializeObject(dlcList), Encoding.UTF8, "application/json");

            await Client.PostAsync(apiUrl + "reportDLC", encodedContent);
        }
    }
}
