namespace MiniBankWebApi.Core.Models
{
    public class PendingTransfer
    {
        public string RequestId { get; set; } = string.Empty;

        public int FromAccountId { get; set; }

        public int ToAccountId { get; set; }

        public string ToAccountNumber { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string OtpCode { get; set; } = string.Empty;

        public int AttemptCount { get; set; }

        public bool RequiresFace { get; set; }

        public string SuspiciousReason { get; set; } = string.Empty;
    }
}
