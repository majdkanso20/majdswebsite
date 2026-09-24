namespace MajdsApp.SharedKernel.Settings;

/// <summary>
/// Resolves the effective value of a setting (an admin override if one exists, else the code-defined
/// default). The Shared Kernel ships a default-only implementation so other modules can depend on this
/// before F-Settings exists; the F-Settings module replaces this registration with the real
/// database-backed one (same pattern as <see cref="Security.IPermissionChecker"/>).
/// </summary>
public interface ISettingsProvider
{
    Task<string> GetAsync(string name, CancellationToken ct = default);
    Task<bool> GetBooleanAsync(string name, CancellationToken ct = default);
    Task<int> GetIntegerAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Called by F-Settings after an update so subsequent reads see the new value (AC pattern
    /// shared with <see cref="Security.IPermissionChecker"/>'s cache invalidation).</summary>
    void Invalidate();

    /// <summary>Effective values for one user: User override (where the definition allows it) > Application
    /// override > code default (FR-SET-002). A null user id yields the application-scope values.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllForUserAsync(string? userId, CancellationToken ct = default);

    /// <summary>Drops the cached overrides of one user after they change their own settings.</summary>
    void InvalidateUser(string userId);
}

public class DefaultSettingsProvider : ISettingsProvider
{
    public Task<string> GetAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(SettingDefinitionRegistry.Find(name)?.DefaultValue ?? string.Empty);

    public async Task<bool> GetBooleanAsync(string name, CancellationToken ct = default) =>
        bool.TryParse(await GetAsync(name, ct), out var value) && value;

    public async Task<int> GetIntegerAsync(string name, CancellationToken ct = default) =>
        int.TryParse(await GetAsync(name, ct), out var value) ? value : 0;

    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(
            SettingDefinitionRegistry.GetAll().ToDictionary(d => d.Name, d => d.DefaultValue));

    public void Invalidate() { }

    public Task<IReadOnlyDictionary<string, string>> GetAllForUserAsync(string? userId, CancellationToken ct = default) => GetAllAsync(ct);

    public void InvalidateUser(string userId) { }
}
