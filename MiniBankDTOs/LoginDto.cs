using System.ComponentModel.DataAnnotations;

namespace MiniBankDTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Account number is required")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
