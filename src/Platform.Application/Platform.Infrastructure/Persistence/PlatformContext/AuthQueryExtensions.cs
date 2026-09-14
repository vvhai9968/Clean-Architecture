using Microsoft.EntityFrameworkCore;

namespace Platform.Infrastructure.Persistence.PlatformContext;

/// <summary>
/// Truy vấn dùng lại nhiều nơi trên PlatformDbContext. Nằm cạnh DbContext vì nó là
/// một câu query EF cụ thể, không phải quy tắc nghiệp vụ.
/// </summary>
public static class AuthQueryExtensions
{
    public static Task<List<string>> GetRoleCodesAsync(
        this PlatformDbContext db,
        Guid userId,
        CancellationToken cancellationToken = default)
        => db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Code)
            .ToListAsync(cancellationToken);
}
