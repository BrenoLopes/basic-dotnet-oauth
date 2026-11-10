using DotNetAuth.Domain.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;
using Xunit;

namespace DotNetAuth.UnitTests.IdentityAccess.Domain;

public sealed class UserTests
{
    [Fact]
    public void Register_creates_user_with_normalized_email_and_display_name()
    {
        var user = User.Register(
            EmailAddress.Create("  User@Example.com "),
            "  Example User  ");

        Assert.Equal("user@example.com", user.Email.Value);
        Assert.Equal("Example User", user.DisplayName);
        Assert.False(user.EmailConfirmed);
        Assert.NotEqual(default, user.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void Email_address_rejects_invalid_values(string value)
    {
        Assert.Throws<DomainRuleViolationException>(() => EmailAddress.Create(value));
    }

    [Fact]
    public void Confirm_email_changes_the_user_state()
    {
        var user = User.Register(EmailAddress.Create("user@example.com"), "Example User");

        user.ConfirmEmail();

        Assert.True(user.EmailConfirmed);
    }
}
