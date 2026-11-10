namespace DotNetAuth.Application.IdentityAccess;

public sealed class LogoutUserService(
    IUserAuthenticationService authenticationService)
{
    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        authenticationService.LogoutAsync(cancellationToken);
}
