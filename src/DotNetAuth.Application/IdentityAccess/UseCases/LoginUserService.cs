using DotNetAuth.Domain.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;

namespace DotNetAuth.Application.IdentityAccess;

public sealed record LoginUserCommand(
    string Email,
    string Password,
    bool RememberMe);

public sealed class LoginUserService(
    IUserAuthenticationService authenticationService)
{
    public async Task<LoginUserResult> ExecuteAsync(
        LoginUserCommand command,
        CancellationToken cancellationToken)
    {
        EmailAddress email;
        try
        {
            email = EmailAddress.Create(command.Email);
        }
        catch (DomainRuleViolationException exception)
        {
            return LoginUserResult.Invalid(
                new ValidationError("email", exception.Message));
        }

        var result = await authenticationService.LoginAsync(
            email,
            command.Password,
            command.RememberMe,
            cancellationToken);

        return LoginUserResult.From(result);
    }
}
