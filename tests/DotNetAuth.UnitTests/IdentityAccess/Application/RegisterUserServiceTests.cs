using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;
using Xunit;

namespace DotNetAuth.UnitTests.IdentityAccess.Application;

public sealed class RegisterUserServiceTests
{
    [Fact]
    public async Task Register_user_creates_account_and_sends_confirmation_email()
    {
        var accounts = new FakeUserAccountService();
        var confirmations = new FakeUserEmailConfirmationService();
        var emailSender = new FakeConfirmationEmailSender();
        var linkBuilder = new FakeConfirmationLinkBuilder();
        var service = new RegisterUserService(accounts, confirmations, emailSender, linkBuilder);

        var result = await service.ExecuteAsync(
            new RegisterUserCommand("user@example.com", "Correct-Horse-Battery-9!", "Example User"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal(result.User.Id, accounts.CreatedUser?.Id);
        Assert.Equal("http://localhost/confirm", emailSender.Message?.ConfirmationLink);
        Assert.Equal(result.User.Id, confirmations.GeneratedFor);
    }

    [Fact]
    public async Task Register_user_returns_validation_error_for_invalid_email()
    {
        var accounts = new FakeUserAccountService();
        var confirmations = new FakeUserEmailConfirmationService();
        var emailSender = new FakeConfirmationEmailSender();
        var linkBuilder = new FakeConfirmationLinkBuilder();
        var service = new RegisterUserService(accounts, confirmations, emailSender, linkBuilder);

        var result = await service.ExecuteAsync(
            new RegisterUserCommand("invalid", "Correct-Horse-Battery-9!", "Example User"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(accounts.CreatedUser);
        Assert.Empty(confirmations.GeneratedFor is null ? [] : new[] { confirmations.GeneratedFor });
    }

    private sealed class FakeUserAccountService : IUserAccountService
    {
        public User? CreatedUser { get; private set; }

        public Task<UserCreationResult> CreateAsync(
            User user,
            string password,
            CancellationToken cancellationToken)
        {
            CreatedUser = user;
            return Task.FromResult(UserCreationResult.Success(user));
        }
    }

    private sealed class FakeUserEmailConfirmationService : IUserEmailConfirmationService
    {
        public UserId? GeneratedFor { get; private set; }

        public Task<EmailConfirmationTokenResult> GenerateTokenAsync(
            UserId userId,
            CancellationToken cancellationToken)
        {
            GeneratedFor = userId;
            return Task.FromResult(EmailConfirmationTokenResult.Success("confirmation-token"));
        }

        public Task<OperationResult> ConfirmAsync(
            UserId userId,
            string token,
            CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Success());
    }

    private sealed class FakeConfirmationEmailSender : IConfirmationEmailSender
    {
        public ConfirmationEmail? Message { get; private set; }

        public Task SendAsync(ConfirmationEmail message, CancellationToken cancellationToken)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfirmationLinkBuilder : IConfirmationLinkBuilder
    {
        public string? Build(UserId userId, string token) => "http://localhost/confirm";
    }
}
