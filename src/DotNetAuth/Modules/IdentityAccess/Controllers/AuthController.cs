using System.Text;
using DotNetAuth.Application.IdentityAccess;
using DotNetAuth.Modules.IdentityAccess.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace DotNetAuth.Modules.IdentityAccess.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    RegisterUserService registerUser,
    ConfirmUserEmailService confirmUserEmail,
    LoginUserService loginUser,
    LogoutUserService logoutUser,
    GetCurrentUserService getCurrentUser) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registerUser.ExecuteAsync(
            new RegisterUserCommand(
                request.Email,
                request.Password,
                request.DisplayName),
            cancellationToken);

        if (!result.Succeeded || result.User is null)
        {
            if (result.ErrorCode == "confirmation_link_unavailable")
            {
                return Problem("Não foi possível criar o link de confirmação.");
            }

            var errors = result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray());
            return BadRequest(new ValidationProblemDetails(errors));
        }

        return Created(string.Empty, new
        {
            Id = result.User.Id.Value,
            Email = result.User.Email.Value,
            Message = "Cadastro criado. Confirme o e-mail antes de fazer login."
        });
    }

    [HttpGet("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(
        string userId,
        string token,
        CancellationToken cancellationToken)
    {
        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return BadRequest(new { Error = "invalid_confirmation" });
        }

        var result = await confirmUserEmail.ExecuteAsync(
            userId,
            decodedToken,
            cancellationToken);

        return result.Succeeded
            ? Ok(new { Message = "E-mail confirmado." })
            : BadRequest(new { Error = "invalid_confirmation" });
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await loginUser.ExecuteAsync(
            new LoginUserCommand(
                request.Email,
                request.Password,
                request.RememberMe),
            cancellationToken);

        if (result.Succeeded)
        {
            return Ok(new { Message = "Login realizado." });
        }

        if (result.IsLockedOut)
        {
            return Problem(
                "Usuário temporariamente bloqueado.",
                statusCode: StatusCodes.Status423Locked);
        }

        if (result.IsNotAllowed)
        {
            return Unauthorized(new { Error = "email_not_confirmed" });
        }

        return Unauthorized(new { Error = "invalid_credentials" });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await logoutUser.ExecuteAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var authentication = await HttpContext.AuthenticateAsync(
            IdentityConstants.ApplicationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Unauthorized();
        }

        var user = await getCurrentUser.ExecuteAsync(
            authentication.Principal,
            cancellationToken);

        return user is null
            ? Unauthorized()
            : Ok(new
            {
                Id = user.Id.Value,
                Email = user.Email.Value,
                user.DisplayName
            });
    }
}

