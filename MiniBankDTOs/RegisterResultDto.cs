namespace MiniBankDTOs
{
    public class RegisterResultDto
    {
        public int AccountId { get; set; }

        public string AccountNumber { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public bool HasFace { get; set; }
    }
}
