using System.Net.Mail;
using DotNetAuth.Domain.IdentityAccess;

namespace DotNetAuth.Domain.IdentityAccess.Users;

public readonly record struct EmailAddress
{
    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EmailAddress Create(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainRuleViolationException("Email address is required.");
        }

        try
        {
            var address = new MailAddress(normalized);
            if (!string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            throw new DomainRuleViolationException("Email address is invalid.");
        }

        return new EmailAddress(normalized);
    }

    public override string ToString() => Value;
}
