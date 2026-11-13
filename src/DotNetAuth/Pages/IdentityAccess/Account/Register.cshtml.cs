using System.ComponentModel.DataAnnotations;
using DotNetAuth.Application.IdentityAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace DotNetAuth.Pages.IdentityAccess.Account;

public sealed class RegisterModel(
    RegisterUserService registerUser,
    IConfiguration configuration) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? SuccessMessage { get; private set; }

    public string? LocalReturnUrl =>
        Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;

    public IActionResult OnGet()
    {
        if (!IsRegistrationEnabled)
        {
            return NotFound();
        }

        NormalizeReturnUrl();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!IsRegistrationEnabled)
        {
            return NotFound();
        }

        NormalizeReturnUrl();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await registerUser.ExecuteAsync(
            new RegisterUserCommand(
                Input.Email,
                Input.Password,
                Input.DisplayName),
            cancellationToken);

        if (!result.Succeeded || result.User is null)
        {
            if (result.ErrorCode == "confirmation_link_unavailable")
            {
                ModelState.AddModelError(string.Empty, "Não foi possível criar o link de confirmação.");
            }

            foreach (var error in result.Errors)
            {
                AddRegistrationError(error);
            }

            return Page();
        }

        Input = new RegisterInput();
        ModelState.Clear();
        SuccessMessage = "Cadastro criado. Confirme o e-mail antes de fazer login.";
        return Page();
    }

    private bool IsRegistrationEnabled =>
        configuration.GetValue<bool>("Web:Authentication:Registration");

    private void AddRegistrationError(ValidationError error)
    {
        switch (error.Code)
        {
            case "DuplicateUserName":
            case "DuplicateEmail":
                ModelState.AddModelError(
                    "Input.Email",
                    "Este e-mail já está cadastrado.");
                break;
            case "PasswordTooShort":
                ModelState.AddModelError(
                    "Input.Password",
                    "A senha deve ter pelo menos 8 caracteres.");
                break;
            case "PasswordRequiresUpper":
                ModelState.AddModelError(
                    "Input.Password",
                    "A senha deve conter uma letra maiúscula.");
                break;
            case "PasswordRequiresLower":
                ModelState.AddModelError(
                    "Input.Password",
                    "A senha deve conter uma letra minúscula.");
                break;
            default:
                ModelState.AddModelError(string.Empty, error.Description);
                break;
        }
    }

    private void NormalizeReturnUrl()
    {
        ReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;
    }

    public sealed class RegisterInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8), RegularExpression(
            @"^(?=.*\p{Lu})(?=.*\p{Ll}).+$",
            ErrorMessage = "A senha deve conter pelo menos uma letra maiúscula e uma letra minúscula.")]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
