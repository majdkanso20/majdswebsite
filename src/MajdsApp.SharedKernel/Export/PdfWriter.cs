using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MajdsApp.SharedKernel.Export;

/// <summary>
/// Server-generated PDF for report-style output (FR-EXP-002): a titled, paginated table on landscape A4.
/// A PDF is for reading, not for bulk data, so it is capped at <see cref="MaxRows"/> rows and says so on the page.
/// </summary>
public static class PdfWriter
{
    public const int MaxRows = 2_000;

    static PdfWriter()
    {
        // QuestPDF's Community license is free for organizations under 1M USD annual revenue; larger organizations need a paid license.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Write(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var shown = rows.Take(MaxRows).ToList();
        var generated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(text => text.FontSize(8));

            page.Header().Column(column =>
            {
                column.Item().Text(title).FontSize(16).Bold();
                var note = rows.Count > shown.Count
                    ? $"Generated {generated} · showing the first {shown.Count:N0} of {rows.Count:N0} rows"
                    : $"Generated {generated} · {rows.Count:N0} rows";
                column.Item().PaddingBottom(8).Text(note).FontColor(Colors.Grey.Darken1);
            });

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns => { foreach (var _ in headers) columns.RelativeColumn(); });

                table.Header(header =>
                {
                    foreach (var text in headers)
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).Bold();
                });

                foreach (var row in shown)
                    foreach (var value in row)
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Format(value));
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? "Yes" : "No",
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
