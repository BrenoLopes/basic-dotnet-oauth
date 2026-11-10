using DotNetAuth.Domain.IdentityAccess.Users;

namespace DotNetAuth.Application.IdentityAccess;

public sealed class ConfirmUserEmailService(
    IUserEmailConfirmationService emailConfirmationService)
{
    public async Task<OperationResult> ExecuteAsync(
        string userId,
        string token,
        CancellationToken cancellationToken)
    {
        UserId id;
        try
        {
            id = new UserId(userId);
        }
        catch (ArgumentException exception)
        {
            return OperationResult.Failure(
                new ValidationError("userId", exception.Message));
        }

        return await emailConfirmationService.ConfirmAsync(
            id,
            token,
            cancellationToken);
    }
}
