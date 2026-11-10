using System.Text;
using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Domain.IdentityAccess.Users;
using Microsoft.AspNetCore.WebUtilities;

namespace DotNetAuth.Modules.IdentityAccess.Composition;

public sealed class AspNetConfirmationLinkBuilder(
    IHttpContextAccessor httpContextAccessor,
    LinkGenerator linkGenerator) : IConfirmationLinkBuilder
{
    public string? Build(UserId userId, string token)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        return httpContext is null
            ? null
            : linkGenerator.GetUriByAction(
                httpContext,
                action: "ConfirmEmail",
                controller: "Auth",
                values: new { userId = userId.Value, token = encodedToken });
    }
}
