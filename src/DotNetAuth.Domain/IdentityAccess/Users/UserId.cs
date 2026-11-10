namespace DotNetAuth.Domain.IdentityAccess.Users;

public readonly record struct UserId
{
    public UserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("User id cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public static UserId New() => new(Guid.NewGuid().ToString());

    public override string ToString() => Value;
}
