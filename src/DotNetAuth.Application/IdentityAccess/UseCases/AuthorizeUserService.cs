using System.Security.Claims;
using DotNetAuth.Domain.IdentityAccess.Users;

namespace DotNetAuth.Application.IdentityAccess;

public sealed class AuthorizeUserService(
    IUserAuthenticationService authenticationService)
{
    public Task<User?> ExecuteAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken) =>
        authenticationService.GetUserAsync(principal, cancellationToken);
}
