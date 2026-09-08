using System.ComponentModel.DataAnnotations;

namespace MiniBankDTOs
{
    public class RefreshDto
    {
        [Required(ErrorMessage = "Refresh token is required")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
