namespace DotNetAuth.Infrastructure.IdentityAccess.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 1025;

    public bool EnableSsl { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string FromAddress { get; init; } = "no-reply@dotnetauth.local";
}
