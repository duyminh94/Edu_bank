namespace MiniBankWebApi.Core.Services
{
    public interface IFraudCheckService
    {
        Task<(bool IsSuspicious, string Reason)> CheckAsync(int fromAccountId, decimal amount);
    }
}
