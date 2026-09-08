using System.ComponentModel.DataAnnotations;

namespace MiniBankDTOs
{
    public class FaceLoginDto
    {
        [Required(ErrorMessage = "Face image is required")]
        public string ImageBase64 { get; set; } = string.Empty;
    }
}
