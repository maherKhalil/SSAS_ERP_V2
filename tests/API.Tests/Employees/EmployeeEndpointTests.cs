using System.Net;
using System.Text.Json;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;

namespace SSAS.API.Tests.Employees;

// ==================================================================================================
// THE EMPLOYEE HTTP CONTRACT, PROVEN OVER REAL HTTP (FP-006C5, A1-A45).
// ==================================================================================================
//
// These answer a question the application and SQL tests cannot: what a CALLER sees. The same refusal that
// Integration.Tests proves is correct in the database has to arrive as the right status code, the right
// code string, and — for the two scope dimensions — a body indistinguishable from the other reasons it
// could have been refused for.
[Collection(EmployeeApiEndpointGroup.Name)]
public sealed class EmployeeEndpointTests : IClassFixture<EmployeeApiTestHost>
{
  private const string Route = "/api/hr/employees";

  private readonly EmployeeApiTestHost host;

  public EmployeeEndpointTests(EmployeeApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ================================================================================================
  // CREATE — A1 to A8
  // ================================================================================================

  [Fact]
  public async Task A1_Authorized_create_succeeds()
  {
    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    var body = document.RootElement;

    // The server-stamped ownership comes BACK even though it could not be sent.
    Assert.Equal(EmployeeApiTestHost.CompanyA, body.GetProperty("companyId").GetGuid());
    Assert.Equal(EmployeeApiTestHost.BranchA, body.GetProperty("branchId").GetGuid());
    Assert.Equal("Active", body.GetProperty("status").GetString());
    Assert.Equal("AAAAAAAAB9E=", body.GetProperty("rowVersion").GetString());

    // The contract's response security headers, applied by the group filter so no route can omit them.
    Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
    Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
  }

  [Fact]
  public async Task A2_Create_without_the_create_permission_is_refused()
  {
    var response = await Send(HttpMethod.Post, Route, ViewToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task A3_Create_without_company_scope_is_refused()
  {
    host.CompanyContext.Error = new Error("Company.InvalidSelection", "denied");

    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("company.scope_denied", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task A4_Create_without_branch_scope_is_refused()
  {
    host.CurrentBranch.Error = new Error("Branch.InvalidSelection", "denied");

    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("branch.scope_denied", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // A malformed header is the caller's own input, so saying so discloses nothing — and a generic denial
  // would leave them hunting a permissions problem they do not have.
  [Theory]
  [InlineData("not-a-guid")]
  [InlineData("{22222222-2222-2222-2222-222222222222}")]
  [InlineData("")]
  public async Task A5_A_malformed_company_header_is_a_validation_failure(string header)
  {
    host.CompanyContext.Error = new Error("Company.InvalidSelectionFormat", "malformed");

    var request = EmployeeApiTestHost.Request(
      HttpMethod.Post, Route, CreateToken, ValidCreateBody, companyHeader: header);
    var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE OWNERSHIP FIELDS ARE UNREPRESENTABLE, NOT IGNORED.
  //
  // Silently dropping them would tell the caller their write succeeded as sent, when what was written was
  // something else entirely.
  [Theory]
  [InlineData("tenantId")]
  [InlineData("companyId")]
  [InlineData("branchId")]
  [InlineData("status")]
  public async Task A6_Create_rejects_a_spoofed_ownership_field(string field)
  {
    var body = $$"""
      {"employeeNumber":"EMP-1","fullName":"A B","employmentDate":"2026-03-01T00:00:00+00:00","departmentId":"88888888-8888-8888-8888-888888888888","positionId":"99999999-9999-9999-9999-999999999999","{{field}}":"22222222-2222-2222-2222-222222222222"}
      """;

    var response = await Send(HttpMethod.Post, Route, CreateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // A6b — THE DEPARTMENT IS PART OF THE CREATE CONTRACT (FP-007 Phase 3).
  // ================================================================================================
  //
  // CONTRACT GUARDS, NOT A NEW ENDPOINT. Phase 3 adds no department route — Phase 4 owns HTTP wiring. What
  // is asserted here is that the EXISTING create route now requires a department and refuses the three ways
  // a department can be unusable, because those refusals reach the wire today whether or not anyone
  // intended to test them.
  [Fact]
  public async Task A6b_Create_without_a_department_is_refused()
  {
    const string body = """
      {"employeeNumber":"EMP-1","fullName":"A B","employmentDate":"2026-03-01T00:00:00+00:00"}
      """;

    var response = await Send(HttpMethod.Post, Route, CreateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task A6c_Create_into_an_inactive_department_is_refused()
  {
    var body = $$"""
      {"employeeNumber":"EMP-1","fullName":"A B","employmentDate":"2026-03-01T00:00:00+00:00","departmentId":"{{EmployeeApiTestHost.DepartmentInactive}}","positionId":"{{EmployeeApiTestHost.PositionA}}"}
      """;

    var response = await Send(HttpMethod.Post, Route, CreateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // A department in a company the caller has not established. Refused with the SAME answer an absent
  // department gets, which is what stops this being a probe for other companies' departments.
  [Fact]
  public async Task A6d_Create_into_another_companys_department_is_refused()
  {
    var body = $$"""
      {"employeeNumber":"EMP-1","fullName":"A B","employmentDate":"2026-03-01T00:00:00+00:00","departmentId":"{{EmployeeApiTestHost.DepartmentOtherCompany}}","positionId":"{{EmployeeApiTestHost.PositionA}}"}
      """;

    var response = await Send(HttpMethod.Post, Route, CreateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // CHANGE DEPARTMENT (FP-007 Phase 4). ON THE EMPLOYEE PREFIX, UNDER EMPLOYEE UPDATE AUTHORITY.
  // ================================================================================================
  //
  // Phase 3 asserted here that no such route existed. It now does, and that guard has been REPLACED rather
  // than deleted — it was passing only because this harness did not map the route, so it described the
  // harness rather than the Host. The host now maps it, and these assert what it actually does.
  //
  // The permission is HR.Employees.Update, NOT Transfer: DepartmentId is a classification, not a security
  // partition (ADR-024), so nothing moves across an authorization boundary.
  [Fact]
  public async Task A6e_Change_department_succeeds_with_employee_update_authority()
  {
    var response = await Send(
      HttpMethod.Post,
      $"{Route}/{EmployeeApiTestHost.EmployeeId}/change-department",
      UpdateToken,
      ChangeDepartmentBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task A6f_Change_department_without_the_update_permission_is_forbidden()
  {
    var response = await Send(
      HttpMethod.Post,
      $"{Route}/{EmployeeApiTestHost.EmployeeId}/change-department",
      ViewToken,
      ChangeDepartmentBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- PERMISSION BLEED, IN THE DIRECTION THIS ROUTE INVITES.
  //
  // Department permissions are the sharp probe here: a reader might reasonably assume "it changes a
  // department, so it needs a department permission". It does not — it changes an EMPLOYEE.
  [Fact]
  public async Task A6g_Change_department_with_only_department_permissions_is_forbidden()
  {
    var token = host.TokenWith(
      HrPermissionNames.UpdateDepartments,
      HrPermissionNames.ViewDepartments,
      HrPermissionNames.CreateDepartments);

    var response = await Send(
      HttpMethod.Post,
      $"{Route}/{EmployeeApiTestHost.EmployeeId}/change-department",
      token,
      ChangeDepartmentBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // An unusable destination is the caller's own argument, so it is a 400 rather than a 404 about the
  // employee they correctly addressed.
  [Fact]
  public async Task A6h_Change_department_into_another_companys_department_is_rejected()
  {
    var body = $$"""
      {"departmentId":"{{EmployeeApiTestHost.DepartmentOtherCompany}}","expectedRowVersion":"AAAAAAAAB9E="}
      """;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/change-department", UpdateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task A6i_Change_department_rejects_an_undeclared_field()
  {
    var body = $$"""
      {"departmentId":"{{EmployeeApiTestHost.DepartmentA}}","branchId":"44444444-4444-4444-4444-444444444444","expectedRowVersion":"AAAAAAAAB9E="}
      """;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/change-department", UpdateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task A7_A_duplicate_employee_number_in_the_same_company_conflicts()
  {
    host.Repository.NumberExists = true;

    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("employee.number_conflict", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // Uniqueness is per company, so the same number in a company the caller also holds is not a conflict.
  [Fact]
  public async Task A8_The_same_number_in_a_different_company_succeeds()
  {
    host.CompanyContext.Established = EmployeeApiTestHost.CompanyB;
    host.Repository.NumberExists = false;

    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  // ================================================================================================
  // GET — A9 to A13
  // ================================================================================================

  [Fact]
  public async Task A9_Authorized_get_succeeds()
  {
    var response = await Send(HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    Assert.Equal(EmployeeApiTestHost.EmployeeId, document.RootElement.GetProperty("employeeId").GetGuid());
  }

  // The read service returns nothing for an out-of-scope employee, and the handler turns that into
  // NotFound. What matters here is that the HTTP layer does not restore the distinction.
  [Fact]
  public async Task A10_An_employee_in_an_unauthorized_branch_is_not_exposed()
  {
    host.Reads.Detail = null;

    var response = await Send(HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", ViewToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("employee.not_found", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task A11_An_employee_in_an_unauthorized_company_is_not_exposed()
  {
    host.Reads.Detail = null;

    var known = await Send(HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", ViewToken);
    var unknown = await Send(HttpMethod.Get, $"{Route}/{Guid.NewGuid()}", ViewToken);

    // BYTE-FOR-BYTE the same answer, so the pair cannot be compared to learn the identifier exists.
    Assert.Equal(unknown.StatusCode, known.StatusCode);
    Assert.Equal(
      await EmployeeApiTestHost.ProblemCodeAsync(unknown),
      await EmployeeApiTestHost.ProblemCodeAsync(known));
  }

  // Termination retains the record, so hiding it here would make a retained employee unreachable — which a
  // caller cannot distinguish from deletion.
  [Fact]
  public async Task A12_A_terminated_employee_can_still_be_retrieved()
  {
    host.Reads.Detail = StubEmployeeReads.SampleDetail(
      EmployeeStatus.Terminated, new DateTimeOffset(2027, 1, 31, 0, 0, 0, TimeSpan.Zero));

    var response = await Send(HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    Assert.Equal("Terminated", document.RootElement.GetProperty("status").GetString());
  }

  // ---- THE ONE THAT MATTERS MOST (ADR-025 decision 8).
  //
  // Platform.Tenant.Administer widens the two SCOPE dimensions and grants no operation. The token below
  // carries it and no HR permission, and the request is refused before any employee is read.
  [Fact]
  public async Task A13_A_tenant_administrator_without_the_hr_view_permission_is_refused()
  {
    var response = await Send(
      HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", host.TokenWith("Platform.Tenant.Administer"));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ================================================================================================
  // SEARCH — A14 to A19
  // ================================================================================================

  [Fact]
  public async Task A14_Search_defaults_to_the_current_branch()
  {
    var response = await Send(HttpMethod.Get, Route, ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal([EmployeeApiTestHost.BranchA], host.Reads.LastScope!.Branches.BranchIds);
  }

  [Fact]
  public async Task A15_Search_accepts_a_selected_subset_of_authorized_branches()
  {
    var response = await Send(
      HttpMethod.Get,
      $"{Route}?branchScope=SelectedAuthorizedBranches&branchIds={EmployeeApiTestHost.BranchA},{EmployeeApiTestHost.BranchB}",
      ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(
      [EmployeeApiTestHost.BranchA, EmployeeApiTestHost.BranchB],
      host.Reads.LastScope!.Branches.BranchIds);
  }

  // Refused, not quietly intersected down to the authorized subset.
  [Fact]
  public async Task A16_A_selection_containing_an_unauthorized_branch_is_refused()
  {
    var response = await Send(
      HttpMethod.Get,
      $"{Route}?branchScope=SelectedAuthorizedBranches&branchIds={EmployeeApiTestHost.BranchA},{EmployeeApiTestHost.BranchC}",
      ViewToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("branch.scope_denied", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // "All" is the caller's authorized set, materialized — never the absence of a predicate.
  [Fact]
  public async Task A17_Search_materializes_all_authorized_branches()
  {
    var response = await Send(HttpMethod.Get, $"{Route}?branchScope=AllAuthorizedBranches", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(
      [EmployeeApiTestHost.BranchA, EmployeeApiTestHost.BranchB],
      host.Reads.LastScope!.Branches.BranchIds);
  }

  [Theory]
  [InlineData("?pageSize=201")]
  [InlineData("?pageSize=0")]
  [InlineData("?pageNumber=0")]
  [InlineData("?pageNumber=-1")]
  [InlineData("?pageSize=abc")]
  // A filter the approved contract does not define must be refused, not ignored.
  [InlineData("?fullNameContains=Layla")]
  // A branch list without the mode that takes one.
  [InlineData("?branchIds=44444444-4444-4444-4444-444444444444")]
  public async Task A18_Invalid_paging_and_unknown_parameters_are_refused(string query)
  {
    var response = await Send(HttpMethod.Get, $"{Route}{query}", ViewToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task A19_Search_does_not_leak_across_companies_by_default()
  {
    var response = await Send(HttpMethod.Get, Route, ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    // CurrentCompany by default: the caller holds two companies, and the scope names exactly the selected
    // one. Asking for the wider scope is possible, but only by naming it.
    Assert.Equal([EmployeeApiTestHost.CompanyA], host.Reads.LastScope!.Companies.CompanyIds);

    var wider = await Send(HttpMethod.Get, $"{Route}?companyScope=AllAuthorizedCompanies", ViewToken);

    Assert.Equal(HttpStatusCode.OK, wider.StatusCode);
    Assert.Equal(
      [EmployeeApiTestHost.CompanyA, EmployeeApiTestHost.CompanyB],
      host.Reads.LastScope!.Companies.CompanyIds);
  }

  // ================================================================================================
  // UPDATE — A20 to A23
  // ================================================================================================

  [Fact]
  public async Task A20_Authorized_update_succeeds()
  {
    var response = await Send(
      HttpMethod.Put, $"{Route}/{EmployeeApiTestHost.EmployeeId}", UpdateToken, ValidUpdateBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task A21_A_stale_rowversion_conflicts()
  {
    var body = """
      {"fullName":"Layla Haddad-Nasr","expectedRowVersion":"AAAAAAAAAAA="}
      """;

    var response = await Send(HttpMethod.Put, $"{Route}/{EmployeeApiTestHost.EmployeeId}", UpdateToken, body);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("concurrency.conflict", await EmployeeApiTestHost.ProblemCodeAsync(response));

    // No EF or SQL detail escapes.
    var payload = await EmployeeApiTestHost.BodyAsync(response);
    Assert.DoesNotContain("DbUpdate", payload, StringComparison.Ordinal);
    Assert.DoesNotContain("RowVersion", payload, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("tenantId")]
  [InlineData("companyId")]
  [InlineData("branchId")]
  [InlineData("employeeNumber")]
  [InlineData("status")]
  public async Task A22_Update_rejects_ownership_and_identity_fields(string field)
  {
    var body = $$"""
      {"fullName":"Layla","expectedRowVersion":"AAAAAAAAB9E=","{{field}}":"x"}
      """;

    var response = await Send(HttpMethod.Put, $"{Route}/{EmployeeApiTestHost.EmployeeId}", UpdateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // The write boundary refuses a cross-company or cross-branch save at SaveChanges. That refusal is an
  // AUTHORIZATION outcome and must arrive as 403, never as a storage outage.
  [Theory]
  [InlineData("Company.InvalidSelection", "company.scope_denied")]
  [InlineData("Branch.InvalidSelection", "branch.scope_denied")]
  public async Task A23_A_cross_scope_write_refusal_is_forbidden_not_a_server_error(string code, string expected)
  {
    host.UnitOfWork.Failure = new Error(code, "refused");

    var response = await Send(
      HttpMethod.Put, $"{Route}/{EmployeeApiTestHost.EmployeeId}", UpdateToken, ValidUpdateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(expected, await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // TERMINATE — A24 to A27
  // ================================================================================================

  [Fact]
  public async Task A24_Authorized_termination_succeeds()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/terminate", TerminateToken, ValidTerminateBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task A25_Termination_with_a_stale_rowversion_conflicts()
  {
    var body = """
      {"terminationDate":"2027-01-31T00:00:00+00:00","reasonCode":"Resignation","expectedRowVersion":"AAAAAAAAAAA="}
      """;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/terminate", TerminateToken, body);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("concurrency.conflict", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // A domain rule, mapped by its own code rather than swept into a generic failure.
  [Fact]
  public async Task A26_A_termination_date_before_employment_is_a_validation_failure()
  {
    var body = """
      {"terminationDate":"2020-01-01T00:00:00+00:00","reasonCode":"Resignation","expectedRowVersion":"AAAAAAAAB9E="}
      """;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/terminate", TerminateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // Update authority is not termination authority: ending employment is a different decision.
  [Fact]
  public async Task A27_Termination_without_the_terminate_permission_is_refused()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/terminate", UpdateToken, ValidTerminateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ================================================================================================
  // TRANSFER — A28 to A35
  // ================================================================================================

  [Fact]
  public async Task A28_Authorized_transfer_succeeds()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/transfer", TransferToken, ValidTransferBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task A29_Transfer_without_the_transfer_permission_is_refused()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/transfer", UpdateToken, ValidTransferBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- THE DESTINATION IS AN ARGUMENT, NOT AN ASSERTION.
  //
  // A branch the caller does not hold is refused even though they named it themselves, which is the whole
  // point: naming a branch never confers reach to it.
  [Fact]
  public async Task A30_A_destination_the_caller_cannot_reach_is_refused()
  {
    var body = $$"""
      {"destinationBranchId":"{{EmployeeApiTestHost.BranchC}}","reasonCode":"Reorganisation","expectedRowVersion":"AAAAAAAAB9E="}
      """;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/transfer", TransferToken, body);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("branch.scope_denied", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // An inactive destination is refused with the SAME answer as an unauthorized one, so activity state is
  // not disclosed either.
  [Fact]
  public async Task A31_An_inactive_destination_is_refused_identically()
  {
    host.BranchAccess.Permitted = [EmployeeApiTestHost.BranchA];

    var inactive = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/transfer", TransferToken, ValidTransferBody);

    var unauthorized = await Send(
      HttpMethod.Post,
      $"{Route}/{EmployeeApiTestHost.EmployeeId}/transfer",
      TransferToken,
      $$"""{"destinationBranchId":"{{EmployeeApiTestHost.BranchC}}","reasonCode":"Reorganisation","expectedRowVersion":"AAAAAAAAB9E="}""");

    Assert.Equal(unauthorized.StatusCode, inactive.StatusCode);
    Assert.Equal(
      await EmployeeApiTestHost.ProblemCodeAsync(unauthorized),
      await EmployeeApiTestHost.ProblemCodeAsync(inactive));
  }

  [Fact]
  public async Task A32_Transfer_with_a_stale_rowversion_conflicts()
  {
    var body = $$"""
      {"destinationBranchId":"{{EmployeeApiTestHost.BranchB}}","reasonCode":"Reorganisation","expectedRowVersion":"AAAAAAAAAAA="}
      """;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/transfer", TransferToken, body);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("concurrency.conflict", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // A terminated employee has no current assignment to move.
  [Fact]
  public async Task A33_Transferring_a_terminated_employee_is_refused()
  {
    host.Repository.Employee = StubEmployeeRepository.NewEmployee(EmployeeStatus.Terminated);

    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/transfer", TransferToken, ValidTransferBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("employee.transition_invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ---- INACTIVE-SOURCE RECOVERY IS NOT PART OF THE HTTP CONTRACT.
  //
  // It is an administrative recovery under ADR-024 decision 12, and the approved transfer contract carries
  // no flag for it. A caller cannot request one, which is why the field is rejected rather than honoured —
  // the recovery path stays reachable only where the owner sanctioned it.
  [Fact]
  public async Task A34_A35_Inactive_source_recovery_cannot_be_requested_over_http()
  {
    var body = $$"""
      {"destinationBranchId":"{{EmployeeApiTestHost.BranchB}}","reasonCode":"Reorganisation","expectedRowVersion":"AAAAAAAAB9E=","inactiveSourceRecovery":true}
      """;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{EmployeeApiTestHost.EmployeeId}/transfer", TransferToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // HISTORY — A36 to A38
  // ================================================================================================

  [Fact]
  public async Task A36_Authorized_history_succeeds()
  {
    host.Reads.History =
    [
      new(Guid.NewGuid(), null, EmployeeApiTestHost.BranchA, DateTimeOffset.UtcNow.AddDays(-2),
        EmployeeBranchTransferReason.InitialAssignment, null, "hr-user"),
      new(Guid.NewGuid(), EmployeeApiTestHost.BranchA, EmployeeApiTestHost.BranchB, DateTimeOffset.UtcNow,
        EmployeeBranchTransferReason.Reorganisation, "consolidating", "hr-user")
    ];

    var response = await Send(
      HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}/branch-history", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    Assert.Equal(2, document.RootElement.GetArrayLength());
    Assert.Equal(
      JsonValueKind.Null, document.RootElement[0].GetProperty("sourceBranchId").ValueKind);
  }

  // ---- A BRANCH THE CALLER CAN NO LONGER REACH STILL APPEARS IN THE HISTORY.
  //
  // Access is authorized through the employee's CURRENT scope, once. Suppressing a past row would corrupt
  // the record rather than protect anything — the employee genuinely worked there.
  [Fact]
  public async Task A37_History_retains_a_branch_the_caller_can_no_longer_reach()
  {
    host.BranchAccess.Permitted = [EmployeeApiTestHost.BranchA];
    host.Reads.History =
    [
      new(Guid.NewGuid(), EmployeeApiTestHost.BranchC, EmployeeApiTestHost.BranchA, DateTimeOffset.UtcNow,
        EmployeeBranchTransferReason.Reorganisation, null, "hr-user")
    ];

    var response = await Send(
      HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}/branch-history", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Contains(
      EmployeeApiTestHost.BranchC.ToString(), await EmployeeApiTestHost.BodyAsync(response), StringComparison.Ordinal);
  }

  // The employee is out of scope, so the history is not reachable at all — the same NotFound the employee
  // read gives, so the two cannot be compared.
  [Fact]
  public async Task A38_History_of_an_out_of_scope_employee_is_not_exposed()
  {
    host.Reads.History = null;

    var response = await Send(
      HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}/branch-history", ViewToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("employee.not_found", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // REVOCATION — A39 to A42
  // ================================================================================================
  //
  // Nothing is cached from login. Each of these removes an authority and asserts the NEXT request over the
  // same host reflects it, which is what makes mid-session revocation real rather than documented.

  [Fact]
  public async Task A39_Revoking_company_access_fails_the_next_request()
  {
    var before = await Send(HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", ViewToken);
    Assert.Equal(HttpStatusCode.OK, before.StatusCode);

    host.CompanyContext.Error = new Error("Company.InvalidSelection", "revoked");

    var after = await Send(HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", ViewToken);

    Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    Assert.Equal("company.scope_denied", await EmployeeApiTestHost.ProblemCodeAsync(after));
  }

  [Fact]
  public async Task A40_Revoking_branch_access_fails_the_next_request()
  {
    var before = await Send(HttpMethod.Get, Route, ViewToken);
    Assert.Equal(HttpStatusCode.OK, before.StatusCode);

    host.CurrentBranch.Error = new Error("Branch.InvalidSelection", "revoked");

    var after = await Send(HttpMethod.Get, Route, ViewToken);

    Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    Assert.Equal("branch.scope_denied", await EmployeeApiTestHost.ProblemCodeAsync(after));
  }

  // An administrator's implicit scope is derived, not stored: when the authority goes, the widened set goes
  // with it, and the caller's own grants are all that remain.
  [Fact]
  public async Task A41_Revoking_administrator_authority_removes_the_implicit_scope()
  {
    host.BranchAccess.Permitted = [];

    var response = await Send(HttpMethod.Get, $"{Route}?branchScope=AllAuthorizedBranches", ViewToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("branch.scope_denied", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task A42_An_unauthenticated_request_is_rejected()
  {
    var response = await Send(HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", token: null);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ================================================================================================
  // ERROR SEMANTICS — A43 to A45
  // ================================================================================================

  // ---- EVERY COMPANY REFUSAL LOOKS THE SAME FROM OUTSIDE.
  //
  // Nonexistent, foreign-tenant, inactive and unassigned all reach the resolver as one error and leave as
  // one response. If any of them differed, the difference would be a probe.
  [Theory]
  [InlineData("Company.InvalidSelection")]
  public async Task A43_Company_denial_variants_are_externally_indistinguishable(string code)
  {
    var bodies = new List<string>();
    var statuses = new List<HttpStatusCode>();

    foreach (var _ in Enumerable.Range(0, 4))
    {
      host.CompanyContext.Error = new Error(code, "denied");

      var response = await Send(HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", ViewToken);

      statuses.Add(response.StatusCode);
      bodies.Add(Redact(await EmployeeApiTestHost.BodyAsync(response)));
    }

    Assert.Single(statuses.Distinct());
    Assert.Single(bodies.Distinct(StringComparer.Ordinal));
    Assert.Equal(HttpStatusCode.Forbidden, statuses[0]);

    // And it names nothing the caller may not see.
    Assert.DoesNotContain(EmployeeApiTestHost.CompanyB.ToString(), bodies[0], StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task A44_Branch_denial_variants_are_externally_indistinguishable()
  {
    host.BranchAccess.Permitted = [EmployeeApiTestHost.BranchA];

    var unauthorized = await Send(
      HttpMethod.Get,
      $"{Route}?branchScope=SelectedAuthorizedBranches&branchIds={EmployeeApiTestHost.BranchB}",
      ViewToken);

    var unknown = await Send(
      HttpMethod.Get,
      $"{Route}?branchScope=SelectedAuthorizedBranches&branchIds={Guid.NewGuid()}",
      ViewToken);

    Assert.Equal(unknown.StatusCode, unauthorized.StatusCode);
    Assert.Equal(
      Redact(await EmployeeApiTestHost.BodyAsync(unknown)),
      Redact(await EmployeeApiTestHost.BodyAsync(unauthorized)));

    Assert.DoesNotContain(
      EmployeeApiTestHost.BranchB.ToString(),
      await EmployeeApiTestHost.BodyAsync(unauthorized),
      StringComparison.OrdinalIgnoreCase);
  }

  // ---- AND A REAL OUTAGE IS STILL AN OUTAGE.
  //
  // The authorization split would be worthless in the other direction: if a routing failure became 403, a
  // caller would be told they lack permission for a database that is simply unreachable, and nobody would
  // be paged.
  [Fact]
  public async Task A45_A_real_storage_failure_is_not_mapped_to_an_authorization_refusal()
  {
    host.UnitOfWork.Failure = new Error("TenantStorage.Unavailable", "no route to the tenant database");

    var response = await Send(
      HttpMethod.Put, $"{Route}/{EmployeeApiTestHost.EmployeeId}", UpdateToken, ValidUpdateBody);

    // A SERVER answer, not a 4xx. The caller is not being told they lack permission for a database that is
    // simply unreachable, and an operator still gets paged.
    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

    // ---- AND THE INTERNAL DETAIL DOES NOT TRAVEL.
    //
    // Asserted on the RAW body rather than a parsed code, because a 500 in this configuration may carry no
    // ProblemDetails body at all — and "nothing leaked" has to hold either way.
    var payload = await EmployeeApiTestHost.BodyAsync(response);
    Assert.DoesNotContain("tenant database", payload, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("TenantStorage", payload, StringComparison.Ordinal);
    Assert.DoesNotContain("scope_denied", payload, StringComparison.Ordinal);
  }

  private const string ValidCreateBody = """
    {"employeeNumber":"EMP-00147","fullName":"Layla Haddad","employmentDate":"2026-03-01T00:00:00+00:00","nationalId":"2990112345678","departmentId":"88888888-8888-8888-8888-888888888888","positionId":"99999999-9999-9999-9999-999999999999"}
    """;

  // The literal is EmployeeApiTestHost.DepartmentA, spelled out for the same reason ValidCreateBody spells
  // out its own: a const body reads as the wire payload it is.
  private const string ChangeDepartmentBody = """
    {"departmentId":"bbbbbbbb-0000-0000-0000-bbbbbbbbbbbb","expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private const string ValidUpdateBody = """
    {"fullName":"Layla Haddad-Nasr","nationalId":"2990112345678","expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private const string ValidTerminateBody = """
    {"terminationDate":"2027-01-31T00:00:00+00:00","reasonCode":"Resignation","expectedRowVersion":"AAAAAAAAB9E="}
    """;

  // ================================================================================================
  // THE DEPARTMENT SUB-OBJECT AND THE DEPARTMENT FILTER — A46 to A51
  // ================================================================================================
  //
  // Both were specified by FP-007 and never reached the wire, and they failed in opposite ways: the
  // sub-object was never built at all, while the FILTER was built end to end BELOW transport and left
  // unreachable because its name was missing from the query allowlist. The second is the one worth a test
  // that says so — a capability can be fully implemented, fully tested, and still be dead.

  // ---- THE DETAIL CARRIES THE DEPARTMENT, RESOLVED.
  //
  // Identifier AND code AND name, all three asserted: the identifier alone was already reachable through
  // other routes, so a test that checked only that would pass against the shape this fix replaced.
  [Fact]
  public async Task A46_The_employee_detail_carries_its_department()
  {
    var response = await Send(
      HttpMethod.Get, $"{Route}/{EmployeeApiTestHost.EmployeeId}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));

    var department = document.RootElement.GetProperty("department");

    Assert.Equal(EmployeeApiTestHost.DepartmentA, department.GetProperty("departmentId").GetGuid());
    Assert.Equal("FIN", department.GetProperty("code").GetString());
    Assert.Equal("Finance", department.GetProperty("name").GetString());
  }

  // ================================================================================================
  // ⚠ A BAD PAGE NUMBER AND A BAD PAGE SIZE ARE DISTINGUISHABLE ON THE WIRE (T-260).
  // ================================================================================================
  //
  // Both used to answer `request.invalid` — the same code a malformed body, an unknown property and a
  // stale row version get. **A paging client that fixed the wrong parameter retried and failed
  // identically**, which is the argument that made a malformed identifier a 400 rather than a 404.
  //
  // ⚠ **AND THE DOMAIN SPLIT ALONE WOULD HAVE BEEN INVISIBLE HERE.** The problem document carries
  // `code`, `correlationId` and `resourceKey` and **no message field**, so `Error.Message` never reaches
  // a caller. The wire code is the entire channel — which is why this asserts the CODE and not the
  // status: both are 400, and a test that checked only the status would pass against the old behaviour.
  [Theory]
  [InlineData("?pageNumber=0", "request.page_number_invalid")]
  [InlineData("?pageSize=0", "request.page_size_invalid")]
  [InlineData("?pageSize=99999", "request.page_size_invalid")]
  public async Task An_out_of_range_page_names_the_parameter_at_fault(string query, string expected)
  {
    var response = await Send(HttpMethod.Get, Route + query, ViewToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));
    Assert.Equal(expected, document.RootElement.GetProperty("code").GetString());
  }

  // ---- AND SO DOES EVERY LIST ROW.
  [Fact]
  public async Task A47_A_search_result_row_carries_its_department()
  {
    host.Reads.Page = [StubEmployeeReads.SampleSummary()];

    var response = await Send(HttpMethod.Get, Route, ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await EmployeeApiTestHost.BodyAsync(response));

    var department = document.RootElement
      .GetProperty("items")[0]
      .GetProperty("department");

    Assert.Equal(EmployeeApiTestHost.DepartmentA, department.GetProperty("departmentId").GetGuid());
    Assert.Equal("FIN", department.GetProperty("code").GetString());
    Assert.Equal("Finance", department.GetProperty("name").GetString());
  }

  // ---- THE FILTER REACHES THE CRITERIA (FR-DEP-0111).
  //
  // Asserted on the criteria the read service received rather than on the rows returned, because the stub
  // does not filter: what this proves is that transport now CARRIES the value, which is precisely what was
  // missing. That the SQL then honours it is proven in `EmployeeBoundarySqlServerTests`.
  [Fact]
  public async Task A48_Search_accepts_a_department_filter()
  {
    var response = await Send(
      HttpMethod.Get, $"{Route}?departmentId={EmployeeApiTestHost.DepartmentA}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(EmployeeApiTestHost.DepartmentA, host.Reads.LastCriteria!.DepartmentId);
  }

  // ---- AND COMBINES WITH THE OTHERS RATHER THAN REPLACING THEM.
  //
  // Every filter here narrows; none widens. Sending the department beside a status and a branch scope and
  // finding all three on the criteria is what rules out a parser that assigns the last one it recognises.
  [Fact]
  public async Task A49_A_department_filter_combines_with_the_other_filters()
  {
    var response = await Send(
      HttpMethod.Get,
      $"{Route}?departmentId={EmployeeApiTestHost.DepartmentA}&status=Terminated" +
        "&branchScope=AllAuthorizedBranches&pageSize=25",
      ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var criteria = host.Reads.LastCriteria!;

    Assert.Equal(EmployeeApiTestHost.DepartmentA, criteria.DepartmentId);
    Assert.Equal([EmployeeStatus.Terminated], criteria.Statuses);
    Assert.Equal(25, criteria.PageSize);
    Assert.Equal(
      [EmployeeApiTestHost.BranchA, EmployeeApiTestHost.BranchB],
      host.Reads.LastScope!.Branches.BranchIds);
  }

  // ---- A MALFORMED IDENTIFIER IS REFUSED, NOT TREATED AS "NO FILTER".
  //
  // Ignoring it would answer a question the caller did not ask — an unfiltered page — while looking like a
  // department with no members.
  [Fact]
  public async Task A50_A_malformed_department_filter_is_a_validation_failure()
  {
    var response = await Send(HttpMethod.Get, $"{Route}?departmentId=not-a-guid", ViewToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE ALLOWLIST IS STILL AN ALLOWLIST.
  //
  // The fix for `departmentId` was to add one NAME to the permitted set, so the risk it creates is that the
  // set stopped being closed. A near-miss name — plausible, adjacent, and not on the list — must still be
  // refused, or the discipline was traded away rather than extended.
  [Theory]
  [InlineData("departmentIds")]
  [InlineData("departmentName")]
  [InlineData("department")]
  public async Task A51_An_undeclared_query_parameter_is_still_refused(string parameter)
  {
    var response = await Send(
      HttpMethod.Get, $"{Route}?{parameter}={EmployeeApiTestHost.DepartmentA}", ViewToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  private static readonly string ValidTransferBody = $$"""
    {"destinationBranchId":"{{EmployeeApiTestHost.BranchB}}","reasonCode":"Reorganisation","reasonText":"consolidating","expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private string ViewToken => host.TokenWith(HrPermissionNames.ViewEmployees);

  private string CreateToken => host.TokenWith(HrPermissionNames.CreateEmployees, HrPermissionNames.ViewEmployees);

  private string UpdateToken => host.TokenWith(HrPermissionNames.UpdateEmployees, HrPermissionNames.ViewEmployees);

  private string TerminateToken =>
    host.TokenWith(HrPermissionNames.TerminateEmployees, HrPermissionNames.ViewEmployees);

  private string TransferToken =>
    host.TokenWith(HrPermissionNames.TransferEmployees, HrPermissionNames.ViewEmployees);

  private Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, string? body = null) =>
    host.Client.SendAsync(EmployeeApiTestHost.Request(method, path, token, body));

  // The correlation id differs per request by design, so comparing raw bodies would always differ. Removing
  // it is what makes "identical" mean identical in every part the caller could learn from.
  private static string Redact(string body) =>
    System.Text.RegularExpressions.Regex.Replace(body, "\"correlationId\":\"[^\"]*\"", "\"correlationId\":\"*\"");
}
