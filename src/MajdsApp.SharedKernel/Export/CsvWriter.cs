using System.Globalization;
using System.Text;

namespace MajdsApp.SharedKernel.Export;

/// <summary>
/// Shared CSV export (F-Export) so every list's export behaves identically: RFC 4180 quoting, a UTF-8
/// BOM (so Excel opens Arabic and other non-ASCII text correctly), and protection against CSV/formula
/// injection — a cell starting with = + - @ (or tab/CR) is prefixed with an apostrophe so a spreadsheet
/// never evaluates user-supplied text as a formula.
/// </summary>
public static class CsvWriter
{
    public const int MaxRows = 50_000;

    public static byte[] Write<T>(IEnumerable<T> rows, params (string Header, Func<T, object?> Value)[] columns)
    {
        var sb = new StringBuilder();
        sb.AppendJoin(',', columns.Select(c => Escape(c.Header, true))).Append("\r\n");

        foreach (var row in rows)
            sb.AppendJoin(',', columns.Select(c => { var v = c.Value(row); return Escape(Format(v), v is string); })).Append("\r\n");

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string cell, bool guardFormulas)
    {
        if (guardFormulas && cell.Length > 0 && cell[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            cell = "'" + cell;

        return cell.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
    }
}
