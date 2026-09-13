using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Http;
using Platform.Application.Services.Auth;
using Platform.Domain.Platform.Auth;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared;

namespace Platform.APIs;

public static class Registers
{
    public static void AddService(this IServiceCollection services)
    {
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IRestHttpClient, RestHttpClient>();
        services.AddScoped<IAuthService, AuthService>();
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
