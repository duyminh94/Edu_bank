using MiniBankWebApi.Core.Models;
using MiniBankWebApi.Helper;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MiniBankWebApi.Core.Services
{
    public class FaceApiService : IFaceApiService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        private readonly HttpClient http;
        private readonly ILogger<FaceApiService> logger;

        public FaceApiService(HttpClient http, ILogger<FaceApiService> logger)
        {
            this.http = http;
            this.logger = logger;
        }

        public async Task<float[]> GetEmbeddingAsync(string imageBase64)
        {
            if (string.IsNullOrWhiteSpace(imageBase64))
            {
                throw new ArgumentException("Face image is required");
            }

            var result = await CallRepresentAsync(imageBase64);

            if (!result.IsReal)
            {
                logger.LogWarning("Face service rejected a spoof image, score {Score}", result.AntispoofScore);
                throw new ArgumentException("This face looks like a photo or a screen, please use the real camera");
            }

            if (result.Embedding.Length != FaceHelper.DescriptorLength)
            {
                logger.LogError("Face service returned {Length} numbers, expected {Expected}",
                    result.Embedding.Length, FaceHelper.DescriptorLength);
                throw new HttpRequestException("Face service returned an unexpected embedding");
            }

            return result.Embedding;
        }

        private async Task<FaceRepresentResponse> CallRepresentAsync(string imageBase64)
        {
            HttpResponseMessage response;
            try
            {
                response = await http.PostAsJsonAsync("/represent", new { image = imageBase64 });
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogError(ex, "Cannot reach the face service");
                throw new HttpRequestException("Face service is not available", ex);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new ArgumentException(await ReadDetailAsync(response));
            }

            if (!response.IsSuccessStatusCode)
            {
                var detail = await ReadDetailAsync(response);
                logger.LogError("Face service failed with status {Status}: {Detail}", (int)response.StatusCode, detail);
                throw new HttpRequestException("Face service failed to read the image");
            }

            var result = await response.Content.ReadFromJsonAsync<FaceRepresentResponse>(JsonOptions);
            if (result == null)
            {
                throw new HttpRequestException("Face service returned an empty response");
            }

            return result;
        }

        private static async Task<string> ReadDetailAsync(HttpResponseMessage response)
        {
            try
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                return body.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String
                    ? detail.GetString()!
                    : "Face service could not read the image";
            }
            catch (Exception)
            {
                return "Face service could not read the image";
            }
        }
    }
}
