using ClosedXML.Excel;

namespace MajdsApp.SharedKernel.Export;

/// <summary>Excel (.xlsx) export. Text is always written as text, never parsed as a formula, so user-supplied
/// values such as "=HYPERLINK(...)" cannot execute when the file is opened (same protection as the CSV writer).</summary>
public static class XlsxWriter
{
    public static byte[] Write(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName(title));

        for (var c = 0; c < headers.Count; c++)
        {
            var header = sheet.Cell(1, c + 1);
            header.Value = headers[c];
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EAF0");
        }

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < headers.Count; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                switch (rows[r][c])
                {
                    case null: break;
                    case DateTime d: cell.Value = d; cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss"; break;
                    case bool b: cell.Value = b ? "Yes" : "No"; break;
                    case int or long or short or decimal or double or float: cell.Value = Convert.ToDouble(rows[r][c]); break;
                    case var other: cell.SetValue(other.ToString() ?? string.Empty); break; // SetValue(string) is literal text
                }
            }
        }

        sheet.SheetView.FreezeRows(1);
        if (rows.Count > 0) sheet.Range(1, 1, rows.Count + 1, headers.Count).SetAutoFilter();
        sheet.Columns().AdjustToContents(1, Math.Min(rows.Count + 1, 200), 8, 60);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Sheet names are limited to 31 characters and cannot contain : \ / ? * [ ].</summary>
    private static string SheetName(string title)
    {
        var cleaned = new string(title.Where(ch => !":\\/?*[]".Contains(ch)).ToArray()).Trim();
        return cleaned.Length == 0 ? "Export" : cleaned[..Math.Min(31, cleaned.Length)];
    }
}
