using MediatR;
using Microsoft.AspNetCore.Identity;
using Trish.Application.Abstractions.Messaging;
using Trish.Application.Abstractions.Persistence;
using Trish.Application.Features.Auth.Command;
using Trish.Application.Shared;

public class SignUpCommandHandler : ICommandHandler<SignUpCommand>
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUnitOfWork unitOfWork;
    private readonly IMediator _mediatR;

    public SignUpCommandHandler(
        UserManager<IdentityUser> userManager,
        IMediator mediatR,
        RoleManager<IdentityRole> roleManager,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _mediatR = mediatR;
        _roleManager = roleManager;
        this.unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SignUpCommand command, CancellationToken cancellationToken)
    {
        // Check if user exists
        var userExists = await _userManager.FindByNameAsync(command.Username);
        if (userExists != null)
            return Result.Failure(new Error("400", "Username already exists"));

        // Create user
        var user = new IdentityUser
        {
            UserName = command.Username,
            Email = command.Email,
            SecurityStamp = Guid.NewGuid().ToString(),
        };

        // Create user and check result
        var result = await _userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
            return Result.Failure(new Error("400", string.Join(", ", result.Errors.Select(x => x.Description))));

        // Ensure changes are saved
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Handle role assignment
        var roleExists = await _roleManager.RoleExistsAsync(command.Role.ToString());
        if (!roleExists)
        {
            var roleResult = await _roleManager.CreateAsync(new IdentityRole(command.Role.ToString()));
            if (!roleResult.Succeeded)
                return Result.Failure(new Error("400", "Failed to create role"));

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Assign role to user
        var roleResult2 = await _userManager.AddToRoleAsync(user, command.Role.ToString());
        if (!roleResult2.Succeeded)
            return Result.Failure(new Error("400", string.Join(", ", roleResult2.Errors.Select(x => x.Description))));

        // Final save
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success("Signed Up Successfully");
    }
}