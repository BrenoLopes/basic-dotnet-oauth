using DotNetAuth.Domain.IdentityAccess.Users;
using Microsoft.AspNetCore.Identity;

namespace DotNetAuth.Infrastructure.IdentityAccess.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User ToDomain() => User.Rehydrate(
        new UserId(Id),
        EmailAddress.Create(Email ?? throw new InvalidOperationException("Identity user email is required.")),
        DisplayName,
        CreatedAt,
        EmailConfirmed);

    public static ApplicationUser FromDomain(User user) => new()
    {
        Id = user.Id.Value,
        UserName = user.Email.Value,
        Email = user.Email.Value,
        DisplayName = user.DisplayName,
        CreatedAt = user.CreatedAt,
        EmailConfirmed = user.EmailConfirmed
    };
}
