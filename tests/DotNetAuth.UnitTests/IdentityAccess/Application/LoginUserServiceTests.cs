using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;
using Xunit;

namespace DotNetAuth.UnitTests.IdentityAccess.Application;

public sealed class LoginUserServiceTests
{
    [Fact]
    public async Task Login_delegates_normalized_email_password_and_remember_me()
    {
        var authentication = new FakeUserAuthenticationService();
        var service = new LoginUserService(authentication);

        var result = await service.ExecuteAsync(
            new LoginUserCommand(" User@Example.com ", "password", true),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("user@example.com", authentication.Email.Value);
        Assert.Equal("password", authentication.Password);
        Assert.True(authentication.RememberMe);
    }

    private sealed class FakeUserAuthenticationService : IUserAuthenticationService
    {
        public EmailAddress Email { get; private set; }

        public string? Password { get; private set; }

        public bool RememberMe { get; private set; }

        public Task<AuthenticationResult> LoginAsync(
            EmailAddress email,
            string password,
            bool rememberMe,
            CancellationToken cancellationToken)
        {
            Email = email;
            Password = password;
            RememberMe = rememberMe;
            return Task.FromResult(AuthenticationResult.Success());
        }

        public Task LogoutAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<User?> GetUserAsync(
            System.Security.Claims.ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);
    }
}
