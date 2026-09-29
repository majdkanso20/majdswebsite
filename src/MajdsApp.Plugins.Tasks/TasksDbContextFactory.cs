using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MajdsApp.Plugins.Tasks;

/// <summary>
/// Design-time only: lets <c>dotnet ef migrations add</c> create a <see cref="TasksDbContext"/> without a host
/// to resolve one from (this project has none — it is loaded into the API's plugins folder at runtime, never
/// referenced or run on its own). The connection string here is never used to run anything; only the model
/// matters for generating a migration. Run migrations for this context from this project itself:
/// <c>dotnet ef migrations add &lt;Name&gt; --project src/MajdsApp.Plugins.Tasks --startup-project src/MajdsApp.Plugins.Tasks --context TasksDbContext</c>
/// </summary>
public class TasksDbContextFactory : IDesignTimeDbContextFactory<TasksDbContext>
{
    public TasksDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TasksDbContext>().UseSqlite("Data Source=design-time-only.db").Options);
}
