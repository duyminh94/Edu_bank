using System.ComponentModel.DataAnnotations;

namespace MiniBankWebApi.Core.Entities
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        public int AccountId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Token { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; }

        public DateTime CreatedAt { get; set; }

        public Account Account { get; set; } = null!;
    }
}
