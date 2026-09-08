namespace MiniBankDTOs
{
    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public int AccountId { get; set; }

        public string AccountNumber { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public bool HasFace { get; set; }
    }
}
