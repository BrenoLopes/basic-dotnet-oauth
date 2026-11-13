using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Infrastructure.IdentityAccess.Caching;
using DotNetAuth.Infrastructure.IdentityAccess.Email;
using DotNetAuth.Infrastructure.IdentityAccess.Identity;
using DotNetAuth.Infrastructure.IdentityAccess.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace DotNetAuth.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var databaseConnection = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured.");
        var databaseServerVersionValue = configuration["Database:MariaDbServerVersion"]
            ?? "11.7.2";
        if (!Version.TryParse(databaseServerVersionValue, out var databaseServerVersion)
            || databaseServerVersion is null)
        {
            throw new InvalidOperationException(
                "Database:MariaDbServerVersion must be a valid version.");
        }

        var databaseRetryCount = GetPositiveInt(
            configuration,
            "Database:Retry:MaxRetryCount",
            3);
        var databaseRetryDelaySeconds = GetPositiveInt(
            configuration,
            "Database:Retry:MaxRetryDelaySeconds",
            5);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(
                databaseConnection,
                new MariaDbServerVersion(databaseServerVersion),
                mySql => mySql.EnableRetryOnFailure(
                    databaseRetryCount,
                    TimeSpan.FromSeconds(databaseRetryDelaySeconds),
                    null)
            )
        );

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = GetPositiveInt(
                    configuration,
                    "Identity:Password:RequiredLength",
                    8);
                options.Password.RequireDigit = configuration.GetValue(
                    "Identity:Password:RequireDigit",
                    true);
                options.Password.RequireUppercase = configuration.GetValue(
                    "Identity:Password:RequireUppercase",
                    true);
                options.Password.RequireLowercase = configuration.GetValue(
                    "Identity:Password:RequireLowercase",
                    true);
                options.Password.RequireNonAlphanumeric = configuration.GetValue(
                    "Identity:Password:RequireNonAlphanumeric",
                    false);
                options.User.RequireUniqueEmail = configuration.GetValue(
                    "Identity:User:RequireUniqueEmail",
                    true);
                options.SignIn.RequireConfirmedEmail = configuration.GetValue(
                    "Identity:SignIn:RequireConfirmedEmail",
                    true);
                options.Lockout.MaxFailedAccessAttempts = GetPositiveInt(
                    configuration,
                    "Identity:Lockout:MaxFailedAccessAttempts",
                    5);
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
                    GetPositiveInt(
                        configuration,
                        "Identity:Lockout:DefaultLockoutMinutes",
                        15));
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/account/login";
            options.LogoutPath = "/api/auth/logout";
            options.ReturnUrlParameter = "returnUrl";
        });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        var oauthAudience = configuration["OAuth:Audience"] ?? "api";

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<ApplicationDbContext>();
            })
            .AddServer(options =>
            {
                var issuer = configuration["OAuth:Issuer"] ?? "http://localhost:5004/";
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
                var requireProofKeyForCodeExchange = configuration.GetValue(
                    "OAuth:RequireProofKeyForCodeExchange",
                    true);
                var allowRefreshTokenFlow = configuration.GetValue(
                    "OAuth:AllowRefreshTokenFlow",
                    true);
                if (requireProofKeyForCodeExchange && !allowAuthorizationCodeFlow)
                {
                    throw new InvalidOperationException(
                        "OAuth:RequireProofKeyForCodeExchange requires OAuth:AllowAuthorizationCodeFlow.");
                }

                options.SetIssuer(new Uri(issuer));
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetTokenEndpointUris("/connect/token");
                if (allowAuthorizationCodeFlow)
                {
                    options.AllowAuthorizationCodeFlow();
                }

                if (requireProofKeyForCodeExchange)
                {
                    options.RequireProofKeyForCodeExchange();
                }

                if (allowRefreshTokenFlow)
                {
                    options.AllowRefreshTokenFlow();
                }

                options.RegisterScopes(scopes);
                options.AddEphemeralEncryptionKey();
                options.AddEphemeralSigningKey();

                var aspNetCore = options.UseAspNetCore();

                if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }

                aspNetCore.EnableAuthorizationEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.AddAudiences(oauthAudience);
                options.UseAspNetCore();
            });

        services.AddStackExchangeRedisCache(options =>
        {
            var instanceName = configuration.GetValue<string>("Cache:InstanceName")
                ?? "dotnetauth";

            options.Configuration = configuration.GetConnectionString("Cache") ?? "localhost:6379";
            options.InstanceName = $"{instanceName}:";
        });

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        services.AddScoped<IUserAccountService, IdentityUserAccountService>();
        services.AddScoped<IUserEmailConfirmationService, IdentityEmailConfirmationService>();
        services.AddScoped<IUserAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IUserProfileCache, RedisUserProfileCache>();
        services.AddSingleton<IConfirmationEmailSender, SmtpEmailSender>();

        return services;
    }

    private static int GetPositiveInt(
        IConfiguration configuration,
        string key,
        int defaultValue)
    {
        var value = configuration.GetValue<int?>(key) ?? defaultValue;
        return value > 0
            ? value
            : throw new InvalidOperationException($"{key} must be greater than zero.");
    }
}
