
using Trish.Application.Abstractions.Messaging;

namespace Trish.Application.Features.Organization.Command
{
    public class AssignUserToOrganizationCommand : ICommand
    {
        public Guid OrganizationId { get; set; }
        public Guid UserId { get; set; }
    }
}
