using FluentValidation;

namespace MajdsApp.SharedKernel.Export;

public enum ExportFormat { Csv, Xlsx, Pdf }

/// <summary>A generated export, ready to stream to the client (F-Export).</summary>
public record ExportFile(byte[] Content, string ContentType, string FileName);

public static class ExportFormats
{
    /// <summary>Parses the <c>format</c> query value; empty means CSV. Anything else is a validation error, so a typo
    /// is reported rather than silently producing a different file.</summary>
    public static ExportFormat Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "csv" => ExportFormat.Csv,
        "xlsx" or "excel" => ExportFormat.Xlsx,
        "pdf" => ExportFormat.Pdf,
        _ => throw new ValidationException($"'{value}' is not a supported export format. Use csv, xlsx or pdf.")
    };
}

/// <summary>
/// Renders one dataset to any supported format (FR-EXP-001/002). Handlers describe rows and columns once and
/// choose the format at the last step, so CSV, Excel and PDF always contain exactly the same rows (AC-EXP-1).
/// </summary>
public static class TabularExport
{
    public static ExportFile Render<T>(
        ExportFormat format, string title, string fileBaseName, IEnumerable<T> rows, params (string Header, Func<T, object?> Value)[] columns)
    {
        var headers = columns.Select(c => c.Header).ToList();
        var table = rows.Select(row => (IReadOnlyList<object?>)columns.Select(c => c.Value(row)).ToList()).ToList();

        return format switch
        {
            ExportFormat.Xlsx => new ExportFile(XlsxWriter.Write(title, headers, table),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileBaseName + ".xlsx"),
            ExportFormat.Pdf => new ExportFile(PdfWriter.Write(title, headers, table), "application/pdf", fileBaseName + ".pdf"),
            _ => new ExportFile(CsvWriter.Write(table, headers.Select((h, i) => (h, (Func<IReadOnlyList<object?>, object?>)(r => r[i]))).ToArray()),
                "text/csv", fileBaseName + ".csv")
        };
    }
}
