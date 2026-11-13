using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace DotNetAuth.IntegrationTests.IdentityAccess;

public sealed class WebAuthenticationApiTests : IClassFixture<WebAuthenticationApiFactory>
{
    private readonly HttpClient _client;
    private readonly WebAuthenticationApiFactory _factory;

    public WebAuthenticationApiTests(WebAuthenticationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost:5004")
        });
    }

    [Fact]
    public async Task Web_pages_are_available_when_web_authentication_is_enabled()
    {
        var loginResponse = await _client.GetAsync("/account/login");
        var registerResponse = await _client.GetAsync("/account/register");

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
    }

    [Fact]
    public async Task Account_pages_use_shared_styles_and_preserve_local_return_url()
    {
        var loginResponse = await _client.GetAsync("/account/login?returnUrl=%2Flocal-target");
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        var registerResponse = await _client.GetAsync("/account/register?returnUrl=%2Flocal-target");
        var registerBody = await registerResponse.Content.ReadAsStringAsync();
        var stylesheetResponse = await _client.GetAsync("/css/account.css");

        Assert.Equal(HttpStatusCode.OK, stylesheetResponse.StatusCode);
        Assert.Contains("/css/account.css", loginBody, StringComparison.Ordinal);
        Assert.Contains("/account/register?returnUrl=%2Flocal-target", loginBody, StringComparison.Ordinal);
        Assert.Contains("/account/login?returnUrl=%2Flocal-target", registerBody, StringComparison.Ordinal);
        Assert.Contains("name=\"ReturnUrl\"", registerBody, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"Input_Email_error\"", loginBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Web_registration_success_offers_login_link_with_local_return_url()
    {
        var registerPage = await _client.GetAsync("/account/register?returnUrl=%2Flocal-target");
        var registerToken = ExtractAntiforgeryToken(await registerPage.Content.ReadAsStringAsync());

        var response = await _client.PostAsync(
            "/account/register",
            Form(new Dictionary<string, string>
            {
                ["Input.Email"] = "success-action@example.com",
                ["Input.Password"] = "Abcdefg1",
                ["Input.DisplayName"] = "Success Action User",
                ["ReturnUrl"] = "/local-target",
                ["__RequestVerificationToken"] = registerToken
            }));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ir para login", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/account/login?returnUrl=%2Flocal-target", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Web_registration_requires_antiforgery_token()
    {
        using var response = await _client.PostAsync(
            "/account/register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = "web-user@example.com",
                ["Input.Password"] = "Correct-Horse-Battery-9!",
                ["Input.DisplayName"] = "Web User"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Web_registration_and_login_create_application_cookie()
    {
        const string email = "web-user@example.com";
        const string password = "Abcdefg1";

        var registerPage = await _client.GetAsync("/account/register");
        var registerToken = ExtractAntiforgeryToken(await registerPage.Content.ReadAsStringAsync());

        var registerResponse = await _client.PostAsync(
            "/account/register",
            Form(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.DisplayName"] = "Web User",
                ["__RequestVerificationToken"] = registerToken
            }));

        var registerBody = await registerResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        Assert.Contains("confirme o e-mail", registerBody, StringComparison.OrdinalIgnoreCase);

        var confirmationResponse = await _client.GetAsync(_factory.LastConfirmationLink);
        Assert.Equal(HttpStatusCode.OK, confirmationResponse.StatusCode);

        var loginPage = await _client.GetAsync("/account/login");
        var loginToken = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());

        var loginResponse = await _client.PostAsync(
            "/account/login",
            Form(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.RememberMe"] = "false",
                ["ReturnUrl"] = "/local-target",
                ["__RequestVerificationToken"] = loginToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/local-target", loginResponse.Headers.Location?.OriginalString);

        var meResponse = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Web_login_rejects_external_return_url()
    {
        const string email = "external-return@example.com";
        const string password = "Correct-Horse-Battery-9!";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = password,
            DisplayName = "External Return User"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var confirmationResponse = await _client.GetAsync(_factory.LastConfirmationLink);
        Assert.Equal(HttpStatusCode.OK, confirmationResponse.StatusCode);

        var loginPage = await _client.GetAsync("/account/login");
        var loginToken = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());

        var response = await _client.PostAsync(
            "/account/login",
            Form(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["ReturnUrl"] = "https://evil.example/steal",
                ["__RequestVerificationToken"] = loginToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Cookie_authentication_does_not_authorize_bearer_profile()
    {
        const string email = "cookie-only@example.com";
        const string password = "Correct-Horse-Battery-9!";

        await RegisterAndLoginAsync(email, password);

        var response = await _client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OAuth_authorization_redirects_to_web_login_when_cookie_is_missing()
    {
        var authorizationUri = QueryHelpers.AddQueryString(
            "/connect/authorize",
            new Dictionary<string, string?>
            {
                ["client_id"] = "dotnetauth-local",
                ["response_type"] = "code",
                ["redirect_uri"] = "http://localhost:5004/oauth/callback",
                ["scope"] = "openid profile email api",
                ["code_challenge"] = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~",
                ["code_challenge_method"] = "S256"
            });

        var response = await _client.GetAsync(authorizationUri);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        var locationPath = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString.Split('?', 2)[0];
        var absoluteLocation = location.IsAbsoluteUri
            ? location
            : new Uri($"http://localhost{location.OriginalString}");
        Assert.Equal("/account/login", locationPath);
        Assert.Contains("returnUrl=", absoluteLocation.Query, StringComparison.Ordinal);
        Assert.Contains("/connect/authorize", Uri.UnescapeDataString(absoluteLocation.Query), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorization_code_can_be_exchanged_after_web_login()
    {
        const string email = "web-oauth-user@example.com";
        const string password = "Correct-Horse-Battery-9!";
        const string redirectUri = "http://localhost:5004/oauth/callback";
        const string verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

        await RegisterAndLoginAsync(email, password);

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
        var authorizationLocation = Assert.IsType<Uri>(authorizationResponse.Headers.Location);
        var code = QueryHelpers.ParseQuery(
                authorizationLocation.IsAbsoluteUri
                    ? authorizationLocation.Query
                    : new Uri($"http://localhost{authorizationLocation.OriginalString}").Query)["code"]
            .ToString();
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

    private async Task RegisterAndLoginAsync(string email, string password)
    {
        var registerPage = await _client.GetAsync("/account/register");
        var registerToken = ExtractAntiforgeryToken(await registerPage.Content.ReadAsStringAsync());
        var registerResponse = await _client.PostAsync(
            "/account/register",
            Form(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.DisplayName"] = "Cookie User",
                ["__RequestVerificationToken"] = registerToken
            }));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var confirmationResponse = await _client.GetAsync(_factory.LastConfirmationLink);
        Assert.Equal(HttpStatusCode.OK, confirmationResponse.StatusCode);

        var loginPage = await _client.GetAsync("/account/login");
        var loginToken = ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());
        var loginResponse = await _client.PostAsync(
            "/account/login",
            Form(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["__RequestVerificationToken"] = loginToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> values) =>
        new(values);

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\\\"__RequestVerificationToken\\\" type=\\\"hidden\\\" value=\\\"([^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("Antiforgery token was not found.");
    }
}

public sealed class WebAuthenticationApiFactory : AuthApiFactory
{
    protected override bool WebAuthenticationRegistrationEnabled => true;
}
