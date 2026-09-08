namespace MiniBankWebApi.Core.Services
{
    public interface IFaceApiService
    {
        Task<float[]> GetEmbeddingAsync(string imageBase64);
    }
}
