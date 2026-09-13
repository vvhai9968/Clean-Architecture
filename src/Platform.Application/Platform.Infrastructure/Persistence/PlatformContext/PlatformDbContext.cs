using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Platform.Auth;
using Platform.Shared.Common;

namespace Platform.Infrastructure.Persistence.PlatformContext;

public class PlatformDbContext(DbContextOptions<PlatformDbContext> options, IHttpContextAccessor httpContextAccessor)
    : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
            entity.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<UserIdentity>(entity =>
        {
            entity.ToTable("UserIdentities");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        });

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries();
        var userId = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

        foreach (var entry in entries)
        {
            if (entry.Entity is not BaseEntity entity) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    if (userId != null) entity.CreatedBy = Guid.Parse(userId);
                    entity.CreatedAt = DateTime.UtcNow;
                    entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    if (userId != null) entity.UpdatedBy = Guid.Parse(userId);
                    entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
