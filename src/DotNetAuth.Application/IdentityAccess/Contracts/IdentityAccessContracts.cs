using System.Security.Claims;
using DotNetAuth.Domain.IdentityAccess.Users;

namespace DotNetAuth.Application.IdentityAccess;

public sealed record ValidationError(string Code, string Description);

public sealed record OperationResult(
    bool Succeeded,
    IReadOnlyList<ValidationError> Errors)
{
    public static OperationResult Success() => new(true, []);

    public static OperationResult Failure(params ValidationError[] errors) =>
        new(false, errors);
}

public sealed record UserCreationResult(
    bool Succeeded,
    User? User,
    IReadOnlyList<ValidationError> Errors)
{
    public static UserCreationResult Success(User user) => new(true, user, []);

    public static UserCreationResult Failure(params ValidationError[] errors) =>
        new(false, null, errors);
}

public sealed record EmailConfirmationTokenResult(
    bool Succeeded,
    string? Token,
    IReadOnlyList<ValidationError> Errors)
{
    public static EmailConfirmationTokenResult Success(string token) =>
        new(true, token, []);

    public static EmailConfirmationTokenResult Failure(params ValidationError[] errors) =>
        new(false, null, errors);
}

public sealed record RegisterUserResult(
    bool Succeeded,
    User? User,
    IReadOnlyList<ValidationError> Errors,
    string? ErrorCode = null)
{
    public static RegisterUserResult Success(User user) => new(true, user, []);

    public static RegisterUserResult Failure(
        IReadOnlyList<ValidationError> errors,
        string? errorCode = null) =>
        new(false, null, errors, errorCode);
}

public sealed record LoginUserResult(
    bool Succeeded,
    bool IsLockedOut,
    bool IsNotAllowed,
    IReadOnlyList<ValidationError> Errors)
{
    public static LoginUserResult From(AuthenticationResult result) =>
        new(result.Succeeded, result.IsLockedOut, result.IsNotAllowed, result.Errors);

    public static LoginUserResult Invalid(ValidationError error) =>
        new(false, false, false, [error]);
}

public sealed record AuthenticationResult(
    bool Succeeded,
    bool IsLockedOut,
    bool IsNotAllowed,
    IReadOnlyList<ValidationError> Errors)
{
    public static AuthenticationResult Success() => new(true, false, false, []);

    public static AuthenticationResult Failure(
        bool isLockedOut = false,
        bool isNotAllowed = false,
        params ValidationError[] errors) =>
        new(false, isLockedOut, isNotAllowed, errors);
}

public sealed record ConfirmationEmail(
    string Recipient,
    string DisplayName,
    string ConfirmationLink);

public sealed record AuthenticatedIdentity(
    string Subject,
    string? Email,
    string? Name);

public sealed record UserProfile(
    string Subject,
    string? Email,
    string? Name);

public interface IUserAccountService
{
    Task<UserCreationResult> CreateAsync(
        User user,
        string password,
        CancellationToken cancellationToken);
}

public interface IUserEmailConfirmationService
{
    Task<EmailConfirmationTokenResult> GenerateTokenAsync(
        UserId userId,
        CancellationToken cancellationToken);

    Task<OperationResult> ConfirmAsync(
        UserId userId,
        string token,
        CancellationToken cancellationToken);
}

public interface IUserAuthenticationService
{
    Task<AuthenticationResult> LoginAsync(
        EmailAddress email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken);

    Task LogoutAsync(CancellationToken cancellationToken);

    Task<User?> GetUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}

public interface IConfirmationEmailSender
{
    Task SendAsync(
        ConfirmationEmail email,
        CancellationToken cancellationToken);
}

public interface IConfirmationLinkBuilder
{
    string? Build(UserId userId, string token);
}

public interface IUserProfileCache
{
    Task<UserProfile?> GetAsync(
        string subject,
        CancellationToken cancellationToken);

    Task SetAsync(
        UserProfile profile,
        TimeSpan expiration,
        CancellationToken cancellationToken);
}
