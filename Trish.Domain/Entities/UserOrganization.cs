namespace Trish.Domain.Entities
{
    public class UserOrganization
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; } = default!;
        public Guid OrganizationId { get; set; } = default!;
    }
}
