using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;
using Microsoft.AspNetCore.Identity;

namespace DotNetAuth.Infrastructure.IdentityAccess.Identity;

public sealed class IdentityEmailConfirmationService(
    UserManager<ApplicationUser> userManager) : IUserEmailConfirmationService
{
    public async Task<EmailConfirmationTokenResult> GenerateTokenAsync(
        UserId userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.Value);
        if (user is null)
        {
            return EmailConfirmationTokenResult.Failure(
                new ValidationError("userId", "User was not found."));
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        return EmailConfirmationTokenResult.Success(token);
    }

    public async Task<OperationResult> ConfirmAsync(
        UserId userId,
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.Value);
        if (user is null)
        {
            return OperationResult.Failure(
                new ValidationError("confirmation", "Invalid confirmation."));
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? OperationResult.Success()
            : OperationResult.Failure(
                new ValidationError("confirmation", "Invalid confirmation."));
    }
}
