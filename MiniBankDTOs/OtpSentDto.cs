namespace MiniBankDTOs
{
    public class OtpSentDto
    {
        public string RequestId { get; set; } = string.Empty;

        public string MaskedEmail { get; set; } = string.Empty;

        public int ExpireMinutes { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool RequiresFace { get; set; }

        public string SuspiciousReason { get; set; } = string.Empty;
    }
}
