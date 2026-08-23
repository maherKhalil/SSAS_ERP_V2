using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.ImportExport;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Tests.ImportExport;

// THE CSV THE EXPORT WRITES (FP-009, DEC-DOC-0008, OD-DOC-006).
//
// The integration tests prove what the export SEES; these prove what it WRITES, at unit speed and without a
// database — which is the level at which the format's compatibility with the import parser can be checked
// exhaustively rather than by example.
public sealed class EmployeeExportWriterTests
{
  private static EmployeeExportRow Row(
    string number = "E-1",
    string name = "Layla Haddad",
    string department = "FIN",
    string position = "DEV",
    EmployeeStatus status = EmployeeStatus.Active) =>
    new(number, name, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
      department, position, status);

  // ---- THE COLUMN SET IS EXACT, AND `nationalId` IS NOT IN IT (OD-DOC-006).
  //
  // Asserted as an exact ordered list rather than a contains-check: the ORDER is part of what the run record
  // preserves under `SEC-DOC-0404`, and a new column would need an import counterpart, a round-trip proof
  // and a decision about whether it may leave the system at all.
  [Fact]
  [Trait("Decision", "OD-DOC-006")]
  public void The_header_is_the_exact_column_set_in_order()
  {
    var content = ExportEmployeesQueryHandler.Write([Row()]);

    Assert.StartsWith(
      "employeeNumber,fullName,employmentDate,departmentCode,positionCode,status\n",
      content,
      StringComparison.Ordinal);

    Assert.DoesNotContain("national", content, StringComparison.OrdinalIgnoreCase);
  }

  // ---- THE DATE FORMAT IS THE ONE THE IMPORT PARSES, AND IT IS CULTURE-INDEPENDENT.
  //
  // A culture-dependent format would produce a file that re-imports as a different date or not at all, which
  // is the round-trip property failing quietly rather than loudly.
  [Fact]
  [Trait("Decision", "DEC-DOC-0008")]
  public void The_employment_date_is_written_in_the_format_the_import_accepts()
  {
    var content = ExportEmployeesQueryHandler.Write([Row()]);

    Assert.Contains("2026-03-01", content, StringComparison.Ordinal);

    // And the import genuinely reads it back, which is what makes the claim more than a string comparison.
    var parsed = EmployeeImportCsvParser.Parse(content);

    Assert.True(parsed.IsSuccess);
    Assert.Equal(
      "2026-03-01",
      Assert.Single(parsed.Value.Rows).Value(EmployeeImportColumns.EmploymentDate));
  }

  // ================================================================================================
  // QUOTING — WRITTEN RFC 4180, READ BACK BY THE IMPORT PARSER RATHER THAN BY A SECOND OPINION
  // ================================================================================================
  //
  // Every case below is checked by ROUND-TRIPPING it through the import parser. A test that asserted the
  // expected bytes would prove the writer matches this test's idea of CSV; this proves the writer matches
  // the READER, which is the property that actually has to hold.
  [Theory]
  [InlineData("Haddad, Layla")]
  [InlineData("Layla \"The Architect\" Haddad")]
  [InlineData("Layla\nHaddad")]
  [InlineData("Layla\r\nHaddad")]
  [InlineData("\"")]
  [InlineData(",")]
  [InlineData("ليلى حداد")]
  [Trait("Decision", "DEC-DOC-0008")]
  public void A_name_needing_escaping_survives_the_round_trip_through_the_import_parser(string name)
  {
    var content = ExportEmployeesQueryHandler.Write([Row(name: name)]);

    var parsed = EmployeeImportCsvParser.Parse(content);

    Assert.True(parsed.IsSuccess);
    Assert.Equal(name, Assert.Single(parsed.Value.Rows).Value(EmployeeImportColumns.FullName));
  }

  // ---- NOTHING IS QUOTED THAT DOES NOT NEED TO BE.
  //
  // Not cosmetic: a file that quotes everything is one an operator's spreadsheet may re-save differently,
  // and the fewer transformations between what left and what comes back, the fewer ways the round trip has
  // to fail.
  [Fact]
  public void An_ordinary_row_carries_no_quotes_at_all()
  {
    Assert.DoesNotContain('"', ExportEmployeesQueryHandler.Write([Row()]));
  }

  // ---- STATUS IS WRITTEN AS ITS ENUM NAME, following every other status in the module.
  [Theory]
  [InlineData(EmployeeStatus.Active, "Active")]
  [InlineData(EmployeeStatus.Inactive, "Inactive")]
  [InlineData(EmployeeStatus.Terminated, "Terminated")]
  public void The_status_column_carries_the_enum_name(EmployeeStatus status, string expected)
  {
    Assert.Contains($",{expected}\n", ExportEmployeesQueryHandler.Write([Row(status: status)]));
  }

  // ---- AN EXPORT OF NOTHING IS A HEADER, NOT AN EMPTY FILE.
  //
  // A zero-byte file is indistinguishable from a failed download. A header alone says "this ran, and your
  // scope matched nobody", which is a different and more useful fact.
  [Fact]
  public void An_export_with_no_rows_still_carries_its_header()
  {
    var content = ExportEmployeesQueryHandler.Write([]);

    Assert.Equal(
      "employeeNumber,fullName,employmentDate,departmentCode,positionCode,status\n", content);
  }

  // ---- THE FILE NAME IS SERVER-GENERATED AND CARRIES NO CALLER INPUT.
  //
  // Reflecting a caller-supplied name into a `Content-Disposition` header is a header-injection surface for
  // no benefit. The timestamp is the only thing that varies, which is what keeps successive exports
  // distinguishable in a downloads folder.
  [Fact]
  public void The_file_name_is_generated_from_the_clock_alone()
  {
    var name = ExportEmployeesQueryHandler.FileNameFor(
      new DateTimeOffset(2026, 8, 22, 14, 5, 9, TimeSpan.Zero));

    Assert.Equal("employees-20260822-140509.csv", name);
  }

  // ---- THE HEADER AN EXPORT WRITES IS A HEADER AN IMPORT ACCEPTS (OD-DOC-010).
  //
  // The round-trip assertions above parse the export's own bytes UNMODIFIED. Before the ruling they had to
  // strip the `status` column first, and the stripper was itself a source of error — it began as "everything
  // after the last comma", which is not the column separator when a quoted name contains one. Removing it
  // removes a test helper that could have been wrong about the thing it was helping to test.
  [Fact]
  [Trait("Decision", "OD-DOC-010")]
  public void The_exported_header_parses_as_an_import_header()
  {
    var parsed = EmployeeImportCsvParser.Parse(ExportEmployeesQueryHandler.Write([Row()]));

    Assert.True(parsed.IsSuccess);

    var row = Assert.Single(parsed.Value.Rows);

    Assert.Equal("E-1", row.Value(EmployeeImportColumns.EmployeeNumber));
    Assert.Equal("FIN", row.Value(EmployeeImportColumns.DepartmentCode));
    Assert.Equal("DEV", row.Value(EmployeeImportColumns.PositionCode));
    Assert.Equal("Active", row.Value(EmployeeImportColumns.Status));
  }
}
