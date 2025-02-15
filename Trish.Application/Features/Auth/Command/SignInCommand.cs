using Trish.Application.Abstractions.Messaging;

namespace Trish.Application.Features.Auth.Command
{
    public class SignInCommand : ICommand
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}
