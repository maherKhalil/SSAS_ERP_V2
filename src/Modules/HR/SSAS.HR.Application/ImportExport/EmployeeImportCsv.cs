using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Application.ImportExport;

// THE COLUMN CONTRACT AND THE FILE PARSER (FP-009, DEC-DOC-0002, DEC-DOC-0003).
//
// ================================================================================================
// THE COLUMNS THAT DO NOT EXIST ARE THE INTERESTING HALF.
// ================================================================================================
//
// There is no `companyId`, `branchId`, `tenantId` or `status` column. They are not validated away — they are
// ABSENT FROM THE CONTRACT, so a file carrying one is refused by the unknown-column rule and never reaches a
// place where somebody could decide what to do with it. That is `FP-006`'s "absent by construction, not
// merely validated" applied to a header row, and it is why the unknown-column rule is a refusal rather than
// a warning: an import that ignored an unrecognised `companyId` column would look like it honoured it.
//
// `nationalId` is optional, and its optionality is load-bearing rather than lenient. `OD-DOC-006` makes the
// column ABSENT from every export, and `DEC-DOC-0008` requires an exported file to re-import — so the one
// column exports omit has to be a column imports do not require, or the round-trip property would be
// aspirational instead of checkable.
public static class EmployeeImportColumns
{
  public const string EmployeeNumber = "employeeNumber";

  public const string FullName = "fullName";

  public const string EmploymentDate = "employmentDate";

  public const string DepartmentCode = "departmentCode";

  public const string PositionCode = "positionCode";

  public const string NationalId = "nationalId";

  // Matched case-insensitively and order-independently: a header row is written by a human in a spreadsheet,
  // and `EmployeeNumber` versus `employeenumber` is not a difference worth refusing a file over. WHICH
  // columns are present still is.
  public static readonly IReadOnlyList<string> Required =
    [EmployeeNumber, FullName, EmploymentDate, DepartmentCode, PositionCode];

  public static readonly IReadOnlyList<string> Optional = [NationalId];

  public static readonly IReadOnlyList<string> All = [.. Required, .. Optional];
}

// One data row, with the file line number the operator's editor shows them.
//
// `RowNumber` is the 1-BASED LINE NUMBER INCLUDING THE HEADER (`DEC-DOC-0003`), so the first data row is 2.
// Reporting a 1-based index over data rows instead would be off by one against every editor and every
// spreadsheet, and the operator's job is to go and fix that line.
public sealed record EmployeeImportRow(int RowNumber, IReadOnlyDictionary<string, string> Values)
{
  public string? Value(string column) =>
    Values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

// One thing wrong with one row. `Column` is null for a problem that belongs to the row rather than to any
// single cell — a ragged row has no offending column, and naming one would be a guess.
public sealed record EmployeeImportRowError(int RowNumber, string? Column, Error Error);

// The parsed file. Rows that failed STRUCTURALLY are already errors here; rows that parsed are handed on for
// validation, because a file whose shape is wrong and a file whose values are wrong fail for different
// reasons and the operator needs to see both.
public sealed record EmployeeImportFile(
  IReadOnlyList<EmployeeImportRow> Rows, IReadOnlyList<EmployeeImportRowError> Errors);

// RFC 4180 parsing, narrowed to what this contract needs.
//
// Deliberately not a general-purpose CSV library: the whole surface is one declared column set, quoted
// fields, doubled quotes and either line ending. A library would bring configuration surface — delimiters,
// culture, header inference, type coercion — whose defaults are exactly the silent behaviour `DEC-DOC-0002`
// refuses.
public static class EmployeeImportCsvParser
{
  public const char Delimiter = ',';

  public const char Quote = '"';

  // Returns a failure for a problem with the FILE — a missing header, an unknown column, a duplicate column.
  // Every one of those is decided before the first data row is read, because a file whose columns are wrong
  // has no rows worth validating and reporting five thousand row errors for one bad header helps nobody.
  public static Result<EmployeeImportFile> Parse(string? content)
  {
    var lines = SplitLines(content ?? string.Empty);

    if (lines.Count == 0)
    {
      return Result.Failure<EmployeeImportFile>(EmployeeImportErrors.HeaderMissing);
    }

    var header = SplitFields(lines[0]);
    var headerResult = ValidateHeader(header);
    if (headerResult.IsFailure)
    {
      return Result.Failure<EmployeeImportFile>(headerResult.Error);
    }

    var columns = headerResult.Value;
    var rows = new List<EmployeeImportRow>();
    var errors = new List<EmployeeImportRowError>();

    for (var index = 1; index < lines.Count; index++)
    {
      // Line 1 is the header, so a data row at list index 1 is line 2.
      var rowNumber = index + 1;
      var fields = SplitFields(lines[index]);

      // ---- A RAGGED ROW IS A ROW ERROR, NOT A FILE ERROR, AND NOT A PADDED ROW.
      //
      // Padding a short row with empty values would turn a misplaced comma into a missing full name, and the
      // operator would be told the wrong thing about the wrong cell. Refusing the row names what actually
      // happened. Under `OD-DOC-003` one of these fails the file anyway, so nothing is gained by guessing.
      if (fields.Count != columns.Count)
      {
        errors.Add(new EmployeeImportRowError(rowNumber, null, EmployeeImportErrors.RowShapeInvalid));
        continue;
      }

      var values = new Dictionary<string, string>(StringComparer.Ordinal);
      for (var column = 0; column < columns.Count; column++)
      {
        values[columns[column]] = fields[column];
      }

      rows.Add(new EmployeeImportRow(rowNumber, values));
    }

    return Result.Success(new EmployeeImportFile(rows, errors));
  }

  // Returns the header mapped onto the CANONICAL column names, so everything downstream reads
  // `EmployeeImportColumns.FullName` and never a casing the operator happened to type.
  private static Result<IReadOnlyList<string>> ValidateHeader(IReadOnlyList<string> header)
  {
    var canonical = new List<string>(header.Count);
    var seen = new HashSet<string>(StringComparer.Ordinal);

    foreach (var raw in header)
    {
      var match = EmployeeImportColumns.All
        .FirstOrDefault(column => string.Equals(column, raw.Trim(), StringComparison.OrdinalIgnoreCase));

      // AN UNRECOGNISED COLUMN IS A REFUSAL. `companyId`, `branchId`, `tenantId` and `status` arrive here
      // like any other unknown name, which is the point: they are refused because they are not in the
      // contract, not by a special case somebody could forget to keep.
      if (match is null)
      {
        return Result.Failure<IReadOnlyList<string>>(EmployeeImportErrors.HeaderColumnUnknown);
      }

      // A duplicate column has two values for one field and no rule says which wins.
      if (!seen.Add(match))
      {
        return Result.Failure<IReadOnlyList<string>>(EmployeeImportErrors.HeaderColumnDuplicated);
      }

      canonical.Add(match);
    }

    return EmployeeImportColumns.Required.Any(column => !seen.Contains(column))
      ? Result.Failure<IReadOnlyList<string>>(EmployeeImportErrors.HeaderColumnMissing)
      : Result.Success<IReadOnlyList<string>>(canonical);
  }

  // Splits on record boundaries, honouring newlines INSIDE quoted fields. A trailing newline does not
  // produce a phantom empty record, and a wholly empty file produces no records at all.
  private static List<string> SplitLines(string content)
  {
    var lines = new List<string>();
    var current = new System.Text.StringBuilder();
    var quoted = false;

    for (var index = 0; index < content.Length; index++)
    {
      var character = content[index];

      if (character == Quote)
      {
        quoted = !quoted;
        current.Append(character);
        continue;
      }

      if (!quoted && (character == '\n' || character == '\r'))
      {
        // Consume the LF of a CRLF pair so one line ending never produces two records.
        if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
        {
          index++;
        }

        lines.Add(current.ToString());
        current.Clear();
        continue;
      }

      current.Append(character);
    }

    if (current.Length > 0)
    {
      lines.Add(current.ToString());
    }

    // A file of blank lines is a file with no records. Blank lines BETWEEN records are kept, because a blank
    // line in the middle of a file is a ragged row the operator should be told about rather than a row the
    // parser silently drops.
    return lines.Count == 1 && string.IsNullOrWhiteSpace(lines[0]) ? [] : lines;
  }

  private static List<string> SplitFields(string line)
  {
    var fields = new List<string>();
    var current = new System.Text.StringBuilder();
    var quoted = false;

    for (var index = 0; index < line.Length; index++)
    {
      var character = line[index];

      if (quoted)
      {
        if (character != Quote)
        {
          current.Append(character);
          continue;
        }

        // A doubled quote inside a quoted field is one literal quote.
        if (index + 1 < line.Length && line[index + 1] == Quote)
        {
          current.Append(Quote);
          index++;
          continue;
        }

        quoted = false;
        continue;
      }

      switch (character)
      {
        case Quote:
          quoted = true;
          break;

        case Delimiter:
          fields.Add(current.ToString());
          current.Clear();
          break;

        default:
          current.Append(character);
          break;
      }
    }

    fields.Add(current.ToString());

    return fields;
  }
}
