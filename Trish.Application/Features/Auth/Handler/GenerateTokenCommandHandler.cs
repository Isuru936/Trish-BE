using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Trish.Application.Features.Auth.Command;

namespace Trish.Application.Features.Auth.Handler
{
    public class GenerateTokenCommandHandler : IRequestHandler<GenerateTokenCommand, JwtSecurityToken>
    {
        private readonly IConfiguration _configuration;

        public GenerateTokenCommandHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<JwtSecurityToken> Handle(GenerateTokenCommand request, CancellationToken cancellationToken)
        {
            var authSignInKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]!));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddHours(3),
                claims: request.AuthClaims,
                signingCredentials: new SigningCredentials(authSignInKey, SecurityAlgorithms.HmacSha256)
            );

            return Task.FromResult(token);
        }
    }
}
