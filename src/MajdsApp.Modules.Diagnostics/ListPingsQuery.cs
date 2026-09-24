using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Paging;
using MediatR;

namespace MajdsApp.Modules.Diagnostics;

public record ListPingsQuery(PagedRequest Request) : IRequest<PagedResponse<PingListItem>>;

public record PingListItem(Guid Id, string Message, DateTime CreatedAt);

public class ListPingsQueryHandler(IUnitOfWork unitOfWork) : IRequestHandler<ListPingsQuery, PagedResponse<PingListItem>>
{
    public Task<PagedResponse<PingListItem>> Handle(ListPingsQuery request, CancellationToken ct)
    {
        var query = unitOfWork.ReadRepository<DiagnosticsPing, Guid>().Query();

        var sortableColumns = new Dictionary<string, System.Linq.Expressions.Expression<Func<DiagnosticsPing, object>>>
        {
            ["message"] = p => p.Message,
            ["createdAt"] = p => p.CreatedAt
        };

        return query.ApplyPagingAsync(request.Request, sortableColumns,
            p => new PingListItem(p.Id, p.Message, p.CreatedAt), ct);
    }
}
