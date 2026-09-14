using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.MediatR.Auth.Commands;
using Platform.Domain.Platform.Auth;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared;
using Platform.Shared.Constants;

namespace Platform.APIs.Application.MigrateData;

public static class DataSeed
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        await RoleSeed(serviceProvider);
        await BootstrapAdminSeed(serviceProvider);
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

    /// <summary>
    /// Chỉ quản trị viên được tạo tài khoản, nên trên một database trống sẽ không ai
    /// đăng nhập được. Hàm này sinh đúng một tài khoản quản trị mồi, và chỉ khi
    /// bảng Users hoàn toàn rỗng — chạy lại trên hệ thống đang vận hành là no-op.
    /// </summary>
    private static async Task BootstrapAdminSeed(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var appSettings = scope.ServiceProvider.GetRequiredService<AppSettings>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DataSeed));

        if (await db.Users.AnyAsync())
            return;

        var bootstrap = appSettings.Bootstrap;

        if (string.IsNullOrWhiteSpace(bootstrap.AdminUserName) || string.IsNullOrWhiteSpace(bootstrap.AdminPassword))
        {
            logger.LogWarning(
                "Database chưa có tài khoản nào và Bootstrap:AdminUserName/AdminPassword đang trống. " +
                "Khai báo hai giá trị này rồi khởi động lại, nếu không sẽ không đăng nhập được.");
            return;
        }

        // Đi qua đúng use case tạo tài khoản thay vì viết lại logic ở đây — tài khoản mồi
        // được kiểm tra và ghi y hệt một tài khoản do quản trị viên tạo.
        await sender.Send(new CreateUserCommand
        {
            UserName = bootstrap.AdminUserName,
            Email = bootstrap.AdminEmail,
            Password = bootstrap.AdminPassword,
            PasswordConfirm = bootstrap.AdminPassword,
            DisplayName = bootstrap.AdminDisplayName,
            RoleCode = AuthIdentityConstants.Admin,
        });

        logger.LogWarning(
            "Đã tạo tài khoản quản trị mồi {UserName}. Hãy đổi mật khẩu và xoá Bootstrap khỏi cấu hình.",
            bootstrap.AdminUserName);
    }
}
