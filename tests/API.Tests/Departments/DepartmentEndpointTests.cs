using System.Net;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;
using SSAS.Platform.Domain;

namespace SSAS.API.Tests.Departments;

// ==================================================================================================
// THE DEPARTMENT HTTP CONTRACT, PROVEN OVER REAL HTTP (FP-007 Phase 4).
// ==================================================================================================
//
// These answer a question the application and SQL tests cannot: what a CALLER sees. The same refusal that
// Integration.Tests proves correct in the database has to arrive as the right status code and the right
// problem code — and for the two unique-constraint contexts, as a DIFFERENT code depending on which
// operation raised it, which is the defect this surface was created to fix.
[Collection(DepartmentApiEndpointGroup.Name)]
public sealed class DepartmentEndpointTests : IClassFixture<DepartmentApiTestHost>
{
  private const string Route = "/api/hr/departments";

  private readonly DepartmentApiTestHost host;

  public DepartmentEndpointTests(DepartmentApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ================================================================================================
  // CREATE
  // ================================================================================================

  [Fact]
  public async Task D1_Authorized_create_succeeds()
  {
    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
  }

  [Fact]
  public async Task D2_Create_without_a_token_is_unauthorized()
  {
    var response = await Send(HttpMethod.Post, Route, token: null, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task D3_Create_without_the_create_permission_is_forbidden()
  {
    var response = await Send(HttpMethod.Post, Route, ViewToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- PERMISSION BLEED. Holding a DIFFERENT HR permission is not holding this one.
  //
  // The failure this guards against is a route wired to the wrong constant, or a permission check that
  // passes on "any HR permission". Employee permissions are the sharpest probe: they are in the same
  // module and the same token shape, so nothing about the request looks unusual.
  [Fact]
  public async Task D4_Create_with_an_unrelated_hr_permission_is_forbidden()
  {
    var token = host.TokenWith(
      HrPermissionNames.CreateEmployees,
      HrPermissionNames.UpdateEmployees,
      HrPermissionNames.TransferEmployees);

    var response = await Send(HttpMethod.Post, Route, token, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task D5_Create_rejects_an_undeclared_field()
  {
    const string body = """
      {"code":"FIN","name":"Finance","companyId":"22222222-2222-2222-2222-222222222222"}
      """;

    var response = await Send(HttpMethod.Post, Route, CreateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE FIRST OF THE TWO UNIQUE-CONSTRAINT CONTEXTS.
  //
  // On create, Persistence.UniqueConstraint means the unique index on NormalizedCode had the last word —
  // the same answer the pre-check gives, so a race and a sequential duplicate are indistinguishable.
  [Fact]
  public async Task D6_A_unique_constraint_on_create_is_a_code_conflict()
  {
    host.UnitOfWork.Failure = IdentityAccessErrors.UniqueConstraintViolation;

    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("department.code_conflict", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D7_A_duplicate_code_conflicts()
  {
    host.Repository.CodeExists = true;

    var response = await Send(HttpMethod.Post, Route, CreateToken, ValidCreateBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("department.code_conflict", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // READ
  // ================================================================================================

  [Fact]
  public async Task D8_Authorized_get_succeeds()
  {
    var response = await Send(HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task D9_Get_without_the_view_permission_is_forbidden()
  {
    var token = host.TokenWith(HrPermissionNames.CreateDepartments);

    var response = await Send(HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}", token);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // Scoped absence. Unknown, another tenant's, another company's and out-of-scope are one answer.
  [Fact]
  public async Task D10_An_out_of_scope_department_is_not_found()
  {
    host.Reads.DetailError = DepartmentErrors.NotFound;

    var response = await Send(HttpMethod.Get, $"{Route}/{Guid.NewGuid()}", ViewToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("department.not_found", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D11_Search_succeeds()
  {
    var response = await Send(HttpMethod.Get, Route, ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // An unparseable page size is a malformed request, not a reason to substitute a default.
  [Fact]
  public async Task D12_Search_with_an_unparseable_page_size_is_rejected()
  {
    var response = await Send(HttpMethod.Get, $"{Route}?pageSize=many", ViewToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D13_Children_succeeds()
  {
    var response = await Send(
      HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}/children", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task D14_Children_of_an_out_of_scope_department_is_not_found()
  {
    host.Reads.Children = null;

    var response = await Send(HttpMethod.Get, $"{Route}/{Guid.NewGuid()}/children", ViewToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("department.not_found", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // UPDATE AND HIERARCHY
  // ================================================================================================

  [Fact]
  public async Task D15_Authorized_update_succeeds()
  {
    var response = await Send(
      HttpMethod.Put, $"{Route}/{DepartmentApiTestHost.DepartmentId}", UpdateToken, ValidUpdateBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task D16_Update_without_the_update_permission_is_forbidden()
  {
    var response = await Send(
      HttpMethod.Put, $"{Route}/{DepartmentApiTestHost.DepartmentId}", ViewToken, ValidUpdateBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // An ordinary update cannot express a hierarchy move: parentDepartmentId is not a declared field.
  [Fact]
  public async Task D17_Update_cannot_express_a_hierarchy_move()
  {
    const string body = """
      {"code":"FIN","name":"Finance","parentDepartmentId":"cccccccc-cccc-cccc-cccc-cccccccccccc","expectedRowVersion":"AAAAAAAAB9E="}
      """;

    var response = await Send(
      HttpMethod.Put, $"{Route}/{DepartmentApiTestHost.DepartmentId}", UpdateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D18_An_invalid_row_version_is_rejected()
  {
    const string body = """
      {"code":"FIN","name":"Finance","expectedRowVersion":"not-base64"}
      """;

    var response = await Send(
      HttpMethod.Put, $"{Route}/{DepartmentApiTestHost.DepartmentId}", UpdateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  // ---- MOVE AND MOVE-TO-ROOT ARE SEPARATE ROUTES.
  //
  // A null parent on the move route is NOT "become a root": that is the other route, and accepting null
  // here would make the most destructive reading of the field the quiet one.
  [Fact]
  public async Task D19_Move_requires_a_parent()
  {
    const string body = """{"parentDepartmentId":null,"expectedRowVersion":"AAAAAAAAB9E="}""";

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/move", UpdateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task D20_Move_to_root_succeeds()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/move-to-root", UpdateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task D21_Move_to_root_without_the_update_permission_is_forbidden()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/move-to-root", ViewToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ================================================================================================
  // MANAGER
  // ================================================================================================

  [Fact]
  public async Task D22_Assign_manager_without_the_update_permission_is_forbidden()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager", ViewToken,
      ValidAssignManagerBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- THE SECOND UNIQUE-CONSTRAINT CONTEXT, AND THE POINT OF THE WHOLE MAPPER.
  //
  // The SAME persistence error that means "code already taken" on create means something else entirely
  // here: the only unique constraint on this route is PK_DepartmentManagers, so a violation means another
  // caller seated a manager first. Answering department.code_conflict — which is what routing this
  // through the employee mapper produced — would name a conflict on a field this request never sent.
  //
  // It must be indistinguishable from a stale rowversion, because both mean "somebody got there first".
  [Fact]
  public async Task D23_A_unique_constraint_on_assign_manager_is_a_concurrency_conflict()
  {
    host.UnitOfWork.Failure = IdentityAccessErrors.UniqueConstraintViolation;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager", UpdateToken,
      ValidAssignManagerBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("concurrency.conflict", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // And the rowversion loser gets the identical answer, which is what "indistinguishable" means.
  [Fact]
  public async Task D24_A_rowversion_conflict_on_assign_manager_answers_identically()
  {
    host.UnitOfWork.Failure = IdentityAccessErrors.ConcurrencyConflict;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager", UpdateToken,
      ValidAssignManagerBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("concurrency.conflict", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D25_Remove_manager_without_an_assignment_is_refused()
  {
    host.Repository.Manager = null;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager/remove", UpdateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("department.manager_invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // LIFECYCLE — BOTH DIRECTIONS CARRY THE DEACTIVATE PERMISSION
  // ================================================================================================

  [Fact]
  public async Task D26_Deactivate_succeeds_with_the_deactivate_permission()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/deactivate", DeactivateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ---- THE RULING, ENFORCED.
  //
  // Update authority is NOT enough to reopen a department. That permission governs whether a department
  // may receive employees, and a caller who may only rename one must not be able to undo a closure someone
  // with the sensitive permission deliberately made.
  [Fact]
  public async Task D27_Activate_is_forbidden_with_only_the_update_permission()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/activate", UpdateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task D28_Deactivate_is_forbidden_with_only_the_update_permission()
  {
    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/deactivate", UpdateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task D29_Deactivating_a_department_with_active_children_is_refused()
  {
    host.Repository.HasActiveChildren = true;

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/deactivate", DeactivateToken,
      ValidRowVersionBody);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("department.transition_invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // COMPANY CONTEXT
  // ================================================================================================

  // ---- THE HEADER'S SYNTAX IS THE CALLER'S PROBLEM; ITS SCOPE IS NOT THEIR BUSINESS.
  //
  // A missing or malformed X-Company-Id is a MALFORMED REQUEST: the caller can already see their own
  // header, so saying so discloses nothing, and a generic denial would send them hunting a permissions
  // problem they do not have.
  //
  // Driven through the establisher's error rather than by omitting the header, following the employee
  // suite's convention — the stub establisher does not read the header, and the REAL one's five-step
  // validation is proven against live state in Integration.Tests. What this layer owns, and what is
  // asserted here, is the answer that reaches the caller.
  [Theory]
  [InlineData("Company.SelectionRequired")]
  [InlineData("Company.InvalidSelectionFormat")]
  public async Task D30_A_missing_or_malformed_company_header_is_a_validation_failure(string code)
  {
    host.CompanyContext.Error = new SSAS.BuildingBlocks.Domain.Error(code, "malformed");

    var response = await Send(HttpMethod.Get, Route, ViewToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  [Fact]
  public async Task D31_An_unauthorized_company_is_denied()
  {
    host.CompanyAccess.Permitted = [];
    host.CompanyContext.Error = new SSAS.BuildingBlocks.Domain.Error("Company.InvalidSelection", "denied");

    var response = await Send(HttpMethod.Get, Route, ViewToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("company.scope_denied", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE MANAGER'S THREE STATES SURVIVE THE WIRE.
  //
  // "Assigned but undisclosed" must not collapse into "no manager": the first says the caller may not see
  // who, the second says there is nobody. A department is company-visible while employees are
  // branch-scoped, so the distinction is reachable by ordinary callers.
  [Fact]
  public async Task D32_An_undisclosed_manager_is_reported_as_assigned_without_an_identity()
  {
    host.Reads.Detail = StubDepartmentReads.SampleDetail(
      manager: DepartmentManagerSummary.Undisclosed());

    var response = await Send(HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await DepartmentApiTestHost.BodyAsync(response);

    Assert.Contains("\"isAssigned\":true", body, StringComparison.Ordinal);
    Assert.Contains("\"employeeId\":null", body, StringComparison.Ordinal);
    Assert.DoesNotContain("\"fullName\":\"", body, StringComparison.Ordinal);
  }

  private const string ValidCreateBody = """
    {"code":"FIN","name":"Finance"}
    """;

  private const string ValidUpdateBody = """
    {"code":"FIN","name":"Finance Renamed","expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private const string ValidRowVersionBody = """
    {"expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private const string ValidAssignManagerBody = """
    {"employeeId":"dddddddd-dddd-dddd-dddd-dddddddddddd","expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private string ViewToken => host.TokenWith(HrPermissionNames.ViewDepartments);

  private string CreateToken =>
    host.TokenWith(HrPermissionNames.CreateDepartments, HrPermissionNames.ViewDepartments);

  private string UpdateToken =>
    host.TokenWith(HrPermissionNames.UpdateDepartments, HrPermissionNames.ViewDepartments);

  private string DeactivateToken =>
    host.TokenWith(HrPermissionNames.DeactivateDepartments, HrPermissionNames.ViewDepartments);

  private Task<HttpResponseMessage> Send(
    HttpMethod method, string path, string? token, string? body = null) =>
    host.Client.SendAsync(DepartmentApiTestHost.Request(method, path, token, body));
}
