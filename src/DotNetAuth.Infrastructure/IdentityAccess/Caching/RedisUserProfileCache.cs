using System.Text.Json;
using DotNetAuth.Application.IdentityAccess;
using Microsoft.Extensions.Caching.Distributed;

namespace DotNetAuth.Infrastructure.IdentityAccess.Caching;

public sealed class RedisUserProfileCache(IDistributedCache cache) : IUserProfileCache
{
    public async Task<UserProfile?> GetAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetStringAsync(CacheKey(subject), cancellationToken);
        return cached is null
            ? null
            : JsonSerializer.Deserialize<UserProfile>(cached);
    }

    public Task SetAsync(
        UserProfile profile,
        TimeSpan expiration,
        CancellationToken cancellationToken) =>
        cache.SetStringAsync(
            CacheKey(profile.Subject),
            JsonSerializer.Serialize(profile),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            },
            cancellationToken);

    private static string CacheKey(string subject) => $"profile:{subject}";
}
