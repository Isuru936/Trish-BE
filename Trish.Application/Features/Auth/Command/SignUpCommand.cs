using Trish.Application.Abstractions.Messaging;
using Trish.Domain.Enums;

namespace Trish.Application.Features.Auth.Command
{
    public class SignUpCommand : ICommand
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Role Role { get; set; }
        public Guid OrganizationId { get; set; }
    }
}
