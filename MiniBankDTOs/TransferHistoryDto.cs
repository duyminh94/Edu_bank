namespace MiniBankDTOs
{
    public class TransferHistoryDto
    {
        public int Id { get; set; }

        public string FromAccountNumber { get; set; } = string.Empty;

        public string ToAccountNumber { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime TransferDate { get; set; }
    }
}
