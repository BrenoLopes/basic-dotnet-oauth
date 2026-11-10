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

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(
                databaseConnection,
                new MariaDbServerVersion(new Version(11, 7, 2)),
                mySql => mySql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            var webAuthenticationRegistrationEnabled = configuration.GetValue<bool>("Web:Authentication:Registration");
            options.LoginPath = webAuthenticationRegistrationEnabled
                ? "/account/login"
                : "/api/auth/login";
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

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<ApplicationDbContext>();
            })
            .AddServer(options =>
            {
                var issuer = configuration["OAuth:Issuer"] ?? "http://localhost:5004/";

                options.SetIssuer(new Uri(issuer));
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetTokenEndpointUris("/connect/token");
                options.AllowAuthorizationCodeFlow();
                options.RequireProofKeyForCodeExchange();
                options.AllowRefreshTokenFlow();
                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email,
                    "api");
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
                options.AddAudiences("api");
                options.UseAspNetCore();
            });

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Cache") ?? "localhost:6379";
            options.InstanceName = "dotnetauth:";
        });

        services.Configure<SmtpOptions>(
            configuration.GetSection(SmtpOptions.SectionName));

        services.AddScoped<IUserAccountService, IdentityUserAccountService>();
        services.AddScoped<IUserEmailConfirmationService, IdentityEmailConfirmationService>();
        services.AddScoped<IUserAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IUserProfileCache, RedisUserProfileCache>();
        services.AddSingleton<IConfirmationEmailSender, SmtpEmailSender>();

        return services;
    }
}
