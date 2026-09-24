namespace MajdsApp.Modules.Audit;

public record AuditLogEntryDto(
    int Id, string Action, string? UserName, DateTime CreatedAt,
    string Outcome, bool Succeeded, int DurationMs, string? ClientIp, string? HttpMethod, string? Url);

public record AuditPropertyChangeDto(string Property, string? Original, string? New);
public record AuditEntityChangeDto(string EntityType, string EntityId, string ChangeType, IReadOnlyList<AuditPropertyChangeDto> Properties);

public record AuditLogEntryDetailDto(
    int Id, string Action, string? UserId, string? UserName, DateTime CreatedAt,
    string Outcome, bool Succeeded, int DurationMs, string? ClientIp, string? ClientBrowser,
    string? HttpMethod, string? Url, string? Error, string? Parameters,
    IReadOnlyList<AuditEntityChangeDto> Changes);

/// <summary>Filters the audit viewer and export share (FR-AUDIT-003): free text, date range (UTC, inclusive of the end day),
/// and outcome — <c>Success</c>, <c>Failure</c> (anything that did not succeed), or one exact outcome such as <c>Forbidden</c>.</summary>
public record AuditLogFilter(string? Text, DateTime? From, DateTime? To, string? Outcome);
