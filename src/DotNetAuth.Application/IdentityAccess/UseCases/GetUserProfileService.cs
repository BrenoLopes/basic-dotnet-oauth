namespace DotNetAuth.Application.IdentityAccess;

public sealed class GetUserProfileService(IUserProfileCache cache)
{
    public async Task<UserProfile> ExecuteAsync(
        AuthenticatedIdentity identity,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync(identity.Subject, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var profile = new UserProfile(
            identity.Subject,
            identity.Email,
            identity.Name);

        await cache.SetAsync(
            profile,
            TimeSpan.FromMinutes(5),
            cancellationToken);

        return profile;
    }
}
