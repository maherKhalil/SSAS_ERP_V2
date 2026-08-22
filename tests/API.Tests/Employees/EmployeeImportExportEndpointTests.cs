using System.Net;
using System.Text;
using System.Text.Json;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.HR.Application.ImportExport;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.API.Tests.Employees;

// THE IMPORT AND EXPORT HTTP SURFACE (FP-009 Phase 2).
//
// ================================================================================================
// TRANSPORT ONLY. THE RULES ARE PROVEN AGAINST REAL SQL SERVER AND ARE NOT RE-PROVEN HERE.
// ================================================================================================
//
// All-or-nothing, create-only, resolution by code, the caps, the round trip — every one is asserted in
// `Integration.Tests` against a real database, where "nothing was written" is a claim something can settle.
// Re-asserting them here would test the stub graph rather than the product.
//
// What only this level can establish: which content types are accepted, which permission each route
// carries, what a refusal looks like on the wire, whether the export's bytes carry a byte order mark, and
// whether the headers the contract promises are actually set.
public sealed class EmployeeImportExportEndpointTests : IClassFixture<EmployeeApiTestHost>
{
  private const string Header = "employeeNumber,fullName,employmentDate,departmentCode,positionCode";

  private const string OneRow = "IMP-1,Layla Haddad,2026-03-01,ENG,DEV";

  private readonly EmployeeApiTestHost host;

  public EmployeeImportExportEndpointTests(EmployeeApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  private static string Csv => $"{Header}\n{OneRow}";

  // ================================================================================================
  // T1. EVERY ROUTE CARRIES ITS OWN PERMISSION, AND NO OTHER ONE OPENS IT
  // ================================================================================================
  //
  // The cross-permission theory: each route is attempted with a token holding the WRONG HR permission. A
  // 403 for every pairing is what makes the separation `OD-DOC-005` ruled real at the routing layer rather
  // than merely declared in a catalog.
  [Theory]
  [InlineData("POST", "/api/hr/employees/import?importKey=k", HrPermissionNames.ViewEmployees)]
  [InlineData("POST", "/api/hr/employees/import/validate?importKey=k", HrPermissionNames.ViewEmployees)]
  [InlineData("POST", "/api/hr/employees/import?importKey=k", HrPermissionNames.ExportEmployees)]
  [InlineData("GET", "/api/hr/employees/export", HrPermissionNames.ViewEmployees)]
  [InlineData("GET", "/api/hr/employees/export", HrPermissionNames.ImportEmployees)]
  [InlineData("GET", "/api/hr/employees/import-runs", HrPermissionNames.ImportEmployees)]
  [InlineData("GET", "/api/hr/employees/export-runs", HrPermissionNames.ExportEmployees)]
  [Trait("Decision", "OD-DOC-005")]
  public async Task T1_A_token_holding_the_wrong_permission_is_refused(
    string method, string path, string wrongPermission)
  {
    using var request = method == "POST"
      ? EmployeeApiTestHost.CsvRequest(HttpMethod.Post, path, host.TokenWith(wrongPermission), Csv)
      : EmployeeApiTestHost.Request(HttpMethod.Get, path, host.TokenWith(wrongPermission));

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- AND UNAUTHENTICATED IS 401 EVERYWHERE, WHICH IS A DIFFERENT ANSWER FROM 403.
  [Theory]
  [InlineData("POST", "/api/hr/employees/import?importKey=k")]
  [InlineData("POST", "/api/hr/employees/import/validate?importKey=k")]
  [InlineData("GET", "/api/hr/employees/export")]
  [InlineData("GET", "/api/hr/employees/import-runs")]
  [InlineData("GET", "/api/hr/employees/export-runs")]
  public async Task T2_An_unauthenticated_request_is_refused_on_every_route(string method, string path)
  {
    using var request = method == "POST"
      ? EmployeeApiTestHost.CsvRequest(HttpMethod.Post, path, token: null, Csv)
      : EmployeeApiTestHost.Request(HttpMethod.Get, path, token: null);

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ---- AND THE RIGHT PERMISSION OPENS IT.
  [Fact]
  public async Task T3_The_declared_permission_admits_each_route()
  {
    using var imported = await host.Client.SendAsync(EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t3",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees), Csv));

    using var exported = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, "/api/hr/employees/export",
      host.TokenWith(HrPermissionNames.ExportEmployees, HrPermissionNames.ViewEmployees)));

    using var runs = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, "/api/hr/employees/import-runs",
      host.TokenWith(HrPermissionNames.ViewEmployees)));

    Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
    Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
    Assert.Equal(HttpStatusCode.OK, runs.StatusCode);
  }

  // ================================================================================================
  // T4. THE CONTENT-TYPE GATE, AT THE HTTP LAYER (DEC-DOC-0014)
  // ================================================================================================
  //
  // `StrictCsvReaderTests` proves the reader's behaviour directly. This proves the ROUTE is wired to it —
  // that a JSON body reaches a `400 request.invalid` rather than being parsed by something else.
  [Theory]
  [InlineData("application/json")]
  [InlineData("text/plain")]
  [InlineData("multipart/form-data; boundary=x")]
  [InlineData("text/csv; charset=windows-1256")]
  [Trait("Decision", "DEC-DOC-0014")]
  public async Task T4_A_body_that_is_not_utf8_csv_is_refused_by_the_route(string contentType)
  {
    using var request = EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t4",
      host.TokenWith(HrPermissionNames.ImportEmployees), Csv, contentType);

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));

    // NO RUN RECORD. A refusal of the REQUEST is not an import attempt, so nothing is recorded and the key
    // is not consumed — unlike a refused FILE, which is.
    Assert.Empty(host.ImportRuns.Runs);
  }

  // ---- BYTES THAT ARE NOT VALID UTF-8 ARE REFUSED, and only a byte-level request can express that.
  [Fact]
  [Trait("Decision", "DEC-DOC-0014")]
  public async Task T5_A_body_that_is_not_valid_utf8_is_refused()
  {
    // 0xC3 opens a two-byte sequence; 0x28 cannot continue it.
    using var request = EmployeeApiTestHost.BytesRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t5",
      host.TokenWith(HrPermissionNames.ImportEmployees), [0xC3, 0x28, 0x41]);

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ---- A UTF-8 BOM ON THE WAY IN IS ACCEPTED, which is what makes the round trip real.
  [Fact]
  [Trait("Decision", "DEC-DOC-0008")]
  public async Task T6_A_body_with_a_byte_order_mark_is_accepted()
  {
    byte[] body = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Csv)];

    using var request = EmployeeApiTestHost.BytesRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t6",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees),
      body, "text/csv; charset=utf-8");

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ================================================================================================
  // T7. `importKey` IS REQUIRED AND THE QUERY VOCABULARY IS CLOSED (DEC-DOC-0014)
  // ================================================================================================
  [Theory]
  [InlineData("/api/hr/employees/import")]
  [InlineData("/api/hr/employees/import?importKey=")]
  [InlineData("/api/hr/employees/import?key=k")]
  [InlineData("/api/hr/employees/import?importKey=k&unexpected=1")]
  [Trait("Decision", "DEC-DOC-0004")]
  public async Task T7_A_missing_or_undeclared_import_key_parameter_is_refused(string path)
  {
    using var request = EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, path, host.TokenWith(HrPermissionNames.ImportEmployees), Csv);

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // T8. A REFUSED FILE IS A `200` CARRYING THE REPORT, NOT A `400` (DEC-DOC-0003)
  // ================================================================================================
  //
  // The report IS the response — the operator's working document. A problem document would discard the very
  // thing they need in order to fix the file.
  [Fact]
  [Trait("Decision", "DEC-DOC-0003")]
  public async Task T8_A_refused_file_answers_200_with_the_row_report()
  {
    using var request = EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t8",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees),
      $"{Header}\nBAD-1,Layla,not-a-date,ENG,DEV");

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    var root = document.RootElement;

    Assert.Equal("Refused", root.GetProperty("outcome").GetString());
    Assert.Equal(0, root.GetProperty("acceptedCount").GetInt32());
    Assert.Equal(1, root.GetProperty("rejectedCount").GetInt32());

    var error = root.GetProperty("errors").EnumerateArray().Single();

    // The line number the operator's editor shows — header included.
    Assert.Equal(2, error.GetProperty("rowNumber").GetInt32());
    Assert.Equal("employmentDate", error.GetProperty("column").GetString());
    Assert.Equal("request.invalid", error.GetProperty("code").GetString());
  }

  // ---- THE ROW-LEVEL CODES ARE THE PROJECTION'S, NOT THE ROUTE MAPPER'S (R8).
  //
  // A department code that resolves to nothing reports `department.not_found` IN THE REPORT — a code the
  // route mapper never emits, because at route level the same domain error is `request.invalid`.
  [Fact]
  [Trait("Decision", "R8")]
  public async Task T9_An_unresolvable_department_code_reports_the_row_level_code()
  {
    using var request = EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t9",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees),
      $"{Header}\nIMP-9,Layla,2026-03-01,NOPE,DEV");

    using var response = await host.Client.SendAsync(request);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    var error = document.RootElement.GetProperty("errors").EnumerateArray().Single();

    Assert.Equal("departmentCode", error.GetProperty("column").GetString());
    Assert.Equal("department.not_found", error.GetProperty("code").GetString());
  }

  // ---- AND `status=Terminated` REPORTS ITS OWN NAMESPACE (R9, OD-DOC-010).
  [Fact]
  [Trait("Decision", "OD-DOC-010")]
  public async Task T10_A_status_an_import_cannot_create_reports_employee_import_status_not_creatable()
  {
    using var request = EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t10",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees),
      $"{Header},status\nIMP-10,Layla,2026-03-01,ENG,DEV,Terminated");

    using var response = await host.Client.SendAsync(request);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    var error = document.RootElement.GetProperty("errors").EnumerateArray().Single();

    Assert.Equal("status", error.GetProperty("column").GetString());
    Assert.Equal("employee_import.status_not_creatable", error.GetProperty("code").GetString());

    // The message names the remedy, which is the whole reason the column is recognized rather than unknown.
    Assert.Contains("Active", error.GetProperty("message").GetString()!, StringComparison.Ordinal);
  }

  // ================================================================================================
  // T11. THE EXPORT'S BYTES AND HEADERS (R10)
  // ================================================================================================
  [Fact]
  [Trait("Decision", "DEC-DOC-0008")]
  public async Task T11_The_export_returns_csv_with_a_byte_order_mark_and_an_attachment_disposition()
  {
    host.Reads.ExportRows =
    [
      new("E-1", "Layla Haddad", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
        "ENG", "DEV", SSAS.HR.Domain.Employees.EmployeeStatus.Active)
    ];

    using var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, "/api/hr/employees/export",
      host.TokenWith(HrPermissionNames.ExportEmployees, HrPermissionNames.ViewEmployees)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.StartsWith("text/csv", EmployeeApiTestHost.ContentType(response)!, StringComparison.Ordinal);

    // THE BOM, visible only through the byte reader — the string reader strips it.
    var bytes = await EmployeeApiTestHost.BodyBytesAsync(response);

    Assert.Equal(0xEF, bytes[0]);
    Assert.Equal(0xBB, bytes[1]);
    Assert.Equal(0xBF, bytes[2]);

    // `Content-Disposition` is a CONTENT header — the response-header idiom used elsewhere cannot see it.
    var disposition = EmployeeApiTestHost.ContentDisposition(response);

    Assert.NotNull(disposition);
    Assert.Contains("attachment", disposition!, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("employees-", disposition, StringComparison.Ordinal);
    Assert.EndsWith(".csv\"", disposition, StringComparison.Ordinal);
  }

  // ---- THE SECURITY HEADERS ARE ON THE FILE RESPONSE TOO, NOT BYPASSED BY IT.
  //
  // `nosniff` and `text/csv` compose rather than conflict: nosniff forbids the browser from second-guessing
  // a declared type, and this response declares its type honestly.
  [Fact]
  public async Task T12_The_export_response_still_carries_the_platform_security_headers()
  {
    using var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, "/api/hr/employees/export",
      host.TokenWith(HrPermissionNames.ExportEmployees, HrPermissionNames.ViewEmployees)));

    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    Assert.Contains("no-store", response.Headers.GetValues("Cache-Control").Single(),
      StringComparison.Ordinal);
  }

  // ---- THE EXPORT REFUSES PAGING (R7), and the refusal is the undeclared-parameter one.
  [Theory]
  [InlineData("pageNumber=1")]
  [InlineData("pageSize=50")]
  [Trait("Decision", "R7")]
  public async Task T13_The_export_refuses_paging_parameters(string parameter)
  {
    using var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, $"/api/hr/employees/export?{parameter}",
      host.TokenWith(HrPermissionNames.ExportEmployees, HrPermissionNames.ViewEmployees)));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ---- AND IT ACCEPTS THE SEARCH VOCABULARY IT INHERITS, INCLUDING `departmentId`.
  [Theory]
  [InlineData("status=Terminated")]
  [InlineData("departmentId=88888888-8888-8888-8888-888888888888")]
  [InlineData("companyScope=AllAuthorizedCompanies")]
  [InlineData("branchScope=AllAuthorizedBranches")]
  [InlineData("employeeNumber=E-1")]
  [Trait("Decision", "AC-DOC-0014")]
  public async Task T14_The_export_accepts_every_filter_the_search_declares(string parameter)
  {
    using var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, $"/api/hr/employees/export?{parameter}",
      host.TokenWith(HrPermissionNames.ExportEmployees, HrPermissionNames.ViewEmployees)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ================================================================================================
  // T15. THE RUN HISTORIES — PAGING, AND THE SCOPE COLUMNS' ABSENCE ON THE WIRE (DEC-DOC-0016)
  // ================================================================================================
  [Fact]
  [Trait("Decision", "DEC-DOC-0016")]
  public async Task T15_The_export_history_wire_shape_carries_the_column_set_and_no_scope()
  {
    host.RunHistory.ExportRuns.Add(new EmployeeExportRunListItem(
      Guid.NewGuid(), 42,
      ["employeeNumber", "fullName", "employmentDate", "departmentCode", "positionCode", "status"],
      DateTimeOffset.UtcNow, "exporter"));

    using var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, "/api/hr/employees/export-runs", host.TokenWith(HrPermissionNames.ViewEmployees)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await EmployeeApiTestHost.BodyAsync(response);

    Assert.Contains("columnSet", body, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("positionCode", body, StringComparison.Ordinal);

    // ---- THE SCOPE SNAPSHOT IS NOWHERE IN THE BYTES.
    //
    // Asserted against the SERIALIZED response rather than the object, because that is what a caller
    // receives — a property added later with a different name would still show up here.
    Assert.DoesNotContain("scope", body, StringComparison.OrdinalIgnoreCase);
  }

  [Theory]
  [InlineData("/api/hr/employees/import-runs?pageSize=0")]
  [InlineData("/api/hr/employees/import-runs?pageNumber=0")]
  [InlineData("/api/hr/employees/export-runs?pageSize=201")]
  [InlineData("/api/hr/employees/import-runs?unexpected=1")]
  public async Task T16_The_histories_refuse_bad_paging_and_undeclared_parameters(string path)
  {
    using var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, path, host.TokenWith(HrPermissionNames.ViewEmployees)));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ================================================================================================
  // T17. THE PER-ROUTE BODY CEILING IS DECLARED ON THE IMPORT ROUTES AND NOWHERE ELSE (R5, R6)
  // ================================================================================================
  //
  // ---- THIS ASSERTS THE **METADATA**, NOT A 10 MB REFUSAL, AND THAT IS DELIBERATE (R6).
  //
  // The harness runs on `TestServer`, which does not implement Kestrel's request-body limits — so sending
  // eleven megabytes here would prove nothing about production and would cost eleven megabytes to prove it.
  // The behavioural half is owned by the application-layer cap test in `Integration.Tests`, which refuses on
  // the number the operator was promised.
  //
  // What IS establishable here, and matters: the ceiling is DECLARED on exactly the two routes that carry a
  // file, and on none of the other forty-four.
  [Fact]
  [Trait("Decision", "R5")]
  public void T17_The_body_ceiling_is_declared_on_the_import_routes_only()
  {
    var endpoints = host.Endpoints();

    var withCeiling = endpoints
      .Where(endpoint => endpoint.Metadata.GetMetadata<
        RequestSizeEndpointConventions.MaxRequestBodySizeMetadata>() is not null)
      .Select(endpoint => endpoint.DisplayName ?? "?")
      .ToArray();

    Assert.Equal(2, withCeiling.Length);
    Assert.All(withCeiling, name => Assert.Contains("import", name, StringComparison.OrdinalIgnoreCase));

    // And the value is the cap the contract names, not an arbitrary number.
    var declared = endpoints
      .Select(endpoint => endpoint.Metadata.GetMetadata<
        RequestSizeEndpointConventions.MaxRequestBodySizeMetadata>())
      .Where(metadata => metadata is not null)
      .Select(metadata => metadata!.Bytes)
      .Distinct()
      .Single();

    Assert.Equal(EmployeeImportLimits.Default.MaximumBytes, declared);
  }

  // ================================================================================================
  // T18. THE IMPORT KEY REPLAY RETURNS THE ORIGINAL RESULT (DEC-DOC-0004)
  // ================================================================================================
  [Fact]
  [Trait("Decision", "DEC-DOC-0004")]
  public async Task T18_Replaying_an_import_key_returns_the_original_run_over_http()
  {
    var original = EmployeeImportRun.Applied(
      EmployeeApiTestHost.TenantId, EmployeeApiTestHost.CompanyA,
      ImportKey.Create("replayed").Value, "original.csv", 512, 7,
      DateTimeOffset.UtcNow, "someone-else").Value;

    host.ImportRuns.Existing = original;

    using var response = await host.Client.SendAsync(EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=replayed",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees), Csv));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    var root = document.RootElement;

    // THE ORIGINAL RUN'S counts, not this submission's — the caller asked "did my import happen?"
    Assert.Equal(original.Id, root.GetProperty("importRunId").GetGuid());
    Assert.Equal("Applied", root.GetProperty("outcome").GetString());
    Assert.Equal(7, root.GetProperty("rowCount").GetInt32());
  }

  // ================================================================================================
  // T19. THE FILE NAME HEADER'S THREE CONSTRAINTS (DEC-DOC-0017)
  // ================================================================================================
  [Theory]
  [InlineData(null, "import.csv")]
  [InlineData("people.csv", "people.csv")]
  [InlineData("../../etc/people.csv", "people.csv")]
  [InlineData("C:\\Users\\x\\people.csv", "people.csv")]
  [Trait("Decision", "DEC-DOC-0017")]
  public async Task T19_The_file_name_is_stored_as_a_leaf_name(string? sent, string expected)
  {
    using var request = EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t19",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees),
      Csv, fileNameHeader: sent);

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(expected, host.ImportRuns.Runs.Single().FileName);
  }

  // ---- A CONTROL CHARACTER IS REFUSED, NOT STRIPPED.
  //
  // Silently cleaning it would record a value the caller never sent.
  [Fact]
  [Trait("Decision", "DEC-DOC-0017")]
  public async Task T20_A_file_name_containing_a_control_character_is_refused()
  {
    using var request = EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t20",
      host.TokenWith(HrPermissionNames.ImportEmployees), Csv, fileNameHeader: "peo\u0001ple.csv");

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Empty(host.ImportRuns.Runs);
  }

  // ================================================================================================
  // T22. THE RACE: VALIDATION PASSED, THE WRITE FAILED, AND THE REFUSAL IS STILL RECORDED
  // ================================================================================================
  //
  // The per-company unique indexes are authoritative; the validation probes are an optimisation of the error
  // message, not the rule. So a concurrent create can take an employee number between the probe and the
  // insert, and the import must roll back, record a REFUSED run — consuming its key — and report the row.
  //
  // ---- WHAT THIS COVERS, AND — SAID PLAINLY — WHAT IT DOES NOT.
  //
  // It covers the CONTRACT: after a failed write the outcome is `Refused`, the offending row is named, and
  // the run record exists so the key stays consumed.
  //
  // It does NOT catch the defect that prompted it. The first implementation wrote the refusal record inside
  // the `await using` transaction scope, immediately after rolling back — and `await using var` runs to the
  // end of the METHOD, so that save was issued against an already-rolled-back transaction. **This harness's
  // transaction is a no-op** (`StubUnitOfWork.NoOpTransaction`), so the old code would pass this test too.
  //
  // The fix is therefore STRUCTURAL rather than test-enforced: the transaction now lives entirely inside
  // `CommitEmployeesAsync`, whose scope ends before anything decides what to record, so there is no line in
  // the deciding method from which the transaction is reachable. Recorded here because a reader who assumes
  // this test guards the scoping would be wrong, and might undo the structure believing it is covered.
  [Fact]
  [Trait("Decision", "DEC-DOC-0004")]
  public async Task T22_A_write_that_fails_after_validation_still_records_a_refused_run()
  {
    // Fail exactly ONE save — the employee's. The refusal record's save must then succeed, which is the
    // whole behaviour under test.
    host.UnitOfWork.FailOnce = new SSAS.BuildingBlocks.Domain.Error(
      "Persistence.UniqueConstraint", "raced");

    using var response = await host.Client.SendAsync(EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import?importKey=t22",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees), Csv));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    var root = document.RootElement;

    Assert.Equal("Refused", root.GetProperty("outcome").GetString());
    Assert.Equal(0, root.GetProperty("acceptedCount").GetInt32());

    // The row that raced is named, by the line number the operator's editor shows.
    var error = root.GetProperty("errors").EnumerateArray().Single();
    Assert.Equal(2, error.GetProperty("rowNumber").GetInt32());

    // ---- AND THE KEY IS CONSUMED. Releasing it would let the very submission the key exists to make
    // unrepeatable be replayed under it.
    var recorded = Assert.Single(host.ImportRuns.Runs);
    Assert.Equal(EmployeeImportOutcome.Refused, recorded.Outcome);
    Assert.Equal("T22", recorded.NormalizedImportKey);
  }

  // ---- AND THE VALIDATE ROUTE WRITES A `Validated` RUN AND NO EMPLOYEES.
  [Fact]
  [Trait("Decision", "FR-DOC-0101")]
  public async Task T21_The_validate_route_records_a_validated_run()
  {
    using var response = await host.Client.SendAsync(EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post, "/api/hr/employees/import/validate?importKey=t21",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees), Csv));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));

    Assert.Equal("Validated", document.RootElement.GetProperty("outcome").GetString());
    Assert.Equal(EmployeeImportOutcome.Validated, host.ImportRuns.Runs.Single().Outcome);
  }
}
