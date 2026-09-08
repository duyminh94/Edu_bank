using MiniBankWebApi.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace MiniBankWebApi.Core.Services
{
    public class FraudCheckService : IFraudCheckService
    {
        private readonly BankDbContext db;
        private readonly IConfiguration config;

        public FraudCheckService(BankDbContext db, IConfiguration config)
        {
            this.db = db;
            this.config = config;
        }

        public async Task<(bool IsSuspicious, string Reason)> CheckAsync(int fromAccountId, decimal amount)
        {
            var maxPerTransaction = config.GetValue<decimal>("Fraud:MaxAmountPerTransaction");
            if (amount > maxPerTransaction)
            {
                return (true, $"Amount is over the single transfer limit of {maxPerTransaction:N0}");
            }

            var dayStart = DateTime.Now.AddHours(-24);
            var recentTransfers = await db.Transfers
                .Where(t => t.FromAccountId == fromAccountId && t.TransferDate >= dayStart)
                .Select(t => new { t.Amount, t.TransferDate })
                .ToListAsync();

            var maxPerDay = config.GetValue<decimal>("Fraud:MaxAmountPerDay");
            var totalToday = recentTransfers.Sum(t => t.Amount) + amount;
            if (totalToday > maxPerDay)
            {
                return (true, $"Total transferred in 24 hours is over the daily limit of {maxPerDay:N0}");
            }

            var windowMinutes = config.GetValue<int>("Fraud:WindowMinutes");
            var maxInWindow = config.GetValue<int>("Fraud:MaxTransfersInWindow");
            var windowStart = DateTime.Now.AddMinutes(-windowMinutes);
            var countInWindow = recentTransfers.Count(t => t.TransferDate >= windowStart);
            if (countInWindow >= maxInWindow)
            {
                return (true, $"{countInWindow} transfers in the last {windowMinutes} minutes");
            }

            return (false, string.Empty);
        }
    }
}
