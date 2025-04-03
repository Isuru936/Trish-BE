using Trish.Application.Abstractions.Messaging;

namespace Trish.Application.Features.Organization.Command
{
    public sealed record CreateOrganizationCommand : ICommand
    {
        public string OrganizationName { get; init; }
        public string Description { get; init; }
        public string ImageUrl { get; set; }
    }
}
