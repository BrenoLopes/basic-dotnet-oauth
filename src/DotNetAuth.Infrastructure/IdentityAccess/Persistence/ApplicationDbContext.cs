using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DotNetAuth.Infrastructure.IdentityAccess.Identity;

namespace DotNetAuth.Infrastructure.IdentityAccess.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();

        builder.Entity(
            "OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization",
            entity =>
            {
                entity.Property<string>("Subject").HasMaxLength(255);
                entity.Property<string>("Type").HasMaxLength(100);
            });

        builder.Entity(
            "OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken",
            entity =>
            {
                entity.Property<string>("Subject").HasMaxLength(255);
                entity.Property<string>("Type").HasMaxLength(100);
            });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName)
                .HasMaxLength(120)
                .IsRequired();

            entity.HasIndex(user => user.Email)
                .IsUnique();
        });
    }
}
