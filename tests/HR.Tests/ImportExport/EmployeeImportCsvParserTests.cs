using SSAS.HR.Application.ImportExport;

namespace SSAS.HR.Tests.ImportExport;

// THE COLUMN CONTRACT AND THE FILE PARSER (FP-009, DEC-DOC-0002, DEC-DOC-0003).
//
// The parser decides two things and nothing else: whether the FILE's columns are acceptable, and whether
// each ROW has the shape those columns describe. Everything about the values — a bad date, a taken employee
// number, an unresolvable department code — belongs to the handler, because those are the answers that need
// a company and a database.
public sealed class EmployeeImportCsvParserTests
{
  private const string Header = "employeeNumber,fullName,employmentDate,departmentCode,positionCode";

  // ================================================================================================
  // THE HEADER, DECIDED BEFORE THE FIRST DATA ROW IS READ
  // ================================================================================================
  [Fact]
  [Trait("Decision", "DEC-DOC-0002")]
  public void A_file_with_the_required_columns_parses()
  {
    var parsed = EmployeeImportCsvParser.Parse($"{Header}\nE-1,Layla Haddad,2026-03-01,FIN,DEV");

    Assert.True(parsed.IsSuccess);

    var row = Assert.Single(parsed.Value.Rows);

    Assert.Equal(2, row.RowNumber);
    Assert.Equal("E-1", row.Value(EmployeeImportColumns.EmployeeNumber));
    Assert.Equal("Layla Haddad", row.Value(EmployeeImportColumns.FullName));
    Assert.Equal("FIN", row.Value(EmployeeImportColumns.DepartmentCode));
    Assert.Null(row.Value(EmployeeImportColumns.NationalId));
  }

  // ---- CASE-INSENSITIVE AND ORDER-INDEPENDENT, because a header row is typed by a human in a spreadsheet.
  //
  // WHICH columns are present is still exact. The tolerance is about spelling the same column two ways, not
  // about which columns a file may carry.
  [Fact]
  [Trait("Decision", "DEC-DOC-0002")]
  public void The_header_is_matched_case_insensitively_and_in_any_order()
  {
    var parsed = EmployeeImportCsvParser.Parse(
      "PositionCode,DEPARTMENTCODE,employmentdate,FullName,EMPLOYEENUMBER\nDEV,FIN,2026-03-01,Layla,E-1");

    Assert.True(parsed.IsSuccess);

    var row = Assert.Single(parsed.Value.Rows);

    // The values land under the CANONICAL names, so nothing downstream ever sees the casing that was typed.
    Assert.Equal("E-1", row.Value(EmployeeImportColumns.EmployeeNumber));
    Assert.Equal("DEV", row.Value(EmployeeImportColumns.PositionCode));
  }

  // ================================================================================================
  // THE COLUMNS THAT DO NOT EXIST ARE REFUSED BY THE GENERAL RULE, NOT BY A SPECIAL CASE
  // ================================================================================================
  //
  // `companyId`, `branchId`, `tenantId` and `status` are absent from the contract, so a file carrying one is
  // refused for being unrecognised — the same refusal a typo gets. That is `FP-006`'s "absent by
  // construction, not merely validated" applied to a header row: there is no code that names these four, so
  // there is nothing for a future change to forget to keep.
  [Theory]
  [InlineData("companyId")]
  [InlineData("branchId")]
  [InlineData("tenantId")]
  [InlineData("status")]
  [InlineData("salary")]
  [InlineData("employeeNumbr")]
  [Trait("Decision", "DEC-DOC-0002")]
  public void An_unrecognised_column_refuses_the_file(string column)
  {
    var parsed = EmployeeImportCsvParser.Parse($"{Header},{column}\nE-1,Layla,2026-03-01,FIN,DEV,x");

    Assert.True(parsed.IsFailure);
    Assert.Equal(EmployeeImportErrors.HeaderColumnUnknown, parsed.Error);
  }

  [Theory]
  [InlineData("fullName,employmentDate,departmentCode,positionCode")]
  [InlineData("employeeNumber,employmentDate,departmentCode,positionCode")]
  [InlineData("employeeNumber,fullName,departmentCode,positionCode")]
  [InlineData("employeeNumber,fullName,employmentDate,positionCode")]
  [InlineData("employeeNumber,fullName,employmentDate,departmentCode")]
  [Trait("Decision", "DEC-DOC-0002")]
  public void A_missing_required_column_refuses_the_file(string header)
  {
    var parsed = EmployeeImportCsvParser.Parse($"{header}\nrow");

    Assert.True(parsed.IsFailure);
    Assert.Equal(EmployeeImportErrors.HeaderColumnMissing, parsed.Error);
  }

  // A duplicate column has two values for one field and no rule says which wins. Refusing is the only answer
  // that does not silently pick one.
  [Fact]
  [Trait("Decision", "DEC-DOC-0002")]
  public void A_duplicated_column_refuses_the_file()
  {
    var parsed = EmployeeImportCsvParser.Parse($"{Header},FULLNAME\nE-1,A,2026-03-01,FIN,DEV,B");

    Assert.True(parsed.IsFailure);
    Assert.Equal(EmployeeImportErrors.HeaderColumnDuplicated, parsed.Error);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("\n")]
  public void A_file_with_no_header_is_refused(string content)
  {
    var parsed = EmployeeImportCsvParser.Parse(content);

    Assert.True(parsed.IsFailure);
    Assert.Equal(EmployeeImportErrors.HeaderMissing, parsed.Error);
  }

  // ---- `nationalId` IS OPTIONAL, AND THE OPTIONALITY IS LOAD-BEARING (OD-DOC-006, DEC-DOC-0008).
  //
  // `OD-DOC-006` makes the column absent from every export, and `DEC-DOC-0008` requires an exported file to
  // re-import. The one column exports omit therefore has to be a column imports do not require, or the
  // round-trip property would be aspirational rather than checkable — so both of these must pass.
  [Fact]
  [Trait("Decision", "OD-DOC-006")]
  public void The_optional_national_id_column_may_be_present_or_absent()
  {
    Assert.True(EmployeeImportCsvParser.Parse($"{Header}\nE-1,A,2026-03-01,FIN,DEV").IsSuccess);

    var withColumn = EmployeeImportCsvParser.Parse(
      $"{Header},nationalId\nE-1,A,2026-03-01,FIN,DEV,1098765432");

    Assert.True(withColumn.IsSuccess);
    Assert.Equal(
      "1098765432",
      Assert.Single(withColumn.Value.Rows).Value(EmployeeImportColumns.NationalId));
  }

  // ================================================================================================
  // ROW SHAPE — REFUSED, NEVER PADDED
  // ================================================================================================
  //
  // Padding a short row with empty values would turn a misplaced comma into a missing full name, and the
  // operator would be told the wrong thing about the wrong cell.
  [Theory]
  [InlineData("E-1,Layla,2026-03-01,FIN")]
  [InlineData("E-1,Layla,2026-03-01,FIN,DEV,extra")]
  public void A_row_with_the_wrong_number_of_fields_is_a_row_error(string row)
  {
    var parsed = EmployeeImportCsvParser.Parse($"{Header}\n{row}");

    Assert.True(parsed.IsSuccess);
    Assert.Empty(parsed.Value.Rows);

    var error = Assert.Single(parsed.Value.Errors);

    Assert.Equal(2, error.RowNumber);
    Assert.Equal(EmployeeImportErrors.RowShapeInvalid, error.Error);

    // No column is named, because a ragged row has no offending column and naming one would be a guess.
    Assert.Null(error.Column);
  }

  // ---- ROW NUMBERS ARE FILE LINE NUMBERS, HEADER INCLUDED (DEC-DOC-0003).
  //
  // The operator's job is to open the file and fix that line, so the number has to be the one their editor
  // shows. A 1-based index over data rows would be off by one against every editor and every spreadsheet.
  [Fact]
  [Trait("Decision", "DEC-DOC-0003")]
  public void Row_numbers_are_the_line_numbers_the_operators_editor_shows()
  {
    var parsed = EmployeeImportCsvParser.Parse(
      $"{Header}\nE-1,A,2026-03-01,FIN,DEV\nE-2,B,2026-03-01,FIN,DEV\nE-3,C,2026-03-01,FIN,DEV");

    Assert.Equal([2, 3, 4], parsed.Value.Rows.Select(row => row.RowNumber));
  }

  // ================================================================================================
  // RFC 4180, TO THE EXTENT THIS CONTRACT NEEDS IT
  // ================================================================================================
  [Fact]
  public void A_quoted_field_may_contain_the_delimiter_a_quote_and_a_newline()
  {
    var parsed = EmployeeImportCsvParser.Parse(
      $"{Header}\n\"E,1\",\"Haddad, \"\"Layla\"\"\",2026-03-01,FIN,DEV");

    var row = Assert.Single(parsed.Value.Rows);

    Assert.Equal("E,1", row.Value(EmployeeImportColumns.EmployeeNumber));
    Assert.Equal("Haddad, \"Layla\"", row.Value(EmployeeImportColumns.FullName));
  }

  [Fact]
  public void A_newline_inside_a_quoted_field_does_not_start_a_new_row()
  {
    var parsed = EmployeeImportCsvParser.Parse(
      $"{Header}\n\"E-1\",\"Layla\nHaddad\",2026-03-01,FIN,DEV");

    var row = Assert.Single(parsed.Value.Rows);

    Assert.Equal("Layla\nHaddad", row.Value(EmployeeImportColumns.FullName));
    Assert.Empty(parsed.Value.Errors);
  }

  [Theory]
  [InlineData("\n")]
  [InlineData("\r\n")]
  [InlineData("\r")]
  public void Either_line_ending_is_accepted_and_a_trailing_one_adds_no_phantom_row(string ending)
  {
    var parsed = EmployeeImportCsvParser.Parse(
      $"{Header}{ending}E-1,A,2026-03-01,FIN,DEV{ending}");

    Assert.Single(parsed.Value.Rows);
    Assert.Empty(parsed.Value.Errors);
  }

  // A blank line in the middle of a file is a ragged row the operator should be told about, not a row the
  // parser silently drops — dropping it would make a file of 1,000 rows import 999 with no explanation.
  [Fact]
  public void A_blank_line_between_rows_is_reported_rather_than_skipped()
  {
    var parsed = EmployeeImportCsvParser.Parse(
      $"{Header}\nE-1,A,2026-03-01,FIN,DEV\n\nE-2,B,2026-03-01,FIN,DEV");

    Assert.Equal(2, parsed.Value.Rows.Count);
    Assert.Equal(3, Assert.Single(parsed.Value.Errors).RowNumber);
  }

  // ---- EVERY BAD ROW IS REPORTED, NOT THE FIRST (DEC-DOC-0003).
  //
  // A report naming one bad row in a thousand costs the operator a thousand round trips to find the rest.
  [Fact]
  [Trait("Decision", "DEC-DOC-0003")]
  public void Every_malformed_row_is_reported_rather_than_the_first()
  {
    var parsed = EmployeeImportCsvParser.Parse(
      $"{Header}\nshort\nE-2,B,2026-03-01,FIN,DEV\nalso,short");

    Assert.Single(parsed.Value.Rows);
    Assert.Equal([2, 4], parsed.Value.Errors.Select(error => error.RowNumber));
  }

  // ---- THE DECLARED COLUMN SET IS EXACTLY SIX, FIVE OF THEM REQUIRED.
  //
  // Asserted as an exact set rather than a contains-check, for the reason the E3 manifest inventory is: a
  // new column may need a validation rule, an export counterpart and a round-trip proof, and "it compiles"
  // settles none of them.
  [Fact]
  [Trait("Decision", "DEC-DOC-0002")]
  public void The_declared_column_set_is_exact()
  {
    Assert.Equal(
      ["departmentCode", "employeeNumber", "employmentDate", "fullName", "nationalId", "positionCode"],
      EmployeeImportColumns.All.OrderBy(column => column, StringComparer.Ordinal));

    Assert.Equal(
      ["departmentCode", "employeeNumber", "employmentDate", "fullName", "positionCode"],
      EmployeeImportColumns.Required.OrderBy(column => column, StringComparer.Ordinal));
  }
}
