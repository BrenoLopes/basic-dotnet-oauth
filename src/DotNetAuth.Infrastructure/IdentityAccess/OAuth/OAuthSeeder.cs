using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace DotNetAuth.Infrastructure.IdentityAccess.OAuth;

public static class OAuthSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = configuration["OAuth:ClientId"] ?? "dotnetauth-local";
        var scopes = configuration.GetSection("OAuth:Scopes").Get<string[]>()
            ?? ["openid", "profile", "email", "api"];
        if (scopes.Length == 0 || scopes.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "OAuth:Scopes must contain at least one non-empty scope.");
        }

        var allowAuthorizationCodeFlow = configuration.GetValue(
            "OAuth:AllowAuthorizationCodeFlow",
            true);
        var allowRefreshTokenFlow = configuration.GetValue(
            "OAuth:AllowRefreshTokenFlow",
            true);

        if (await manager.FindByClientIdAsync(clientId) is not null)
        {
            return;
        }

        var redirectUri = configuration["OAuth:ClientRedirectUri"]
            ?? "http://localhost:5004/oauth/callback";

        var permissions = new List<string>
        {
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token
        };

        if (allowAuthorizationCodeFlow)
        {
            permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        }

        if (allowRefreshTokenFlow)
        {
            permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        }

        permissions.AddRange(
            scopes.Select(scope => OpenIddictConstants.Permissions.Prefixes.Scope + scope));

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = "DotNetAuth local client",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            RedirectUris = { new Uri(redirectUri) }
        };

        foreach (var permission in permissions)
        {
            descriptor.Permissions.Add(permission);
        }

        await manager.CreateAsync(descriptor);
    }
}
