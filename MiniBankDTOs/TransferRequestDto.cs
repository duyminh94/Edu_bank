using System.ComponentModel.DataAnnotations;

namespace MiniBankDTOs
{
    public class TransferRequestDto
    {
        [Required(ErrorMessage = "Receiver account number is required")]
        public string ToAccountNumber { get; set; } = string.Empty;

        [Range(1, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
    }
}
