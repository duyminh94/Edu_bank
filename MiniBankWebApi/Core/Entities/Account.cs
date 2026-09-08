using System.ComponentModel.DataAnnotations;

namespace MiniBankWebApi.Core.Entities
{
    public class Account
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string PhoneNumber { get; set; } = string.Empty;

        public DateTime DateOfBirth { get; set; }

        [Required]
        [MaxLength(12)]
        public string IdentityNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public string? FaceDescriptor { get; set; }
    }
}
