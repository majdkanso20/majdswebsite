using MajdsApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Tests.Support;

/// <summary>Reads what is actually stored, bypassing the API, to prove things like encryption at rest.</summary>
public sealed class SqlProbe(ApiFactory factory) : IAsyncDisposable
{
    private readonly IServiceScope _scope = factory.Services.CreateScope();

    public async Task<string?> StoredValueAsync(string settingName)
    {
        var db = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.Database
            .SqlQueryRaw<string>("SELECT \"Value\" AS \"Value\" FROM \"SettingValues\" WHERE \"Id\" = {0}", settingName)
            .ToListAsync();
        return rows.SingleOrDefault();
    }

    public ValueTask DisposeAsync()
    {
        _scope.Dispose();
        return ValueTask.CompletedTask;
    }
}
