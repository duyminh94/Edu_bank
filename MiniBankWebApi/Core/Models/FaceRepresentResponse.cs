namespace MiniBankWebApi.Core.Models
{
    public class FaceRepresentResponse
    {
        public bool IsReal { get; set; }
        public double AntispoofScore { get; set; }
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
