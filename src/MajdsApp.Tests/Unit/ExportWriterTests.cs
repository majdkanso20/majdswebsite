using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using MajdsApp.SharedKernel.Export;
using Xunit;

namespace MajdsApp.Tests.Unit;

public class ExportWriterTests
{
    private static readonly string[] Headers = ["Name", "Score", "When", "Active"];

    private static IReadOnlyList<IReadOnlyList<object?>> Rows(params object?[][] rows) => rows.Select(r => (IReadOnlyList<object?>)r).ToList();

    [Fact]
    public void Excel_keeps_text_that_looks_like_a_formula_as_plain_text()
    {
        var bytes = XlsxWriter.Write("Test", Headers, Rows(["=HYPERLINK(\"http://evil\",\"x\")", 5, null, true]));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var cell = workbook.Worksheet(1).Cell(2, 1);
        cell.HasFormula.Should().BeFalse();
        cell.GetString().Should().Be("=HYPERLINK(\"http://evil\",\"x\")");
    }

    [Fact]
    public void Excel_writes_numbers_dates_and_booleans_with_their_natural_types()
    {
        var when = new DateTime(2026, 9, 24, 8, 30, 0, DateTimeKind.Utc);
        var bytes = XlsxWriter.Write("Test", Headers, Rows(["a", 42, when, true]));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var row = workbook.Worksheet(1).Row(2);
        row.Cell(2).DataType.Should().Be(XLDataType.Number);
        row.Cell(2).GetDouble().Should().Be(42);
        row.Cell(3).DataType.Should().Be(XLDataType.DateTime);
        row.Cell(3).GetDateTime().Should().Be(when);
        row.Cell(4).GetString().Should().Be("Yes");
    }

    [Fact]
    public void Excel_handles_no_rows_and_awkward_sheet_titles()
    {
        var bytes = XlsxWriter.Write("A/B:C*[very long title that exceeds thirty one characters]", Headers, []);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        workbook.Worksheet(1).Name.Length.Should().BeLessThanOrEqualTo(31);
        workbook.Worksheet(1).Row(1).Cell(1).GetString().Should().Be("Name");
    }

    [Fact]
    public void Pdf_is_generated_and_says_when_rows_were_left_out()
    {
        var rows = Enumerable.Range(0, PdfWriter.MaxRows + 5).Select(i => (IReadOnlyList<object?>)new object?[] { "row " + i, i, null, true }).ToList();

        var pdf = PdfWriter.Write("Big report", Headers, rows);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void One_call_renders_the_same_rows_in_every_format()
    {
        var people = new[] { new { Name = "Ada", Score = 1 }, new { Name = "Omar", Score = 2 } };

        ExportFile Make(ExportFormat f) =>
            TabularExport.Render(f, "People", "people", people, ("Name", p => p.Name), ("Score", p => p.Score));

        var csv = Make(ExportFormat.Csv);
        csv.FileName.Should().Be("people.csv");
        Encoding.UTF8.GetString(csv.Content).Should().Contain("Ada,1").And.Contain("Omar,2");

        var xlsx = Make(ExportFormat.Xlsx);
        xlsx.FileName.Should().Be("people.xlsx");
        using var workbook = new XLWorkbook(new MemoryStream(xlsx.Content));
        workbook.Worksheet(1).RowsUsed().Count().Should().Be(3);

        Make(ExportFormat.Pdf).FileName.Should().Be("people.pdf");
    }

    [Theory]
    [InlineData(null, ExportFormat.Csv)]
    [InlineData("", ExportFormat.Csv)]
    [InlineData("CSV", ExportFormat.Csv)]
    [InlineData("xlsx", ExportFormat.Xlsx)]
    [InlineData("Excel", ExportFormat.Xlsx)]
    [InlineData("pdf", ExportFormat.Pdf)]
    public void Format_names_are_understood(string? value, ExportFormat expected) => ExportFormats.Parse(value).Should().Be(expected);

    [Fact]
    public void An_unknown_format_name_is_a_validation_error() =>
        ((Action)(() => ExportFormats.Parse("docx"))).Should().Throw<FluentValidation.ValidationException>();
}
