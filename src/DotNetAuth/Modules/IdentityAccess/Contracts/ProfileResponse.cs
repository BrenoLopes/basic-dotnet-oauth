namespace DotNetAuth.Modules.IdentityAccess.Contracts;

public sealed record ProfileResponse(string Subject, string? Email, string? Name);
