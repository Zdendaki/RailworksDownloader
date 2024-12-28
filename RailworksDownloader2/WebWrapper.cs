using RailworksDownloader2.Data;
using System.Net.Http;

namespace RailworksDownloader2
{
    class WebWrapper
    {
        const string API = "https://dls.rw.jachyhm.cz/api/";

        HttpClient _client;

        public WebWrapper()
        {
            _client = new HttpClient()
            {
                BaseAddress = new Uri(API)
            };
        }

        public async Task<Package> GetPackage(int id)
        {
            FormUrlEncodedContent content = new([new("id", id.ToString())]);
            HttpResponseMessage response = await _client.PostAsync("query", content);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<Package>();
            }
            else
            {
                return null;
            }
        }

        public async Task<LoginContent> Login(string email, string password)
        {
            FormUrlEncodedContent content = new([new("email", email), new("password", password)]);
            HttpResponseMessage response = await _client.PostAsync("login", content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsAsync<LoginContent>();
            }
            else
            {
                return null;
            }
        }
    }
}
