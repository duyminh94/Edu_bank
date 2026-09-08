namespace MiniBankDTOs
{
    public class LinkDto
    {
        public string Rel { get; set; } = string.Empty;

        public string Href { get; set; } = string.Empty;

        public LinkDto()
        {
        }

        public LinkDto(string rel, string href)
        {
            Rel = rel;
            Href = href;
        }
    }
}
