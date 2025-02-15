namespace Trish.Domain.Entities
{
    public class Organization
    {
        public Organization(string name, string description)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
        }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public static Organization Create(string name, string description)
        {
            return new Organization(name, description);
        }
    }
}
