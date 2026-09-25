using FluentValidation;
using MajdsApp.SharedKernel.Exceptions;

namespace MajdsApp.SharedKernel.Import;

public record ImportRowError(int Row, string Reason);

/// <summary>The outcome of an import (FR-EXP-003): what was imported and, for everything that was not, the row and why.</summary>
public record ImportResult(int Total, int Succeeded, IReadOnlyList<ImportRowError> Errors)
{
    public int Failed => Errors.Count;
}

public static class ImportRunner
{
    /// <summary>
    /// Imports each row independently: valid rows are saved and invalid rows are reported with their row number and
    /// the reason (AC-EXP-2), and one bad row never stops the rest. The per-row action should go through the same
    /// command as the normal create endpoint, so imports obey exactly the same validation rules.
    /// Refusals (missing permission) are not row problems and propagate, so an unauthorized import fails as a whole.
    /// </summary>
    public static async Task<ImportResult> RunAsync(ImportTable table, Func<ImportRow, Task> importRow, CancellationToken ct)
    {
        var errors = new List<ImportRowError>();
        var succeeded = 0;

        foreach (var row in table.Rows)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await importRow(row);
                succeeded++;
            }
            catch (ValidationException ex)
            {
                errors.Add(new ImportRowError(row.Number, Describe(ex)));
            }
            catch (ConflictException ex)
            {
                errors.Add(new ImportRowError(row.Number, ex.Message));
            }
        }

        return new ImportResult(table.Rows.Count, succeeded, errors);
    }

    private static string Describe(ValidationException ex)
    {
        var messages = ex.Errors.Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
        return messages.Count > 0 ? string.Join(" ", messages) : ex.Message;
    }
}
