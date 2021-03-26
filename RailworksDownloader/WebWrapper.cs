using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static RailworksDownloader.Utils;

namespace RailworksDownloader
{
    public class ObjectResult<T>
    {
        public int code { get; set; } = -1;
        public string message { get; set; } = string.Empty;
        public T content { get; set; }

        public ObjectResult() { }

        public ObjectResult(int code, string message)
        {
            this.code = code;
            this.message = message;
        }

        public ObjectResult(int code, string message, T content)
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

        public int? steamappid { get; set; }

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

    public class ArrayResult<T>
    {
        public int code { get; set; }

        public string message { get; set; }

        public T[] content { get; set; }
    }

    public class AppVersionContent
    {
        public string version_name { get; set; }
        public DateTime deployed { get; set; }
        public string comment { get; set; }
        public string file_path { get; set; }
    }

    public class ReportDLCcontent
    {
        public string token { get; set; }
        public List<SteamManager.DLC> dlcList { get; set; }
        public ReportDLCcontent(string token, List<SteamManager.DLC> dlcList)
        {
            this.token = token;
            this.dlcList = dlcList;
        }
    }

    public class WebWrapper
    {
        private Uri ApiUrl { get; set; }

        private static HttpClient Client { get; set; }

        internal delegate void OnDownloadProgressChangedEventHandler(float progress);
        internal event OnDownloadProgressChangedEventHandler OnDownloadProgressChanged;

        public WebWrapper(Uri apiUrl)
        {
            ApiUrl = apiUrl;

            Client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
        }

        public async Task<ObjectResult<object>> DownloadPackage(int packageId, string token)
        {
            Uri url = new Uri(ApiUrl + $"download?token={token}&package_id={packageId}");

            OnDownloadProgressChanged?.Invoke(0);

            WebClient webClient = new WebClient();
            webClient.DownloadProgressChanged += (sender, e) =>
            {
                OnDownloadProgressChanged?.Invoke(e.ProgressPercentage);
            };

            string tempFname = Path.GetTempFileName();
            await webClient.DownloadFileTaskAsync(url, tempFname);

            if (ZipTools.IsCompressedData(tempFname))
            {
                return new ObjectResult<object>(200, "Package succesfully downloaded!", tempFname);
            }
            else
            {
                ObjectResult<object> obj = JsonConvert.DeserializeObject<ObjectResult<object>>(File.ReadAllText(tempFname));
                if (obj != null)
                {
                    obj.code = 404;
                    obj.content = tempFname;
                }
                return obj;
            }
        }

        public async Task<Package> SearchForFile(string fileToFind)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "file", fileToFind } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
            {
                ObjectResult<QueryContent> responseContent = JsonConvert.DeserializeObject<ObjectResult<QueryContent>>(await response.Content.ReadAsStringAsync());

                if (Utils.IsSuccessStatusCode(responseContent.code))
                    return new Package(responseContent.content);
            }

            return null;
        }

        public async Task<Package> GetPackage(int packageId)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "id", packageId.ToString() } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
            {
                ObjectResult<QueryContent> responseContent = JsonConvert.DeserializeObject<ObjectResult<QueryContent>>(await response.Content.ReadAsStringAsync());

                if (Utils.IsSuccessStatusCode(responseContent.code))
                    return new Package(responseContent.content);
            }

            return null;
        }

        public static async Task<ObjectResult<LoginContent>> Login(string email, string password, Uri ApiUrl)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "email", email }, { "password", password } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "login", encodedContent);
            if (response.IsSuccessStatusCode)
                return JsonConvert.DeserializeObject<ObjectResult<LoginContent>>(await response.Content.ReadAsStringAsync());

            return null;
        }

        public async Task<HashSet<string>> QueryArray(string query)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { query, null } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
            {
                ArrayResult<string> jsonObject = JsonConvert.DeserializeObject<ArrayResult<string>>(await response.Content.ReadAsStringAsync());
                if (Utils.IsSuccessStatusCode(jsonObject.code))
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

        public async Task<IEnumerable<Package>> GetDownloadableFromMissing(IEnumerable<string> missing)
        {
            MultipartFormDataContent content = new MultipartFormDataContent{
                {new StringContent(string.Join("\n", missing)), "getAvailableFrom"}
            };

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "query", content);
            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
                ArrayResult<QueryContent> jsonObject = JsonConvert.DeserializeObject<ArrayResult<QueryContent>>(responseString);
                if (Utils.IsSuccessStatusCode(jsonObject.code))
                {
                    List<Package> pkgs = new List<Package>();
                    foreach (QueryContent qc in jsonObject.content)
                    {
                        pkgs.Add(new Package(qc));
                    }
                    return pkgs;
                }
            }

            return new Package[0];
        }

        public static async Task ReportDLC(List<SteamManager.DLC> dlcList, string token, Uri apiUrl)
        {
            StringContent encodedContent = new StringContent(JsonConvert.SerializeObject(new ReportDLCcontent(token, dlcList)), Encoding.UTF8, "application/json");
            await Client.PostAsync(apiUrl + "reportDLC", encodedContent);
        }

        public async Task<Dictionary<int, int>> GetVersions(List<int> packages)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "getVersions", string.Join(",", packages) } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await Client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
            {
                ObjectResult<Dictionary<string, int>> jsonObject = JsonConvert.DeserializeObject<ObjectResult<Dictionary<string, int>>>(await response.Content.ReadAsStringAsync());
                if (Utils.IsSuccessStatusCode(jsonObject.code))
                {
                    return jsonObject.content.ToDictionary(x => int.Parse(x.Key), x => x.Value); ;
                }
            }

            return new Dictionary<int, int>();
        }

        public static async Task<ObjectResult<AppVersionContent>> GetAppVersion(Uri apiUrl)
        {
            Dictionary<string, string> content = new Dictionary<string, string> { };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await new HttpClient(new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }).PostAsync(apiUrl + "getAppVersion", encodedContent);
            if (response.IsSuccessStatusCode)
                return JsonConvert.DeserializeObject<ObjectResult<AppVersionContent>>(await response.Content.ReadAsStringAsync());

            return null;
        }
    }
}
