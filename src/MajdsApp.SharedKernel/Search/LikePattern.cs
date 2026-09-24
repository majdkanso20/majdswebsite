namespace MajdsApp.SharedKernel.Search;

/// <summary>Builds a "contains" pattern for <c>EF.Functions.Like(column, pattern, LikePattern.Escape)</c>.
/// LIKE is case-insensitive for ASCII on SQLite/SQL Server, unlike <c>string.Contains</c> which EF
/// translates to a case-sensitive <c>instr</c> on SQLite; user-typed % and _ are escaped so they're literal.</summary>
public static class LikePattern
{
    public const string Escape = "\\";

    public static string Contains(string term) =>
        "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
