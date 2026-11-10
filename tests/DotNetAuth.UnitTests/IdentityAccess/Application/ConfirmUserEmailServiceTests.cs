using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;
using Xunit;

namespace DotNetAuth.UnitTests.IdentityAccess.Application;

public sealed class ConfirmUserEmailServiceTests
{
    [Fact]
    public async Task Confirm_email_delegates_user_id_and_token_to_the_confirmation_port()
    {
        var confirmation = new FakeUserEmailConfirmationService();
        var service = new ConfirmUserEmailService(confirmation);

        var result = await service.ExecuteAsync(
            "user-1",
            "confirmation-token",
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(new UserId("user-1"), confirmation.UserId);
        Assert.Equal("confirmation-token", confirmation.Token);
    }

    private sealed class FakeUserEmailConfirmationService : IUserEmailConfirmationService
    {
        public UserId UserId { get; private set; }

        public string? Token { get; private set; }

        public Task<EmailConfirmationTokenResult> GenerateTokenAsync(
            UserId userId,
            CancellationToken cancellationToken) =>
            Task.FromResult(EmailConfirmationTokenResult.Success("token"));

        public Task<OperationResult> ConfirmAsync(
            UserId userId,
            string token,
            CancellationToken cancellationToken)
        {
            UserId = userId;
            Token = token;
            return Task.FromResult(OperationResult.Success());
        }
    }
}
