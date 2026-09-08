using MiniBankDTOs;

namespace MiniBankWebApi.Core.Services
{
    public interface IAuthService
    {
        Task<RegisterResultDto> RegisterAsync(RegisterDto dto);
        Task<TokenResponse?> LoginAsync(LoginDto dto);
        Task<TokenResponse?> RefreshAsync(string refreshToken);
        Task<bool> RegisterFaceAsync(int accountId, string imageBase64);
        Task<TokenResponse?> FaceLoginAsync(FaceLoginDto dto);
    }
}
