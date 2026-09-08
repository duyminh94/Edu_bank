using System.ComponentModel.DataAnnotations;

namespace MiniBankDTOs
{
    public class ConfirmOtpDto
    {
        [Required(ErrorMessage = "Request id is required")]
        public string RequestId { get; set; } = string.Empty;

        [Required(ErrorMessage = "OTP code is required")]
        public string OtpCode { get; set; } = string.Empty;

        public string? FaceImageBase64 { get; set; }
    }
}
