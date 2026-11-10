using DotNetAuth.Domain.IdentityAccess;

namespace DotNetAuth.Domain.IdentityAccess.Users;

public sealed class User
{
    private User(
        UserId id,
        EmailAddress email,
        string displayName,
        DateTimeOffset createdAt,
        bool emailConfirmed)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        CreatedAt = createdAt;
        EmailConfirmed = emailConfirmed;
    }

    public UserId Id { get; }

    public EmailAddress Email { get; }

    public string DisplayName { get; }

    public DateTimeOffset CreatedAt { get; }

    public bool EmailConfirmed { get; private set; }

    public static User Register(
        EmailAddress email,
        string displayName,
        DateTimeOffset? createdAt = null,
        UserId? id = null)
    {
        var normalizedDisplayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            throw new DomainRuleViolationException("Display name is required.");
        }

        if (normalizedDisplayName.Length > 120)
        {
            throw new DomainRuleViolationException("Display name cannot exceed 120 characters.");
        }

        return new User(
            id ?? UserId.New(),
            email,
            normalizedDisplayName,
            createdAt ?? DateTimeOffset.UtcNow,
            emailConfirmed: false);
    }

    public static User Rehydrate(
        UserId id,
        EmailAddress email,
        string displayName,
        DateTimeOffset createdAt,
        bool emailConfirmed) =>
        new(id, email, displayName, createdAt, emailConfirmed);

    public void ConfirmEmail()
    {
        EmailConfirmed = true;
    }
}
