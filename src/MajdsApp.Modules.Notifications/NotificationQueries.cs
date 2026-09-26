using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Mapping;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Paging;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

public record NotificationDto(int Id, string Type, string Title, string Message, bool IsRead, DateTime CreatedAt, string? Link = null);

// Notifications are always the caller's own, so these carry no [RequiresPermission]; scoping by
// ICurrentUser is the access control.
[RequiresFeature("Notifications")]
public record ListMyNotificationsQuery(PagedRequest Request) : IRequest<PagedResponse<NotificationDto>>;

public class ListMyNotificationsQueryHandler(IReadRepository<Notification, int> notifications, ICurrentUser currentUser, IObjectMapper mapper)
    : IRequestHandler<ListMyNotificationsQuery, PagedResponse<NotificationDto>>
{
    public async Task<PagedResponse<NotificationDto>> Handle(ListMyNotificationsQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var query = notifications.Query().Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Request.Page - 1) * request.Request.PageSize).Take(request.Request.PageSize)
            .Select(mapper.Projection<Notification, NotificationDto>()).ToListAsync(ct);

        return new PagedResponse<NotificationDto>
            { Items = items, TotalCount = total, Page = request.Request.Page, PageSize = request.Request.PageSize };
    }
}

[RequiresFeature("Notifications")]
public record GetUnreadCountQuery : IRequest<int>;

public class GetUnreadCountQueryHandler(ApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetUnreadCountQuery, int>
{
    public Task<int> Handle(GetUnreadCountQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        return db.Set<Notification>().CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }
}
