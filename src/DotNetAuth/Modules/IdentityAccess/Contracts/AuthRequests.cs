using System.ComponentModel.DataAnnotations;

namespace DotNetAuth.Modules.IdentityAccess.Contracts;

public sealed record RegisterRequest(
    [param: Required, EmailAddress] string Email,
    [param: Required, MinLength(8), RegularExpression(
        @"^(?=.*\p{Lu})(?=.*\p{Ll}).+$",
        ErrorMessage = "A senha deve conter pelo menos uma letra maiúscula e uma letra minúscula.")] string Password,
    [param: Required, MaxLength(120)] string DisplayName);

public sealed record LoginRequest(
    [param: Required, EmailAddress] string Email,
    [param: Required] string Password,
    bool RememberMe = false);
