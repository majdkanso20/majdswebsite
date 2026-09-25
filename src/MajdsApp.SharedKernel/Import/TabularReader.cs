using System.Text;
using ClosedXML.Excel;
using FluentValidation;

namespace MajdsApp.SharedKernel.Import;

/// <summary>One data row of an uploaded file. <see cref="Number"/> is the row number as the user sees it in a
/// spreadsheet (the header is row 1), so error messages point at the right line.</summary>
public record ImportRow(int Number, IReadOnlyDictionary<string, string> Values)
{
    public string Get(string column) => Values.TryGetValue(column, out var value) ? value : string.Empty;
}

public record ImportTable(IReadOnlyList<string> Headers, IReadOnlyList<ImportRow> Rows);

/// <summary>
/// Reads an uploaded CSV or Excel (.xlsx) file into rows keyed by column header (F-Export FR-EXP-003). Header
/// matching ignores case and surrounding spaces, and column order does not matter. Rows that are entirely empty
/// are skipped. A file that cannot be read, is too large, or lacks a required column is rejected up front with a
/// message the user can act on, before any row is imported.
/// </summary>
public static class TabularReader
{
    public const int MaxRows = 5_000;
    public const long MaxBytes = 5 * 1024 * 1024;

    public static ImportTable Read(Stream content, string fileName, IReadOnlyCollection<string> requiredColumns)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var grid = extension switch
        {
            ".csv" => ReadCsv(content),
            ".xlsx" => ReadXlsx(content),
            _ => throw new ValidationException("Upload a .csv or .xlsx file.")
        };

        if (grid.Count == 0)
            throw new ValidationException("The file is empty.");

        var headers = grid[0].Select(h => h.Trim()).ToList();
        var missing = requiredColumns.Where(required => !headers.Contains(required, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing);
            throw new ValidationException($"The file is missing the required column(s): {names}.");
        }

        var rows = new List<ImportRow>();
        for (var i = 1; i < grid.Count; i++)
        {
            var cells = grid[i];
            if (cells.All(string.IsNullOrWhiteSpace)) continue;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
                if (headers[c].Length > 0) values[headers[c]] = c < cells.Count ? cells[c].Trim() : string.Empty;

            rows.Add(new ImportRow(i + 1, values));
        }

        if (rows.Count > MaxRows)
            throw new ValidationException($"The file has {rows.Count:N0} rows; the limit is {MaxRows:N0}. Split it into smaller files.");

        return new ImportTable(headers, rows);
    }

    private static List<List<string>> ReadXlsx(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new ValidationException("The workbook has no sheets.");
            var used = sheet.RangeUsed();
            if (used is null) return [];

            return sheet.Rows(used.FirstRow().RowNumber(), used.LastRow().RowNumber())
                .Select(row => Enumerable.Range(1, used.ColumnCount()).Select(c => row.Cell(c).GetFormattedString()).ToList())
                .ToList();
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            throw new ValidationException("The Excel file could not be read. Save it as .xlsx and try again.");
        }
    }

    /// <summary>RFC 4180: quoted fields, doubled quotes, and line breaks inside quotes. Tolerates a UTF-8 BOM and , or ; separators.</summary>
    private static List<List<string>> ReadCsv(Stream content)
    {
        using var reader = new StreamReader(content, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var separator = DetectSeparator(text);

        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void EndField() { row.Add(field.ToString()); field.Clear(); }
        void EndRow() { EndField(); rows.Add(row); row = []; }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') inQuotes = false;
                else field.Append(ch);
            }
            else if (ch == '"' && field.Length == 0) inQuotes = true;
            else if (ch == separator) EndField();
            else if (ch == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') i++; EndRow(); }
            else if (ch == '\n') EndRow();
            else field.Append(ch);
        }

        if (field.Length > 0 || row.Count > 0) EndRow();
        return rows;
    }

    private static char DetectSeparator(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        return firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';
    }
}
