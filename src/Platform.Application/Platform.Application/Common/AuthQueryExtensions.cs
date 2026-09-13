using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence.PlatformContext;

namespace Platform.Application.Common;

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
