using System.Text;
using ClosedXML.Excel;
using MajdsApp.SharedKernel.Export;

namespace MajdsApp.SharedKernel.Import;

public record ImportColumn(string Name, bool Required, string Description);

/// <summary>
/// The downloadable template for an import (FR-EXP-005): the exact column headers the importer expects. The Excel
/// version adds an Instructions sheet saying which columns are required. The data sheet holds only headers, so
/// nothing in the template is imported by accident.
/// </summary>
public static class ImportTemplate
{
    public static ExportFile Create(ExportFormat format, string baseName, IReadOnlyList<ImportColumn> columns)
    {
        if (format == ExportFormat.Pdf)
            throw new FluentValidation.ValidationException("A template is a .csv or .xlsx file.");

        if (format == ExportFormat.Xlsx)
            return new ExportFile(Xlsx(columns), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", baseName + "-import-template.xlsx");

        var csv = string.Join(',', columns.Select(c => c.Name)) + "\r\n";
        return new ExportFile([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)], "text/csv", baseName + "-import-template.csv");
    }

    private static byte[] Xlsx(IReadOnlyList<ImportColumn> columns)
    {
        using var workbook = new XLWorkbook();
        var data = workbook.Worksheets.Add("Data");
        for (var i = 0; i < columns.Count; i++)
        {
            var cell = data.Cell(1, i + 1);
            cell.Value = columns[i].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EAF0");
        }
        data.Columns().AdjustToContents(1, 1, 14, 40);
        data.SheetView.FreezeRows(1);

        var help = workbook.Worksheets.Add("Instructions");
        help.Cell(1, 1).Value = "Column";
        help.Cell(1, 2).Value = "Required";
        help.Cell(1, 3).Value = "What to enter";
        help.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < columns.Count; i++)
        {
            help.Cell(i + 2, 1).Value = columns[i].Name;
            help.Cell(i + 2, 2).Value = columns[i].Required ? "Yes" : "No";
            help.Cell(i + 2, 3).Value = columns[i].Description;
        }
        help.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
