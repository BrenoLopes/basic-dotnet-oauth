using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;
using Microsoft.AspNetCore.Identity;

namespace DotNetAuth.Infrastructure.IdentityAccess.Identity;

public sealed class IdentityUserAccountService(
    UserManager<ApplicationUser> userManager) : IUserAccountService
{
    public async Task<UserCreationResult> CreateAsync(
        User user,
        string password,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var identityUser = ApplicationUser.FromDomain(user);
        var result = await userManager.CreateAsync(identityUser, password);

        if (!result.Succeeded)
        {
            return UserCreationResult.Failure(
                result.Errors
                    .Select(error => new ValidationError(error.Code, error.Description))
                    .ToArray()
                );
        }

        return UserCreationResult.Success(identityUser.ToDomain());
    }
}
