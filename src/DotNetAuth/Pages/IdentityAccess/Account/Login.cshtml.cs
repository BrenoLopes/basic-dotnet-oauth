using System.ComponentModel.DataAnnotations;
using DotNetAuth.Application.IdentityAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DotNetAuth.Pages.IdentityAccess.Account;

public sealed class LoginModel(LoginUserService loginUser) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? LocalReturnUrl =>
        Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;

    public bool IsOAuthContinuation =>
        LocalReturnUrl?.StartsWith(
            "/connect/authorize",
            StringComparison.OrdinalIgnoreCase) == true;

    public void OnGet()
    {
        NormalizeReturnUrl();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        NormalizeReturnUrl();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await loginUser.ExecuteAsync(
            new LoginUserCommand(
                Input.Email,
                Input.Password,
                Input.RememberMe),
            cancellationToken);

        if (result.Succeeded)
        {
            var target = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
            return LocalRedirect(target!);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Usuário temporariamente bloqueado.");
        }
        else if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Confirme o e-mail antes de fazer login.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
        }

        return Page();
    }

    private void NormalizeReturnUrl()
    {
        ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;
    }

    public sealed class LoginInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
