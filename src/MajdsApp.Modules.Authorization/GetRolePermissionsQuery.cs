using MajdsApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Authorization;

public record GetRolePermissionsQuery(string RoleId) : IRequest<IReadOnlyList<string>>;

public class GetRolePermissionsQueryHandler(ApplicationDbContext db) : IRequestHandler<GetRolePermissionsQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetRolePermissionsQuery request, CancellationToken ct) =>
        await db.Set<RolePermission>()
            .Where(rp => rp.RoleId == request.RoleId && rp.IsGranted)
            .Select(rp => rp.PermissionName)
            .ToListAsync(ct);
}
