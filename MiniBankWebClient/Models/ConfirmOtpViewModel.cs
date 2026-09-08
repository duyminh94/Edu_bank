using System.ComponentModel.DataAnnotations;

namespace MiniBankWebClient.Models
{
    public class ConfirmOtpViewModel
    {
        [Required]
        public string RequestId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the OTP code")]
        public string OtpCode { get; set; } = string.Empty;

        public string? MaskedEmail { get; set; }

        public string? Summary { get; set; }

        public bool RequiresFace { get; set; }

        public string? SuspiciousReason { get; set; }

        public string? FaceImageBase64 { get; set; }
    }
}
