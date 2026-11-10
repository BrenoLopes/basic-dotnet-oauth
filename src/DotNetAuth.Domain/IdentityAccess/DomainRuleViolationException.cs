namespace DotNetAuth.Domain.IdentityAccess;

public sealed class DomainRuleViolationException(string message) : Exception(message);
