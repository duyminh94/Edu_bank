namespace MiniBankWebApi.Core.Services
{
    public interface IMailService
    {
        Task SendOtpAsync(string toEmail, string otpCode, int expireMinutes);
    }
}
