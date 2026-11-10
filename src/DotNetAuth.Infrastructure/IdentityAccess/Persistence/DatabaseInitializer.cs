using DotNetAuth.Infrastructure.IdentityAccess.OAuth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetAuth.Infrastructure.IdentityAccess.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        const int maxAttempts = 10;
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitialization");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await database.Database.MigrateAsync();
                await OAuthSeeder.SeedAsync(scope.ServiceProvider, configuration);
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database initialization attempt {Attempt} of {MaxAttempts} failed.",
                    attempt,
                    maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        using var finalScope = services.CreateScope();
        var finalDatabase = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await finalDatabase.Database.MigrateAsync();
        await OAuthSeeder.SeedAsync(finalScope.ServiceProvider, configuration);
    }
}
