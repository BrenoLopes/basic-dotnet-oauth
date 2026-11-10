using System.Security.Claims;
using DotNetAuth.Application.IdentityAccess;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace DotNetAuth.Modules.IdentityAccess.Controllers;

public sealed class AuthorizationController(
    AuthorizeUserService authorizeUser,
    IConfiguration configuration) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var authentication = await HttpContext.AuthenticateAsync(
            IdentityConstants.ApplicationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            if (configuration.GetValue<bool>("Web:Authentication:Registration"))
            {
                var returnUrl = $"{Request.PathBase}{Request.Path}{Request.QueryString}";
                var loginUrl = QueryHelpers.AddQueryString(
                    "/account/login",
                    "returnUrl",
                    returnUrl);
                return Redirect(loginUrl);
            }

            return Unauthorized(new { Error = "login_required" });
        }

        var user = await authorizeUser.ExecuteAsync(
            authentication.Principal,
            cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { Error = "login_required" });
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, user.Id.Value));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Email, user.Email.Value));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Name, user.DisplayName));

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources("api");

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken);
        }

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
