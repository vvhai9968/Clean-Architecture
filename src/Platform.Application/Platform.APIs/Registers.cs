using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Platform.Auth;
using Platform.Domain.Platform.Auth.Abstractions;
using Platform.Infrastructure.Identity;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Infrastructure.Tvan;
using Platform.Shared;

namespace Platform.APIs;

public static class Registers
{
    public static void AddService(this IServiceCollection services)
    {
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        // Token của T-VAN được cache ở đây. Chạy nhiều instance thì đổi sang
        // AddStackExchangeRedisCache để cả cụm dùng chung một token, tránh login lặp.
        services.AddDistributedMemoryCache();

        services.AddTvanIntegration();
        services.AddTvanOutboxDispatcher();
    }

    public static void AddDbContext(this IServiceCollection services, AppSettings appSettings)
    {
        services.AddDbContext<PlatformDbContext>(options =>
        {
            var schema = typeof(PlatformDbContext).Assembly.GetName().Name;
            options.UseNpgsql(appSettings.ConnectionStrings.DefaultConnection, npgsql =>
            {
                npgsql.MigrationsAssembly(schema);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory");
            });
        });
    }
}
