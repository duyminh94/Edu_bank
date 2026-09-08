using MiniBankDTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MiniBankWebClient.Services
{
    public class BankApiClient
    {
        private readonly string baseUrl = "http://localhost:5038/api";
        private readonly HttpClient httpClient;
        private readonly IHttpContextAccessor accessor;

        public BankApiClient(HttpClient httpClient, IHttpContextAccessor accessor)
        {
            this.httpClient = httpClient;
            this.accessor = accessor;
        }

        private ISession Session => accessor.HttpContext!.Session;

        public bool IsLoggedIn => Session.GetString("AccessToken") != null;

        public Task<HttpResponseMessage> PostAnonymousAsync<T>(string path, T body) =>
            httpClient.PostAsJsonAsync($"{baseUrl}{path}", body);

        public async Task<HttpResponseMessage> GetAsync(string path)
        {
            var response = await SendAsync(HttpMethod.Get, path, (object?)null);
            if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
            if (!await RefreshAsync()) return response;

            return await SendAsync(HttpMethod.Get, path, (object?)null);
        }

        public async Task<HttpResponseMessage> PostAsync<T>(string path, T body)
        {
            var response = await SendAsync(HttpMethod.Post, path, body);
            if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
            if (!await RefreshAsync()) return response;

            return await SendAsync(HttpMethod.Post, path, body);
        }

        public async Task<bool> RefreshAsync()
        {
            var refreshToken = Session.GetString("RefreshToken");
            if (string.IsNullOrEmpty(refreshToken)) return false;

            var response = await httpClient.PostAsJsonAsync($"{baseUrl}/auth/refresh",
                                    new RefreshDto { RefreshToken = refreshToken });
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (result == null) return false;

            SaveSession(result);
            return true;
        }

        public void SaveSession(TokenResponse result)
        {
            Session.SetString("AccessToken", result.AccessToken);
            Session.SetString("RefreshToken", result.RefreshToken);
            Session.SetString("AccountNumber", result.AccountNumber);
            Session.SetString("FullName", result.FullName);
            Session.SetString("Balance", result.Balance.ToString("N0"));
            Session.SetString("HasFace", result.HasFace.ToString());
        }

        public void SaveBalance(decimal balance) => Session.SetString("Balance", balance.ToString("N0"));

        public void SaveFaceRegistered() => Session.SetString("HasFace", "True");

        public bool HasFace => Session.GetString("HasFace") == "True";

        public void Clear() => Session.Clear();

        public static async Task<string> ReadErrorAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            try
            {
                var error = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                return error != null && error.TryGetValue("error", out var message) ? message : body;
            }
            catch (JsonException)
            {
                return body;
            }
        }

        private async Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string path, T body)
        {
            var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.GetString("AccessToken"));

            if (body != null) request.Content = JsonContent.Create(body);

            return await httpClient.SendAsync(request);
        }
    }
}
