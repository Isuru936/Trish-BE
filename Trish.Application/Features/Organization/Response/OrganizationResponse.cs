namespace Trish.Application.Features.Organization.Response
{
    public sealed record OrganizationResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
    }
}
