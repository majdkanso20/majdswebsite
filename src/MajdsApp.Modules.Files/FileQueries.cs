using MajdsApp.SharedKernel.Search;
using System.Linq.Expressions;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Paging;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Files;

/// <summary>Lists every file for users holding Files.View, otherwise only the caller's own. Access is
/// decided in the handler (not an attribute) because the permission changes the result set, not access.</summary>
[RequiresFeature("Files")]
public record ListFilesQuery(PagedRequest Request) : IRequest<PagedResponse<FileDto>>;

public class ListFilesQueryHandler(ApplicationDbContext db, ICurrentUser currentUser, IPermissionChecker permissions)
    : IRequestHandler<ListFilesQuery, PagedResponse<FileDto>>
{
    public async Task<PagedResponse<FileDto>> Handle(ListFilesQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var query = db.Set<FileRecord>().AsNoTracking();

        if (!await permissions.HasPermissionAsync(Permissions.Files.View, ct))
            query = query.Where(f => f.OwnerId == userId);

        if (!string.IsNullOrWhiteSpace(request.Request.Filter))
        {
            var pattern = LikePattern.Contains(request.Request.Filter);
            query = query.Where(f => EF.Functions.Like(f.FileName, pattern, LikePattern.Escape));
        }

        var sortable = new Dictionary<string, Expression<Func<FileRecord, object>>>
        {
            ["fileName"] = f => f.FileName,
            ["size"] = f => f.Size,
            ["createdAt"] = f => f.CreatedAt
        };

        return await query.ApplyPagingAsync(request.Request, sortable,
            f => new FileDto(f.Id, f.FileName, f.ContentType, f.Size, f.OwnerName, f.CreatedAt), ct);
    }
}

public record DownloadedFile(string FileName, string ContentType, Stream Content);

[RequiresFeature("Files")]
public record DownloadFileQuery(Guid FileId) : IRequest<DownloadedFile>;

public class DownloadFileQueryHandler(
    ApplicationDbContext db, FileStorage storage, ICurrentUser currentUser, IPermissionChecker permissions)
    : IRequestHandler<DownloadFileQuery, DownloadedFile>
{
    public async Task<DownloadedFile> Handle(DownloadFileQuery request, CancellationToken ct)
    {
        var record = await db.Set<FileRecord>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == request.FileId, ct)
            ?? throw new NotFoundException("File not found.");

        var isOwner = record.OwnerId is not null && record.OwnerId == currentUser.UserId;
        if (!isOwner && !await permissions.HasPermissionAsync(Permissions.Files.View, ct))
            throw new ForbiddenException($"Missing permission '{Permissions.Files.View}'.");

        return new DownloadedFile(record.FileName, record.ContentType, storage.Open(record.StoredName));
    }
}
