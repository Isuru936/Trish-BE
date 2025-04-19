using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Trish.Application.Abstractions.Messaging;
using Trish.Application.Abstractions.Persistence;
using Trish.Application.Features.Auth.Command;
using Trish.Application.Shared;
using Trish.Domain.Entities;
using Trish.Domain.Enums;

namespace Trish.Application.Features.Auth.Handler
{
    public class SignInCommandHandler : ICommandHandler<SignInCommand>
    {

        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IGenericRepository<UserOrganization> _genericRepository;
        private readonly IConfiguration _configuration;
        private readonly IMediator _mediatR;

        public SignInCommandHandler(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, IConfiguration configuration, IMediator mediatR, IGenericRepository<UserOrganization> genericRepository)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _mediatR = mediatR;
            _genericRepository = genericRepository;
        }

        public async Task<Result> Handle(SignInCommand command, CancellationToken cancellationToken)
        {
            IdentityUser? user = await _userManager.FindByEmailAsync(command.Email);
            if (user == null)
            {
                return Result.Failure(403, new Error("User Not Found"));
            }

            SignInResult result = await _signInManager.PasswordSignInAsync(user, command.Password, false, false);

            if (!result.Succeeded)
            {
                return Result.Failure(403, new Error("Invalid Password"));
            }

            var role = await _userManager.GetRolesAsync(user);
            UserOrganization? organization = null;
            List<Claim> authClaims;

            if (role[0] == "Admin")
            {
                var organizations = await _genericRepository.GetAll();

                organization = organizations.Where(s => s.UserId == Guid.Parse(user.Id)).SingleOrDefault(); // Handle null case safely

                authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName!),
                    new Claim(ClaimTypes.Email, user.Email!),
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Role, role[0]),
                    new Claim(ClaimTypes.Sid, organization.Id.ToString())
                };
            }

            authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Role, role[0]),
            };

            var token = await _mediatR.Send(new GenerateTokenCommand(authClaims), cancellationToken);

            var assignedRole = Role.USER;

            if (role[0].ToLower() == Role.ADMIN.ToString().ToLower())
            {
                assignedRole = Role.ADMIN;
            }

            var userResponse = new
            {
                user.UserName,
                user.Email,
                user.Id,
                role = assignedRole,
                organizationId = organization?.OrganizationId,
                token = new JwtSecurityTokenHandler().WriteToken(token)
            };

            return Result.Success(userResponse);

        }
    }
}
