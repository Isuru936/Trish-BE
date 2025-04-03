namespace Trish.Domain.Entities
{
    public class Organization
    {
        public Organization(string name, string description, string imageUrl)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            ImageUrl = imageUrl;
        }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }

        public static Organization Create(string name, string description, string ImageUrl)
        {
            return new Organization(name, description, ImageUrl);
        }
    }
}
