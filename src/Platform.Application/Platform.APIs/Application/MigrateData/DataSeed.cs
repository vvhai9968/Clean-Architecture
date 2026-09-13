using Microsoft.EntityFrameworkCore;
using Platform.Domain.Platform.Auth;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared.Constants;

namespace Platform.APIs.Application.MigrateData;

public static class DataSeed
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await RoleSeed(serviceProvider);
    }

    private static async Task RoleSeed(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (await db.Roles.AnyAsync())
            return;

        db.Roles.AddRange(
            new Role { Code = AuthIdentityConstants.Admin, Name = "Admin" },
            new Role { Code = AuthIdentityConstants.User, Name = "User" }
        );
        await db.SaveChangesAsync();
    }
}
