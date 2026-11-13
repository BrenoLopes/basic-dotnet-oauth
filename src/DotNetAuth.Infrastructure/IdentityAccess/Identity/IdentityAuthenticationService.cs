using System.Security.Claims;
using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace DotNetAuth.Infrastructure.IdentityAccess.Identity;

public sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IConfiguration configuration) : IUserAuthenticationService
{
    public async Task<AuthenticationResult> LoginAsync(
        EmailAddress email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = await signInManager.PasswordSignInAsync(
            email.Value,
            password,
            isPersistent: rememberMe,
            lockoutOnFailure: configuration.GetValue("Identity:SignIn:LockoutOnFailure", true)
        );

        if (result.Succeeded)
        {
            return AuthenticationResult.Success();
        }

        return AuthenticationResult.Failure(
            isLockedOut: result.IsLockedOut,
            isNotAllowed: result.IsNotAllowed,
            new ValidationError("authentication", "Invalid credentials."));
    }

    public Task LogoutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return signInManager.SignOutAsync();
    }

    public async Task<User?> GetUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal);
        return user?.ToDomain();
    }
}
