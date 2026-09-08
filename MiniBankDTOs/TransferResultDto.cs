namespace MiniBankDTOs
{
    public class TransferResultDto
    {
        public int TransferId { get; set; }

        public string Message { get; set; } = string.Empty;

        public string FromAccountNumber { get; set; } = string.Empty;

        public string ToAccountNumber { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public decimal NewBalance { get; set; }

        public List<LinkDto> Links { get; set; } = new();
    }
}
