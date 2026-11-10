using DotNetAuth.Domain.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;

namespace DotNetAuth.Application.IdentityAccess;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string DisplayName);

public sealed class RegisterUserService(
    IUserAccountService accountService,
    IUserEmailConfirmationService emailConfirmationService,
    IConfirmationEmailSender emailSender,
    IConfirmationLinkBuilder linkBuilder)
{
    public async Task<RegisterUserResult> ExecuteAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        User user;
        try
        {
            user = User.Register(
                EmailAddress.Create(command.Email),
                command.DisplayName);
        }
        catch (DomainRuleViolationException exception)
        {
            return RegisterUserResult.Failure(
                [new ValidationError("request", exception.Message)]);
        }

        var creation = await accountService.CreateAsync(
            user,
            command.Password,
            cancellationToken);
        if (!creation.Succeeded || creation.User is null)
        {
            return RegisterUserResult.Failure(creation.Errors);
        }

        user = creation.User;
        var tokenResult = await emailConfirmationService.GenerateTokenAsync(
            user.Id,
            cancellationToken);
        if (!tokenResult.Succeeded || tokenResult.Token is null)
        {
            return RegisterUserResult.Failure(tokenResult.Errors);
        }

        var confirmationLink = linkBuilder.Build(user.Id, tokenResult.Token);
        if (confirmationLink is null)
        {
            return RegisterUserResult.Failure(
                [],
                "confirmation_link_unavailable");
        }

        await emailSender.SendAsync(
            new ConfirmationEmail(
                user.Email.Value,
                user.DisplayName,
                confirmationLink),
            cancellationToken);

        return RegisterUserResult.Success(user);
    }
}
