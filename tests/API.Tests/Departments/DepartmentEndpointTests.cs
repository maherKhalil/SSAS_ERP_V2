using System.Net;
using System.Text.Json;
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
  // ⚠ CITED BY 265: `AC-DEP-0002`, the `companyId` half. This test was the UNCITED evidence that showed the
  // criterion was wrong — the behaviour was already asserted; it was the specification that was stale.
  // Paired with `D5b`, which carries `tenantId`: the two are independent entries in a per-call-site
  // allowlist, so neither covers the other.
  [Trait("Criterion", "AC-DEP-0002")]
  public async Task D5_Create_rejects_an_undeclared_field()
  {
    const string body = """
      {"code":"FIN","name":"Finance","companyId":"22222222-2222-2222-2222-222222222222"}
      """;

    var response = await Send(HttpMethod.Post, Route, CreateToken, body);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE SAME RULE FOR `tenantId`, AND A SEPARATE TEST BECAUSE THE ALLOWLIST IS PER-NAME (265).
  //
  // ⚠ `ReadStrictJsonAsync` DOES NOT DERIVE THE ACCEPTED MEMBERS FROM `CreateDepartmentRequest`. It takes an
  // explicit `fields` DICTIONARY built at the call site -- currently code / name / parentDepartmentId in
  // `DepartmentEndpointRouteBuilderExtensions` -- and rejects any name absent from it. So `companyId` and
  // `tenantId` are TWO INDEPENDENT ENTRIES in an allowlist, not two faces of one derived rule: adding
  // `tenantId` to that dictionary would bind and honour it WHILE `D5`, WHICH WITNESSES `companyId` ONLY,
  // STAYS GREEN. A shared mechanism argues against a second test only when the subjects cannot diverge.
  //
  // The value names a DIFFERENT tenant from the host's own, so what is refused is the cross-tenant
  // assertion the criterion is about and not merely a malformed field.
  //
  // ⚠ CITED BY 265 ONLY AFTER THE CRITERION WAS CORRECTED (`b679214`). `AC-DEP-0002` used to say body
  // identifiers are *IGNORED, NOT HONOURED* and that such a request *produces a department in the caller's
  // own tenant* — and the product does NEITHER, it REFUSES with 400. Citing it then would have attached a
  // criterion to a test contradicting its stated behaviour. `AC-DEP-0035`, in the SAME document, stated the
  // opposite disposition for the same class of undeclared field and the code implements 0035's rule: the
  // specification disagreed with itself and the implementation picked the safer side.
  [Fact]
  [Trait("Criterion", "AC-DEP-0002")]
  public async Task D5b_Create_rejects_an_undeclared_tenant_id()
  {
    const string body = """
      {"code":"FIN","name":"Finance","tenantId":"33333333-3333-3333-3333-333333333333"}
      """;

    var response = await Send(HttpMethod.Post, Route, CreateToken, body);

    // `request.invalid` rather than only the status: a 400 could also be a missing required field, and
    // `code` and `name` are both present here precisely so that route to 400 is closed.
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await DepartmentApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE FIRST OF THE TWO UNIQUE-CONSTRAINT CONTEXTS.
  //
  // On create, Persistence.UniqueConstraint means the unique index on NormalizedCode had the last word —
  // the same answer the pre-check gives, so a race and a sequential duplicate are indistinguishable.
  [Fact]
  public async Task D6_A_unique_constraint_violation_on_create_maps_to_a_code_conflict()
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
  public async Task D23_A_unique_constraint_violation_on_assign_manager_maps_to_a_concurrency_conflict()
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
  // CITED BY B18 pass 20 as the only assertion of `AC-DEP-0048`'s `409` clause -- for ONE mutation.
  // The criterion says every department mutation; the other six map their concurrency conflict
  // nowhere that a test reads.
  [Trait("Criterion", "AC-DEP-0048")]
  public async Task D24_A_concurrency_conflict_on_assign_manager_maps_identically()
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
  // ⚠⚠⚠ `OD-DEP-003` READING (i) — THE DEPARTMENTAL SELF-MANAGEMENT BAN. RED ON PURPOSE (2026-09-06).
  // ================================================================================================
  //
  // The owner CLOSED `OD-DEP-003` on 2026-08-20 adopting reading (iii) (`decisions-approved.md:27`), and
  // (iii) is *"(i) now, (ii) when a reporting line is introduced"* (`README.md:213`). Reading (i) is
  // *"An employee may not be the manager of the department they themselves belong to"*, marked
  // **enforceable in FP-007 — Yes, fully**. `AC-DEP-0023` states it in BOTH directions.
  //
  // ⚠⚠ THE PRODUCT DOES NOT ENFORCE IT AND SAYS SO IN WRITING. `DepartmentManagerCommandHandlers.cs:68-70`:
  // *"DEPARTMENT MEMBERSHIP IS NOT CONSULTED EITHER, in either direction… `Employee.DepartmentId ==
  // Department.Id` is explicitly NOT a rule."* **That comment and the code it describes are ONE commit —
  // `245f64b`, 2026-08-20 16:18 — and the ruling reached the repository at `4a84e7d`, 2026-08-21 05:03.**
  // *So the argument predates the decision's recording, and `git log` on that file returns exactly one
  // commit: it has never been reopened. This is a stale position, not a live dissent.*
  //
  // ***WRITTEN RED AND VERIFIED RED BEFORE THE FIX EXISTED*** — it failed at the `Assert.NotEqual` below with
  // *Expected: Not OK / Actual: OK*, because the assignment SUCCEEDED and seated the manager. **The fix
  // (`DepartmentManagerCommandHandlers`, `employee.DepartmentId == department.Id` →
  // `DepartmentErrors.ManagerInOwnDepartment`) turned it green.** *Recorded because a guard that has never
  // failed is indistinguishable from one that cannot.*
  //
  // ⚠ IT ASSERTS BEHAVIOUR AND DELIBERATELY NOT A PROBLEM CODE. The manager refusals collapse to one wire
  // code on purpose — `DepartmentApiErrorMapper` keeps *nonexistent*, *another company's*, *terminated* and
  // now *own department* all as `department.manager_invalid`, so a department caller cannot probe the
  // employee set. Asserting that code here would therefore prove almost nothing about WHICH rule fired.
  // What is asserted is what the ruling requires: the assignment does not succeed, and no manager is seated.
  [Fact]
  public async Task An_employee_cannot_be_made_manager_of_the_department_they_belong_to()
  {
    // ⚠⚠⚠ THE EMPLOYEE IS STAMPED INTO THE DEPARTMENT THE HANDLER *LOADS*, NOT THE ONE THE ROUTE NAMES.
    // The first version of this test used `DepartmentApiTestHost.DepartmentId` — the id in the URL — and
    // the stub repository ignores the requested id and returns a `Department` whose `Id` came from
    // `Department.Create`, i.e. a fresh `Guid` per reset. **The two arms of this pair were therefore the
    // SAME arrangement and neither employee was ever a member**, so the guard stayed red after the fix and
    // the companion proved nothing. *A route parameter and an aggregate's identity are different things
    // here, and only the second reaches `employee.DepartmentId == department.Id`.*
    //
    // Everything else is the seeded ELIGIBLE state — same tenant, same company, not terminated — so
    // membership is the only difference between this arrangement and the companion below.
    host.EmployeeRepository.Employee = EmployeeInDepartment(host.Repository.Department!.Id);

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager", UpdateToken,
      ValidAssignManagerBody);

    Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    Assert.Null(host.Repository.Manager);
  }

  // ---- THE ANTI-VACUITY COMPANION, AND IT IS NOT OPTIONAL.
  //
  // A handler that refused EVERY assignment would satisfy the guard above completely while destroying the
  // feature. This is the same arrangement with ONE field changed, and it must keep passing — before the
  // fix and after it.
  [Fact]
  public async Task An_employee_in_another_department_may_still_be_made_manager()
  {
    // ⚠ THE ARRANGEMENT IS ASSERTED, NOT ASSUMED. The employee must be in a department that is NOT the one
    // the handler loads, and relying on a constant failing to collide with a freshly generated `Guid` would
    // leave this test's meaning resting on a fact nobody wrote down — which is precisely the defect its
    // twin above carried. The inequality is therefore stated before the act.
    var elsewhere = Employees.EmployeeApiTestHost.DepartmentA;
    Assert.NotEqual(host.Repository.Department!.Id, elsewhere);

    host.EmployeeRepository.Employee = EmployeeInDepartment(elsewhere);

    var response = await Send(
      HttpMethod.Post, $"{Route}/{DepartmentApiTestHost.DepartmentId}/manager", UpdateToken,
      ValidAssignManagerBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.NotNull(host.Repository.Manager);
  }

  // ================================================================================================
  // ⚠⚠⚠ THE OTHER HALF OF READING (i) IS NOT ENFORCED, AND THIS FILE MUST NOT BE READ AS IF IT WERE.
  // ================================================================================================
  //
  // `AC-DEP-0023` and `OD-DEP-003` reading (i) are a STATE INVARIANT — *"an employee may not BE the manager
  // of the department they themselves belong to"* — and **a state has two routes into it**:
  //
  //     ***ASSIGN*** — make a member of this department its manager   ***CLOSED***, by the pair above
  //     ***MOVE***   — move the manager into the department they head ***NOT ENFORCED***
  //
  // ***A READER WHO MEETS THE FIXED ASSIGN PATH WILL CONCLUDE THE RULE IS ENFORCED. IT IS HALF ENFORCED,
  // AND HALF A STATE INVARIANT IS NOT A WITNESS FOR IT*** — which is why neither test above carries a
  // `Criterion` trait for `AC-DEP-0023` and why this file does not cite it.
  //
  // ⚠ WHY THE MOVE GUARD IS ABSENT RATHER THAN FAILING. `ChangeEmployeeDepartmentCommandHandler` **cannot
  // learn who manages a department**: its constructor is `(IEmployeeRepository, ITenantUnitOfWork,
  // ICurrentTenant, ICurrentCompany, ICurrentUser, IDateTimeProvider)` — no `IDepartmentRepository` — and
  // `IEmployeeRepository`'s entire surface (`GetByIdAsync`, the two `Exists` probes, the four
  // `FindAssignable…` lookups) carries **no manager read at all**. Its destination check runs through
  // `CreateEmployeeCommandHandler.ValidateDepartmentAsync(employees, …)`, which answers company-and-active
  // and nothing else. ***So a test written today could assert "this move is refused" but could NOT express
  // "this employee manages the destination" — a guard demanding that EVERY move be refused. That is the
  // fixture dictating the claim, and it was not written for that reason.***
  //
  // ⚠⚠ IT IS DEFERRED, NOT FORGOTTEN, AND THE REASON IS DATA. The assign fix can only prevent NEW
  // violations; the move fix would refuse an operation on rows nobody edited. **No unique index, check
  // constraint or foreign key ties `DepartmentManagers.EmployeeId` to `Employees.DepartmentId`, so a
  // department whose current manager belongs to it is representable and may already exist.** *The owner
  // holds that question, and the seam — a new dependency, or a manager read beside
  // `FindAssignableDepartmentAsync` — is a design decision recorded as open rather than taken quietly.*

  // Builds the seeded eligible employee, stamped into a NAMED department. `StampInitialAssignment` refuses
  // a second call, so the department has to be chosen at construction rather than changed afterwards.
  private static SSAS.HR.Domain.Employees.Employee EmployeeInDepartment(Guid departmentId)
  {
    var employee = SSAS.HR.Domain.Employees.Employee.Create(
      SSAS.HR.Domain.Employees.EmployeeNumber.Create("EMP-00147").Value,
      SSAS.HR.Domain.Employees.EmployeeFullName.Create("Layla Haddad").Value,
      null,
      new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
      "hr-user",
      Guid.NewGuid(),
      new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)).Value;

    employee.TenantId = DepartmentApiTestHost.TenantId;
    employee.CompanyId = DepartmentApiTestHost.CompanyA;
    employee.BranchId = DepartmentApiTestHost.BranchA;

    employee.StampInitialAssignment(
      DepartmentApiTestHost.TenantId,
      DepartmentApiTestHost.CompanyA,
      DepartmentApiTestHost.BranchA,
      departmentId,
      Employees.EmployeeApiTestHost.PositionA,
      "seed",
      Guid.NewGuid(),
      new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

    return employee;
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
  // CITED BY B18 pass 21: `AC-DEP-0027` at the API layer, mapping the refusal to a conflict. See
  // `Deactivation_is_refused_while_an_active_child_remains` in the department SQL suite.
  [Trait("Criterion", "AC-DEP-0027")]
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
  // ---- ⚠ AND THE TWO CONDITIONS ANSWER DIFFERENTLY SINCE T-268, DELIBERATELY.
  //
  // Both arrive by the same route and look alike, which is why they are asserted side by side rather than
  // in separate tests. Only one of them is a **precondition**:
  //
  //   `Company.InvalidSelectionFormat` -> `request.invalid`
  //       The header is malformed. **Fix your input and try again** -- an ordinary validation failure,
  //       and one of the 129 domain codes that collapse into the generic code.
  //
  //   `Company.SelectionRequired`      -> `company.selection_required`
  //       The header is absent and no company is selected. **You are not in a state where this request
  //       means anything**: the remedy is a DIFFERENT call -- select a company -- and then this same
  //       request unchanged. A client that cannot tell it from a malformed field cannot offer the picker.
  //
  // Both stay 400. Each is a client error, and the actionable difference is carried by the code, because
  // **the status is the category and the code is the instruction.** A reader who finds this assertion
  // changed should be able to see from here that it was intended.
  [Theory]
  [InlineData("Company.SelectionRequired", "company.selection_required")]
  [InlineData("Company.InvalidSelectionFormat", "request.invalid")]
  public async Task D30_A_missing_company_selection_is_a_precondition_and_a_malformed_one_is_not(
    string code, string expectedWireCode)
  {
    host.CompanyContext.Error = new SSAS.BuildingBlocks.Domain.Error(code, "malformed");

    var response = await Send(HttpMethod.Get, Route, ViewToken);

    // Still 400 for both -- the category did not change, only the instruction.
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal(expectedWireCode, await DepartmentApiTestHost.ProblemCodeAsync(response));
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

  // ================================================================================================
  // employeeCount — THE FP-007 FIELD THAT NEVER SHIPPED (api-contracts, DEC-POS-0034)
  // ================================================================================================
  //
  // Specified by FP-007, absent from the implementation, and marked "matched" by FP-007's own as-built
  // pass. These four tests are what makes the claim checkable rather than asserted, and they are written
  // around the one distinction the field is easy to get wrong: ZERO AND NULL ARE DIFFERENT ANSWERS.
  //
  //   0    — the caller can read employees and this department has none they can see;
  //   null — the caller cannot read employees at all, so no number would be honest.
  //
  // Both are seeded to the SAME stub value where it matters, so a test passing by accident — because the
  // stub was never reached and returned its default — is not possible.

  // ---- WITH AN EMPLOYEE SCOPE, THE COUNT IS A NUMBER.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D33_EmployeeCount_is_a_number_for_a_caller_who_can_read_employees()
  {
    host.EmployeeReads.DepartmentMemberCount = 12;

    var response = await Send(
      HttpMethod.Get,
      $"{Route}/{DepartmentApiTestHost.DepartmentId}",
      host.TokenWith(HrPermissionNames.ViewDepartments, HrPermissionNames.ViewEmployees));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await DepartmentApiTestHost.BodyAsync(response));

    Assert.Equal(12, document.RootElement.GetProperty("employeeCount").GetInt32());
  }

  // ---- ZERO IS A NUMBER, NOT AN ABSENCE.
  //
  // An empty department read by a caller who CAN read employees answers 0 — and the assertion checks the
  // JSON value KIND as well as the value, because `GetInt32()` on a null would throw rather than report the
  // difference this test exists to pin down.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D34_EmployeeCount_is_zero_for_an_empty_department_and_zero_is_not_null()
  {
    host.EmployeeReads.DepartmentMemberCount = 0;

    var response = await Send(
      HttpMethod.Get,
      $"{Route}/{DepartmentApiTestHost.DepartmentId}",
      host.TokenWith(HrPermissionNames.ViewDepartments, HrPermissionNames.ViewEmployees));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await DepartmentApiTestHost.BodyAsync(response));

    var count = document.RootElement.GetProperty("employeeCount");

    Assert.Equal(JsonValueKind.Number, count.ValueKind);
    Assert.Equal(0, count.GetInt32());
  }

  // ---- WITHOUT ONE, IT IS NULL — PRESENT AND NULL, NOT ABSENT.
  //
  // Both halves are asserted separately: the property must EXIST so the JSON shape is stable across
  // callers, and its value must be null rather than 0. The stub is seeded to 12 precisely so a `0` here
  // would prove the count was taken when it should not have been, instead of looking like a correct empty
  // department.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D35_EmployeeCount_is_null_for_a_caller_who_cannot_read_employees()
  {
    host.EmployeeReads.DepartmentMemberCount = 12;

    var response = await Send(HttpMethod.Get, $"{Route}/{DepartmentApiTestHost.DepartmentId}", ViewToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await DepartmentApiTestHost.BodyAsync(response));

    Assert.True(
      document.RootElement.TryGetProperty("employeeCount", out var count),
      "the field must be present for every caller, or the JSON shape varies per caller");

    Assert.Equal(JsonValueKind.Null, count.ValueKind);
  }

  // ---- THE COUNT IS TAKEN UNDER THE CALLER'S OWN EMPLOYEE SCOPE, NOT A WIDER ONE.
  //
  // The count cannot be issued without an `EmployeeReadScope` — the interface makes that a compile error —
  // so what is left to prove at this layer is that the scope handed to the counter is the CALLER'S: their
  // authorized companies and their authorized branches, neither widened. Whether the SQL then honours that
  // scope is proven against a real database in `Integration.Tests`, because a stub cannot filter rows.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D36_EmployeeCount_is_counted_under_the_callers_own_employee_scope()
  {
    host.EmployeeReads.DepartmentMemberCount = 3;

    var response = await Send(
      HttpMethod.Get,
      $"{Route}/{DepartmentApiTestHost.DepartmentId}",
      host.TokenWith(HrPermissionNames.ViewDepartments, HrPermissionNames.ViewEmployees));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var scope = host.EmployeeReads.LastScope;

    Assert.NotNull(scope);
    Assert.Equal(DepartmentApiTestHost.TenantId, scope.TenantId);
    Assert.Equal(
      [DepartmentApiTestHost.CompanyA, DepartmentApiTestHost.CompanyB],
      scope.Companies.CompanyIds.OrderBy(id => id).ToArray());
    Assert.Equal([DepartmentApiTestHost.BranchA], scope.Branches.BranchIds.ToArray());
  }

  // ---- AND IT IS ON THE WRITE-BACK REPRESENTATION TOO.
  //
  // Every write reads the department back through the scoped path and returns the same shape a GET does.
  // Composing the count in only the read route would give one contract two shapes, so this asserts the
  // field survives an update — the cheapest probe that the composer sits on the shared path.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task D37_A_write_back_carries_the_same_employeeCount_field()
  {
    host.EmployeeReads.DepartmentMemberCount = 7;

    var response = await Send(
      HttpMethod.Put,
      $"{Route}/{DepartmentApiTestHost.DepartmentId}",
      host.TokenWith(
        HrPermissionNames.UpdateDepartments,
        HrPermissionNames.ViewDepartments,
        HrPermissionNames.ViewEmployees),
      ValidUpdateBody);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = JsonDocument.Parse(await DepartmentApiTestHost.BodyAsync(response));

    Assert.Equal(7, document.RootElement.GetProperty("employeeCount").GetInt32());
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
