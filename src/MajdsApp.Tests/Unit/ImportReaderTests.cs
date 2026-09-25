using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using FluentValidation;
using MajdsApp.SharedKernel.Import;
using Xunit;

namespace MajdsApp.Tests.Unit;

public class ImportReaderTests
{
    private static MemoryStream Csv(string text, bool bom = false) =>
        new([.. (bom ? Encoding.UTF8.GetPreamble() : []), .. Encoding.UTF8.GetBytes(text)]);

    private static readonly string[] Required = ["Email"];

    [Fact]
    public void Rows_are_numbered_like_a_spreadsheet_and_blank_rows_are_skipped()
    {
        var table = TabularReader.Read(Csv("Email,Full name\r\na@b.co,Ada\r\n,\r\nc@d.co,Cy\r\n"), "x.csv", Required);

        table.Rows.Select(r => r.Number).Should().Equal(2, 4);
        table.Rows[1].Get("Email").Should().Be("c@d.co");
    }

    [Fact]
    public void Quoted_fields_commas_doubled_quotes_and_line_breaks_are_read_correctly()
    {
        var table = TabularReader.Read(Csv("Email,Full name\r\na@b.co,\"Smith, \"\"Ada\"\"\r\nLovelace\"\r\n"), "x.csv", Required);

        table.Rows.Should().ContainSingle();
        table.Rows[0].Get("Full name").Should().Be("Smith, \"Ada\"\r\nLovelace");
    }

    [Fact]
    public void A_byte_order_mark_and_semicolon_separators_are_tolerated_and_headers_ignore_case()
    {
        var table = TabularReader.Read(Csv("email;FULL NAME\na@b.co;Ada\n", bom: true), "x.csv", Required);

        table.Rows[0].Get("Email").Should().Be("a@b.co");
        table.Rows[0].Get("Full name").Should().Be("Ada");
    }

    [Fact]
    public void Column_order_does_not_matter_and_Arabic_text_survives()
    {
        var table = TabularReader.Read(Csv("Full name,Email\nمجد,a@b.co\n"), "x.csv", Required);

        table.Rows[0].Get("Full name").Should().Be("مجد");
        table.Rows[0].Get("Email").Should().Be("a@b.co");
    }

    [Fact]
    public void A_missing_required_column_rejects_the_whole_file()
    {
        var act = () => TabularReader.Read(Csv("Name\nAda\n"), "x.csv", Required);

        act.Should().Throw<ValidationException>().WithMessage("*missing the required column*Email*");
    }

    [Theory]
    [InlineData("x.txt")]
    [InlineData("x.pdf")]
    [InlineData("x")]
    public void Only_csv_and_xlsx_are_accepted(string name)
    {
        var act = () => TabularReader.Read(Csv("Email\na@b.co\n"), name, Required);

        act.Should().Throw<ValidationException>().WithMessage("*.csv or .xlsx*");
    }

    [Fact]
    public void An_empty_file_and_a_corrupt_workbook_are_reported_clearly()
    {
        ((Action)(() => TabularReader.Read(Csv(""), "x.csv", Required))).Should().Throw<ValidationException>().WithMessage("*empty*");
        ((Action)(() => TabularReader.Read(Csv("not an excel file"), "x.xlsx", Required))).Should().Throw<ValidationException>().WithMessage("*could not be read*");
    }

    [Fact]
    public void Too_many_rows_are_refused_up_front()
    {
        var text = "Email\n" + string.Join('\n', Enumerable.Range(0, TabularReader.MaxRows + 1).Select(i => $"u{i}@b.co"));

        ((Action)(() => TabularReader.Read(Csv(text), "x.csv", Required))).Should().Throw<ValidationException>().WithMessage("*limit*");
    }

    [Fact]
    public void An_Excel_file_is_read_from_its_first_sheet()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        sheet.Cell(1, 1).Value = "Email";
        sheet.Cell(1, 2).Value = "Age";
        sheet.Cell(2, 1).Value = "a@b.co";
        sheet.Cell(2, 2).Value = 42;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var table = TabularReader.Read(stream, "x.xlsx", Required);

        table.Rows.Should().ContainSingle();
        table.Rows[0].Get("Email").Should().Be("a@b.co");
        table.Rows[0].Get("Age").Should().Be("42");
    }

    [Fact]
    public async Task The_runner_keeps_going_after_a_bad_row_and_reports_it_with_the_row_number()
    {
        var table = TabularReader.Read(Csv("Email\na@b.co\nbad\nc@d.co\n"), "x.csv", Required);

        var result = await ImportRunner.RunAsync(table, row =>
            row.Get("Email").Contains('@') ? Task.CompletedTask : throw new ValidationException("Not an email."), default);

        result.Total.Should().Be(3);
        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(1);
        result.Errors.Should().ContainSingle().Which.Should().Be(new ImportRowError(3, "Not an email."));
    }
}
