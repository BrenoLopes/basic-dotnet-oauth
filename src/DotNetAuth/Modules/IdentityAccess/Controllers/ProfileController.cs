using System.Security.Claims;
using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Modules.IdentityAccess.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace DotNetAuth.Modules.IdentityAccess.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public sealed class ProfileController(GetUserProfileService getUserProfile) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(OpenIddictConstants.Claims.Subject)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Unauthorized();
        }

        var profile = await getUserProfile.ExecuteAsync(
            new AuthenticatedIdentity(
                subject,
                User.FindFirstValue(ClaimTypes.Email)
                    ?? User.FindFirstValue(OpenIddictConstants.Claims.Email),
                User.FindFirstValue(ClaimTypes.Name)
                    ?? User.FindFirstValue(OpenIddictConstants.Claims.Name)),
            cancellationToken);

        return Ok(new ProfileResponse(profile.Subject, profile.Email, profile.Name));
    }
}

