using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Infrastructure.IdentityAccess.OAuth;
using DotNetAuth.Infrastructure.IdentityAccess.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace DotNetAuth.IntegrationTests.IdentityAccess;

public sealed class OAuthApiTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;
    private readonly AuthApiFactory _factory;

    public OAuthApiTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost:5004")
        });
    }

    [Fact]
    public async Task Discovery_document_exposes_oauth_endpoints()
    {
        var response = await _client.GetAsync("/.well-known/openid-configuration");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            "http://localhost:5004/connect/authorize",
            document.RootElement.GetProperty("authorization_endpoint").GetString());
        Assert.Equal(
            "http://localhost:5004/connect/token",
            document.RootElement.GetProperty("token_endpoint").GetString());
    }

    [Fact]
    public async Task Protected_profile_requires_an_access_token()
    {
        var response = await _client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void OpenIddict_protocol_type_columns_allow_long_values()
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokenType = database.Model
            .FindEntityType("OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken")
            ?.FindProperty("Type");
        var authorizationType = database.Model
            .FindEntityType("OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization")
            ?.FindProperty("Type");

        Assert.True(tokenType?.GetMaxLength() >= 100);
        Assert.True(authorizationType?.GetMaxLength() >= 100);
    }

    [Fact]
    public async Task Web_login_page_is_not_available_when_web_authentication_is_disabled()
    {
        var response = await _client.GetAsync("/account/login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Api_registration_accepts_eight_character_password_without_digit_or_symbol()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "eight-character-password@example.com",
            Password = "Abcdefgh",
            DisplayName = "Eight Character Password"
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task Api_registration_rejects_password_shorter_than_eight_characters()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "short-password@example.com",
            Password = "Abcdefg",
            DisplayName = "Short Password"
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("minimum length of '8'", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("abcdefgh", "minuscula")]
    [InlineData("ABCDEFGH", "maiuscula")]
    public async Task Api_registration_rejects_password_without_required_letter_case(
        string password,
        string missingCase)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"missing-{missingCase}@example.com",
            Password = password,
            DisplayName = "Invalid Password"
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("uma letra", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Authorization_code_can_be_exchanged_for_an_access_token()
    {
        const string email = "oauth-user@example.com";
        const string password = "Correct-Horse-Battery-9!";
        const string redirectUri = "http://localhost:5004/oauth/callback";
        const string verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = password,
            DisplayName = "OAuth User"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var confirmationResponse = await _client.GetAsync(_factory.LastConfirmationLink);
        var confirmationBody = await confirmationResponse.Content.ReadAsStringAsync();
        Assert.True(confirmationResponse.StatusCode == HttpStatusCode.OK, confirmationBody);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var codeChallenge = WebEncoders.Base64UrlEncode(
            SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        var authorizationUri = QueryHelpers.AddQueryString(
            "/connect/authorize",
            new Dictionary<string, string?>
            {
                ["client_id"] = "dotnetauth-local",
                ["response_type"] = "code",
                ["redirect_uri"] = redirectUri,
                ["scope"] = "openid profile email api",
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256"
            });

        var authorizationResponse = await _client.GetAsync(authorizationUri);
        Assert.Equal(HttpStatusCode.Redirect, authorizationResponse.StatusCode);
        var location = Assert.IsType<Uri>(authorizationResponse.Headers.Location);
        var code = QueryHelpers.ParseQuery(location.Query)["code"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(code));

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "dotnetauth-local",
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = verifier
            })
        };

        var tokenResponse = await _client.SendAsync(tokenRequest);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.StatusCode == HttpStatusCode.OK, tokenBody);
        using var tokenDocument = JsonDocument.Parse(tokenBody);
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/api/profile");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var profileResponse = await _client.SendAsync(profileRequest);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
    }
}

public class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly TestEmailSender _emailSender = new();

    public string LastConfirmationLink => _emailSender.LastConfirmationLink
        ?? throw new InvalidOperationException("No confirmation email was sent.");

    protected virtual bool WebAuthenticationRegistrationEnabled => false;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        using var scope = Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await database.Database.EnsureCreatedAsync();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        await OAuthSeeder.SeedAsync(scope.ServiceProvider, configuration);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ApplyMigrations"] = "false",
                ["ConnectionStrings:Default"] = "Server=unused;Database=unused;",
                ["OAuth:Issuer"] = "http://localhost:5004/",
                ["Web:Authentication:Registration"] = WebAuthenticationRegistrationEnabled.ToString()
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
            services.RemoveAll<IConfirmationEmailSender>();
            services.AddSingleton<IConfirmationEmailSender>(_emailSender);
        });
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}

public sealed class TestEmailSender : IConfirmationEmailSender
{
    public string? LastConfirmationLink { get; private set; }

    public Task SendAsync(
        ConfirmationEmail email,
        CancellationToken cancellationToken)
    {
        LastConfirmationLink = email.ConfirmationLink;
        return Task.CompletedTask;
    }
}
