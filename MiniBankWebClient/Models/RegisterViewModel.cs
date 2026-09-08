using System.ComponentModel.DataAnnotations;

namespace MiniBankWebClient.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [MaxLength(100, ErrorMessage = "Full name is too long")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Email is not valid")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must have at least 6 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password")]
        [Compare(nameof(Password), ErrorMessage = "The two passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Phone number must have 10 digits and start with 0")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of birth is required")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Identity number is required")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "Identity number must have 12 digits")]
        public string IdentityNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required")]
        [MaxLength(200, ErrorMessage = "Address is too long")]
        public string Address { get; set; } = string.Empty;

        public string? FaceImageBase64 { get; set; }
    }
}
