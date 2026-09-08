using System.Text.Json;

namespace MiniBankWebApi.Helper
{
    public static class FaceHelper
    {
        public const int DescriptorLength = 512;

        public static string ToJson(float[] descriptor) => JsonSerializer.Serialize(descriptor);

        public static float[]? FromJson(string? json) =>
            string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<float[]>(json);

        public static double Distance(float[] first, float[] second)
        {
            if (first.Length != second.Length)
            {
                throw new ArgumentException($"Two face descriptors must have the same length, got {first.Length} and {second.Length}");
            }

            double dotProduct = 0;
            double firstSquareSum = 0;
            double secondSquareSum = 0;

            for (var i = 0; i < first.Length; i++)
            {
                dotProduct += first[i] * second[i];
                firstSquareSum += first[i] * first[i];
                secondSquareSum += second[i] * second[i];
            }

            var lengthProduct = Math.Sqrt(firstSquareSum) * Math.Sqrt(secondSquareSum);
            if (lengthProduct == 0) return 1;

            return 1 - (dotProduct / lengthProduct);
        }
    }
}
