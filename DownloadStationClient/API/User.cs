using DownloadStationClient.API.Data;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DownloadStationClient.API
{
    internal class User
    {
        public bool LoggedIn { get; set; }

        public string? Email { get; set; }

        public string? Name { get; set; }

        public string? Token { get; set; }

        public User()
        {
            LoggedIn = false;
        }

        public async Task<LoginResponse> Login(string email, string password)
        {
            using (HttpClient client = new HttpClient())
            {
                Dictionary<string, string> content = new() { { "email", email }, { "password", password } };
                FormUrlEncodedContent encodedContent = new(content);
                HttpResponseMessage response = await client.PostAsync(App.API_URL + "login", encodedContent);

                try
                {
                    string plainJson = await response.Content.ReadAsStringAsync();
                    var loginContent = JsonConvert.DeserializeObject<ObjectResult<LoginContent>>(plainJson);
                    bool logged = response.IsSuccessStatusCode && loginContent?.Content?.Privileges > 0;

                    if (logged)
                    {
                        LoggedIn = true;
                        Email = loginContent!.Content!.Email;
                        Name = loginContent!.Content!.RealName;
                        Token = loginContent!.Content!.Token;
                    }

                    return new LoginResponse(logged, null);
                }
                catch
                {
                    return new LoginResponse(false, null);
                }
            }
        }
    }

    internal struct LoginResponse
    {
        public bool Success { get; }

        public string? ErrorMessage { get; }

        public LoginResponse(bool success, string? errorMessage)
        {
            Success = success;
            ErrorMessage = errorMessage;
        }
    }
}
