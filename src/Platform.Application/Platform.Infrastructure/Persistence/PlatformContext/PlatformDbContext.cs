using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Platform.Auth;
using Platform.Domain.Platform.Tvan;
using Platform.Shared.Common;

namespace Platform.Infrastructure.Persistence.PlatformContext;

public class PlatformDbContext(DbContextOptions<PlatformDbContext> options, IHttpContextAccessor httpContextAccessor)
    : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserTvanProvider> UserTvanProviders => Set<UserTvanProvider>();

    public DbSet<TvanProvider> TvanProviders => Set<TvanProvider>();
    public DbSet<TvanEndpoint> TvanEndpoints => Set<TvanEndpoint>();
    public DbSet<TvanHeaderTemplate> TvanHeaderTemplates => Set<TvanHeaderTemplate>();
    public DbSet<TvanCredential> TvanCredentials => Set<TvanCredential>();
    public DbSet<TvanTenantBinding> TvanTenantBindings => Set<TvanTenantBinding>();
    public DbSet<TvanTransaction> TvanTransactions => Set<TvanTransaction>();
    public DbSet<TvanTransactionAttempt> TvanTransactionAttempts => Set<TvanTransactionAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.UserName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TaxCode).HasMaxLength(20);
        });

        modelBuilder.Entity<UserTvanProvider>(entity =>
        {
            entity.ToTable("UserTvanProviders");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.ProviderCode }).IsUnique();
            // Thứ tự ưu tiên là dữ liệu nghiệp vụ, không phải thứ tự chèn — index theo nó.
            entity.HasIndex(x => new { x.UserId, x.Priority });
            entity.HasOne<User>().WithMany(x => x.TvanProviders)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.ProviderCode).HasMaxLength(32).IsRequired();
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

        ConfigureTvan(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Cấu hình T-VAN. Mọi cột *Json dùng kiểu jsonb của Postgres để vẫn query/index được
    /// bằng SQL khi cần rà soát cấu hình, thay vì chôn cấu hình trong text mù.
    /// </summary>
    private static void ConfigureTvan(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TvanProvider>(entity =>
        {
            entity.ToTable("TvanProviders");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Code, x.Environment }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Environment).HasMaxLength(16).IsRequired();
            entity.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
            entity.Property(x => x.AuthSchemeKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.AuthConfigJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CallbackMapJson).HasColumnType("jsonb");
            entity.Property(x => x.ResilienceConfigJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<TvanEndpoint>(entity =>
        {
            entity.ToTable("TvanEndpoints");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProviderId, x.OperationCode }).IsUnique();
            entity.HasOne(x => x.Provider).WithMany(x => x.Endpoints)
                .HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.OperationCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            entity.Property(x => x.PathTemplate).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SoapAction).HasMaxLength(500);
            entity.Property(x => x.RequestTransformsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ResponseMapJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ArgsJson).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<TvanHeaderTemplate>(entity =>
        {
            entity.ToTable("TvanHeaderTemplates");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProviderId, x.EndpointId, x.Name }).IsUnique();
            entity.HasOne<TvanProvider>().WithMany(x => x.Headers)
                .HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ValueTemplate).HasMaxLength(2000).IsRequired();
        });

        modelBuilder.Entity<TvanCredential>(entity =>
        {
            entity.ToTable("TvanCredentials");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProviderId, x.TaxCode }).IsUnique();
            entity.HasOne<TvanProvider>().WithMany()
                .HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.TaxCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ProtectedSecretsJson).IsRequired();
        });

        modelBuilder.Entity<TvanTenantBinding>(entity =>
        {
            entity.ToTable("TvanTenantBindings");
            entity.HasKey(x => x.Id);
            // Một MST có nhiều nhà truyền nhận: unique theo cặp, thứ tự nằm ở Priority.
            entity.HasIndex(x => new { x.TaxCode, x.ProviderId }).IsUnique();
            entity.HasIndex(x => new { x.TaxCode, x.Priority });
            entity.Property(x => x.TaxCode).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<TvanTransaction>(entity =>
        {
            entity.ToTable("TvanTransactions");
            entity.HasKey(x => x.Id);
            // Chốt idempotency ở tầng DB: gửi trùng bị chặn kể cả khi có race giữa nhiều instance.
            // Khoá là CorrelationKey đơn lẻ chứ không kèm nhà truyền nhận — một thông điệp
            // nghiệp vụ có thể đã chuyển qua nhiều nhà khi failover nhưng vẫn là một giao dịch.
            entity.HasIndex(x => x.CorrelationKey).IsUnique();
            entity.Property(x => x.ProviderPlan).HasMaxLength(500).IsRequired();
            entity.Property(x => x.RouteSource).HasMaxLength(32);
            // Index phục vụ vòng quét của outbox dispatcher.
            entity.HasIndex(x => new { x.State, x.NextAttemptAt });
            entity.Property(x => x.ProviderCode).HasMaxLength(32).IsRequired();
            entity.Property(x => x.TaxCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.OperationCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CorrelationKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.State).HasConversion<int>();
            entity.Property(x => x.FieldsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ProviderReference).HasMaxLength(100);
            entity.Property(x => x.ResultCode).HasMaxLength(50);
            entity.Property(x => x.ResultMessage).HasMaxLength(2000);
            entity.Property(x => x.LockedBy).HasMaxLength(200);
        });

        modelBuilder.Entity<TvanTransactionAttempt>(entity =>
        {
            entity.ToTable("TvanTransactionAttempts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TransactionId);
            entity.HasOne<TvanTransaction>().WithMany(x => x.Attempts)
                .HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.RequestUri).HasMaxLength(2000);
            entity.Property(x => x.ProviderCode).HasMaxLength(32);
        });
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
