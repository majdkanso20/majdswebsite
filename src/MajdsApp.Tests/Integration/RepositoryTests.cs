using FluentAssertions;
using FluentValidation;
using MajdsApp.Data;
using MajdsApp.Modules.Diagnostics;
using MajdsApp.Modules.Files;
using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Paging;
using MajdsApp.Tests.Support;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>Records the domain events the host publishes, so a test can see exactly what was published and when.</summary>
public class PingEventLog : INotificationHandler<PingRecorded>
{
    public static readonly List<PingRecorded> Seen = [];

    public Task Handle(PingRecorded notification, CancellationToken cancellationToken)
    {
        lock (Seen) Seen.Add(notification);
        return Task.CompletedTask;
    }
}

public class RepositoryFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddTransient<INotificationHandler<PingRecorded>, PingEventLog>());
    }
}

/// <summary>P3 (repository, specifications, unit of work, central soft delete and domain events) and F-Data paging, against the real host and database.</summary>
public class RepositoryTests(RepositoryFactory factory) : IClassFixture<RepositoryFactory>
{
    private static FileRecord NewFile(string name, string owner = "u") =>
        new() { Id = Guid.NewGuid(), FileName = name, ContentType = "text/plain", Size = name.Length, StoredName = "s-" + name, OwnerId = owner, CreatedAt = DateTime.UtcNow };

    private sealed class ByOwner(string owner) : Specification<FileRecord>(f => f.OwnerId == owner);

    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<FileRecord, object>>> Sortable =
        new() { ["fileName"] = f => f.FileName };

    [Fact]
    public async Task The_read_repository_never_tracks_and_the_write_repository_does()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var write = scope.ServiceProvider.GetRequiredService<IRepository<FileRecord, Guid>>();
        var read = scope.ServiceProvider.GetRequiredService<IReadRepository<FileRecord, Guid>>();
        var file = NewFile("tracking.txt");
        await write.AddAsync(file);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await read.GetByIdAsync(file.Id))!.FileName.Should().Be("tracking.txt");
        await read.Query().ToListAsync();
        db.ChangeTracker.Entries().Should().BeEmpty("a read path must never be able to modify what it reads");

        await write.GetByIdAsync(file.Id);
        db.ChangeTracker.Entries<FileRecord>().Should().ContainSingle();
    }

    [Fact]
    public async Task Repositories_in_one_scope_share_one_context_and_a_transaction_can_be_rolled_back()
    {
        using var scope = factory.Services.CreateScope();
        var write = scope.ServiceProvider.GetRequiredService<IRepository<FileRecord, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var read = scope.ServiceProvider.GetRequiredService<IReadRepository<FileRecord, Guid>>();
        var file = NewFile("rolled-back.txt");

        await uow.BeginTransactionAsync();
        await write.AddAsync(file);
        await uow.SaveChangesAsync();
        (await read.Query().AnyAsync(f => f.Id == file.Id)).Should().BeTrue();   // the read repository sees what the write repository saved: one context
        await uow.RollbackAsync();

        (await read.Query().AnyAsync(f => f.Id == file.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task A_specification_selects_and_counts_and_paging_is_tied_to_the_repository()
    {
        using var scope = factory.Services.CreateScope();
        var write = scope.ServiceProvider.GetRequiredService<IRepository<FileRecord, Guid>>();
        var read = scope.ServiceProvider.GetRequiredService<IReadRepository<FileRecord, Guid>>();
        await write.AddRangeAsync(Enumerable.Range(1, 5).Select(i => NewFile($"page-{i}.txt", "paging-owner")));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        (await read.CountAsync(new ByOwner("paging-owner"))).Should().Be(5);
        (await read.ListAsync(new ByOwner("paging-owner"))).Should().HaveCount(5);

        var page = await read.PagedAsync(new PagedRequest { Page = 2, PageSize = 2, Sort = "fileName:desc" }, Sortable, f => f.FileName, new ByOwner("paging-owner"));
        page.TotalCount.Should().Be(5);
        page.Items.Should().Equal("page-3.txt", "page-2.txt");
    }

    [Fact]
    public async Task Sorting_by_a_column_that_is_not_allowed_is_an_error_not_a_silently_unsorted_list()
    {
        using var scope = factory.Services.CreateScope();
        var read = scope.ServiceProvider.GetRequiredService<IReadRepository<FileRecord, Guid>>();

        var act = () => read.PagedAsync(new PagedRequest { Sort = "storedName:asc" }, Sortable, f => f.FileName);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("storedName").And.Contain("fileName");

        var admin = await factory.SignInAsync("repo.admin@example.com", "Admin");
        (await admin.GetAsync<object>("/api/audit/list?page=1&pageSize=5&sort=nonsense:asc")).Status.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Removing_an_entity_that_supports_soft_delete_only_marks_it_however_it_was_removed()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ping = new DiagnosticsPing { Id = Guid.NewGuid(), Message = "soft" };
        db.Add(ping);
        await db.SaveChangesAsync();

        db.Remove(ping);                    // a plain context remove, not the repository
        await db.SaveChangesAsync();

        (await db.Set<DiagnosticsPing>().AnyAsync(p => p.Id == ping.Id)).Should().BeFalse("the filter hides it");
        var row = await db.Set<DiagnosticsPing>().IgnoreQueryFilters().SingleAsync(p => p.Id == ping.Id);
        row.IsDeleted.Should().BeTrue();
        row.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Domain_events_are_published_after_the_save_succeeds_and_only_once()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ping = new DiagnosticsPing { Id = Guid.NewGuid(), Message = "event-ok" };
        ping.RecordedAs("tester");
        db.Add(ping);

        await db.SaveChangesAsync();
        await db.SaveChangesAsync(); // nothing new: the event must not go out again

        lock (PingEventLog.Seen) PingEventLog.Seen.Where(e => e.PingId == ping.Id).Should().ContainSingle().Which.RecordedBy.Should().Be("tester");
        ping.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task A_save_that_fails_publishes_nothing()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ping = new DiagnosticsPing { Id = Guid.NewGuid(), Message = "event-fail" };
        ping.RecordedAs("tester");
        ping.Message = null!;                 // the column is required: the database refuses the row
        db.Add(ping);

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
        lock (PingEventLog.Seen) PingEventLog.Seen.Should().NotContain(e => e.PingId == ping.Id);
    }

    [Fact]
    public void The_largest_page_a_client_can_ask_for_is_capped_and_the_setting_is_checked_at_start()
    {
        new PagedRequest { PageSize = 100000 }.PageSize.Should().Be(PagedRequest.MaxPageSize);

        using var bad = new BadPaging();
        bad.Invoking(f => f.CreateClient()).Should().Throw<Exception>().Which.ToString().Should().Contain("Paging:MaxPageSize");
    }

    private sealed class BadPaging() : ApiFactory(new Dictionary<string, string> { ["Paging:MaxPageSize"] = "0" }, null);
}
