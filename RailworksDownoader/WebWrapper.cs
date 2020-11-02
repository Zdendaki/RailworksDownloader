using Desharp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
            if (response.IsSuccessStatusCode)
                return new Package(JsonConvert.DeserializeObject<QueryResult>(await response.Content.ReadAsStringAsync()).content);

            return null;
        }

        public async Task<HashSet<string>> GetAllFiles()
        {
            Dictionary<string, string> content = new Dictionary<string, string> { { "listFiles", null } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(content);

            HttpResponseMessage response = await client.PostAsync(ApiUrl + "query", encodedContent);
            if (response.IsSuccessStatusCode)
                return new HashSet<string>(JsonConvert.DeserializeObject<GetAllFilesResult>(await response.Content.ReadAsStringAsync()).content.Select(x => Railworks.NormalizePath(x)));

            return null;

        }

        public static async Task ReportDLC(List<SteamManager.DLC> dlcList, Uri apiUrl)
        {
            //Dictionary<string, string> content = new Dictionary<string, string> { { "content", dlcList } };

            string z = JsonConvert.SerializeObject(dlcList);

            new SetClipboardHelper(DataFormats.Text, z).Go();

            StringContent encodedContent = new StringContent(z, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(apiUrl + "reportDLC", encodedContent);
            if (response.IsSuccessStatusCode)
            {
                string x = await response.Content.ReadAsStringAsync();
                Console.WriteLine(x);
            }
        }
    }

    class SetClipboardHelper : StaHelper
    {
        readonly string _format;
        readonly object _data;

        public SetClipboardHelper(string format, object data)
        {
            _format = format;
            _data = data;
        }

        protected override void Work()
        {
            var obj = new System.Windows.DataObject(
                _format,
                _data
            );

            System.Windows.Clipboard.SetDataObject(obj, true);
        }
    }

    abstract class StaHelper
    {
        readonly ManualResetEvent _complete = new ManualResetEvent(false);

        public void Go()
        {
            var thread = new Thread(new ThreadStart(DoWork))
            {
                IsBackground = true,
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        // Thread entry method
        private void DoWork()
        {
            try
            {
                _complete.Reset();
                Work();
            }
            catch (Exception ex)
            {
                if (DontRetryWorkOnFailed)
                    throw;
                else
                {
                    try
                    {
                        Thread.Sleep(1000);
                        Work();
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                _complete.Set();
            }
        }

        public bool DontRetryWorkOnFailed { get; set; }

        // Implemented in base class to do actual work.
        protected abstract void Work();
    }
}
