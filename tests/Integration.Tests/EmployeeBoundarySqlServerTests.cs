using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Application.Employees;
using SSAS.HR.Domain.ImportExport;
using SSAS.HR.Application.ImportExport;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Roles;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Branches;
using SSAS.Platform.Infrastructure.Companies;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;
using PlatformCompany = SSAS.Platform.Domain.Companies.Company;
using PlatformTenant = SSAS.Platform.Domain.Tenants.Tenant;

namespace SSAS.Integration.Tests;

// EMPLOYEE AGAINST REAL SQL SERVER — AND THE CLOSURE OF ADR-023 LOW-1 (FP-006C3).
//
// ================================================================================================
// THIS FILE IS WHY THE WHOLE PACKAGE WAS SEQUENCED THE WAY IT WAS.
// ================================================================================================
//
// Employee is the FIRST production IBranchOwnedEntity and the FIRST production ICompanyOwnedEntity. Until it
// existed, `TenantDbContext.ApplyBranchRulesAsync` had never executed against a real business entity: the
// authorizer was proven in isolation and the boundary was proven with test probes, but the CALL SITE BETWEEN
// THEM had never run for real. ADR-023 recorded that gap as LOW-1, and these tests close it.
//
// Every proof below runs through the REAL TenantDbContext, the REAL authorizers, the REAL repository and the
// REAL command handlers, against real SQL Server. Nothing here is mocked, because everything being tested is
// enforced by something only a real server and the real save pipeline have.
[Trait("Category", "SqlServer")]
public sealed class EmployeeBoundarySqlServerTests
{
  // ================================================================================================
  // B1 — THE ADR-023 LOW-1 PROOFS
  // ================================================================================================

  // ---- V. THE BRANCH WRITE AUTHORIZER IS GENUINELY REACHED, and the branch is stamped.
  //
  // THIS IS THE ONE THAT MATTERS MOST. Every branch test written before FP-006 passes whether or not the
  // authorizer's call site is reached, because no production entity implemented the interface. Asserting the
  // resulting BranchId would prove the value; only observing the INVOCATION proves the wiring.
  [Fact]
  public async Task V_Creating_a_real_employee_invokes_the_branch_write_authorizer_and_stamps_the_branch()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-V"));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    // The wiring: the boundary actually called the authorizer during this save.
    Assert.True(graph.BranchAuthorizerCalls > 0);

    Assert.Equal(fixture.BranchA, await fixture.EmployeeBranchAsync(created.Value));
  }

  // ---- W. A SPOOFED BranchId ON CREATE IS REFUSED, not silently rewritten.
  //
  // Quietly correcting it would hide the attempt, which is the whole reason a supplied value is CONFIRMED
  // rather than trusted.
  [Fact]
  public async Task W_A_spoofed_branch_on_employee_create_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);
    await using var context = await graph.ContextAsync();

    var employee = EmployeeFixture.NewAggregate("EMP-W");
    employee.BranchId = fixture.BranchB;
    employee.CompanyId = fixture.CompanyA;
    context.Set<Employee>().Add(employee);

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("Branch ownership", refusal.Message, StringComparison.Ordinal);

    Assert.Equal(0, await fixture.EmployeeCountAsync());
  }

  // ---- X. AN ORDINARY UPDATE CANNOT MUTATE BranchId.
  //
  // Proven at the BOUNDARY, independently of the update contract omitting the field: both defences exist and
  // this is the one that holds even if a future caller reaches the entity another way.
  [Fact]
  public async Task X_An_ordinary_update_cannot_change_an_employees_branch()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-X", fixture.BranchA);

    var graph = fixture.Graph(fixture.BranchA);
    await using var context = await graph.ContextAsync();
    var employee = await context.Set<Employee>().SingleAsync(candidate => candidate.Id == employeeId);

    employee.BranchId = fixture.BranchB;

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("cannot be changed after an entity is created", refusal.Message, StringComparison.Ordinal);

    Assert.Equal(fixture.BranchA, await fixture.EmployeeBranchAsync(employeeId));
  }

  // ---- Y. CROSS-BRANCH UPDATE AND DELETE ARE REFUSED.
  [Fact]
  public async Task Y_A_cross_branch_employee_update_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-Y1", fixture.BranchB);

    // Acting in branch A, reaching an employee owned by branch B.
    var graph = fixture.Graph(fixture.BranchA);
    await using var context = await graph.ContextAsync();
    var employee = await context.Set<Employee>().SingleAsync(candidate => candidate.Id == employeeId);

    employee.UpdateProfile(
      EmployeeFullName.Create("Edited Elsewhere").Value, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("must match the trusted branch context", refusal.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Y_A_cross_branch_employee_delete_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-Y2", fixture.BranchB);

    var graph = fixture.Graph(fixture.BranchA);
    await using var context = await graph.ContextAsync();
    var employee = await context.Set<Employee>().SingleAsync(candidate => candidate.Id == employeeId);

    context.Set<Employee>().Remove(employee);

    await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Equal(1, await fixture.EmployeeCountAsync());
  }

  // ---- AND SAME-BRANCH WORK STILL SUCCEEDS, so none of the above passes because the boundary refuses
  // everything.
  [Fact]
  public async Task A_same_branch_employee_update_still_succeeds()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-OK", fixture.BranchA);

    var graph = fixture.Graph(fixture.BranchA);
    var updated = await graph.Update().HandleAsync(new UpdateEmployeeProfileCommand(
      employeeId, "Renamed Person", null, await fixture.RowVersionAsync(employeeId)));

    Assert.True(updated.IsSuccess, updated.IsFailure ? updated.Error.Code : null);
  }

  // ================================================================================================
  // COMPANY BOUNDARY, ON THE FIRST REAL ICompanyOwnedEntity
  // ================================================================================================

  // ---- C-A. THE COMPANY WRITE AUTHORIZER IS GENUINELY REACHED on a real Employee save.
  [Fact]
  public async Task CA_Creating_a_real_employee_invokes_the_company_write_authorizer()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-CA"));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
    Assert.True(graph.CompanyAuthorizerCalls > 0);
    Assert.Equal(fixture.CompanyA, await fixture.EmployeeCompanyAsync(created.Value));
  }

  // ---- C-B. A SPOOFED CompanyId ON CREATE IS REFUSED.
  [Fact]
  public async Task CB_A_spoofed_company_on_employee_create_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);
    await using var context = await graph.ContextAsync();

    var employee = EmployeeFixture.NewAggregate("EMP-CB");
    employee.CompanyId = fixture.CompanyB;
    context.Set<Employee>().Add(employee);

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("Company ownership", refusal.Message, StringComparison.Ordinal);
    Assert.Equal(0, await fixture.EmployeeCountAsync());
  }

  // ---- C-C. AN ORDINARY UPDATE CANNOT MUTATE CompanyId. Unlike branch there is no sanctioned transfer: an
  // employee does not move between legal entities.
  [Fact]
  public async Task CC_An_ordinary_update_cannot_change_an_employees_company()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-CC", fixture.BranchA);

    var graph = fixture.Graph(fixture.BranchA);
    await using var context = await graph.ContextAsync();
    var employee = await context.Set<Employee>().SingleAsync(candidate => candidate.Id == employeeId);

    employee.CompanyId = fixture.CompanyB;

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("Company ownership cannot be changed", refusal.Message, StringComparison.Ordinal);
    Assert.Equal(fixture.CompanyA, await fixture.EmployeeCompanyAsync(employeeId));
  }

  // ---- C-D. CROSS-COMPANY UPDATE AND DELETE ARE REFUSED.
  [Fact]
  public async Task CD_A_cross_company_employee_update_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-CD", fixture.BranchA);

    // Acting within company B, reaching a row owned by company A.
    var graph = fixture.Graph(fixture.BranchA, company: fixture.CompanyB);
    await using var context = await graph.ContextAsync();
    var employee = await context.Set<Employee>().SingleAsync(candidate => candidate.Id == employeeId);

    employee.UpdateProfile(
      EmployeeFullName.Create("Edited Cross Company").Value, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("must match the trusted company context", refusal.Message, StringComparison.Ordinal);
  }

  // ================================================================================================
  // REVOCATION — EVERY INPUT IS RE-ASKED, NEVER CACHED FROM REQUEST START
  // ================================================================================================

  [Fact]
  public async Task Revoking_branch_access_mid_session_refuses_the_next_employee_write()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R1"))).IsSuccess);

    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchA);

    var after = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R2"));

    Assert.True(after.IsFailure);
    Assert.Equal(1, await fixture.EmployeeCountAsync());
  }

  [Fact]
  public async Task Revoking_administrator_authority_mid_session_removes_implicit_branch_scope()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    // The administrator holds NO branch assignment rows: their scope is derived from the permission alone.
    Assert.Equal(0, await fixture.BranchAccessRowCountAsync(fixture.AdministratorUserId));

    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.AdministratorUserId);
    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R3"))).IsSuccess);

    await fixture.RevokeAdministratorAuthorityAsync();

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R4"))).IsFailure);
    Assert.Equal(1, await fixture.EmployeeCountAsync());
  }

  [Fact]
  public async Task Revoking_company_access_mid_session_refuses_the_next_employee_write()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R5"))).IsSuccess);

    await fixture.RevokeCompanyAssignmentAsync(fixture.NormalUserId, fixture.CompanyA);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R6"))).IsFailure);
    Assert.Equal(1, await fixture.EmployeeCountAsync());
  }

  // The administrator's COMPANY scope is derived from the same permission, so revoking it removes that too.
  [Fact]
  public async Task Revoking_administrator_authority_mid_session_removes_implicit_company_scope()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    Assert.Equal(0, await fixture.CompanyAccessRowCountAsync(fixture.AdministratorUserId));

    var resolver = fixture.CompanyResolver();
    Assert.True((await resolver.AuthorizeCompanyAsync(
      fixture.Tenant, fixture.AdministratorUserId, fixture.CompanyA)).IsSuccess);

    await fixture.RevokeAdministratorAuthorityAsync();

    Assert.True((await resolver.AuthorizeCompanyAsync(
      fixture.Tenant, fixture.AdministratorUserId, fixture.CompanyA)).IsFailure);
  }

  [Fact]
  public async Task Deactivating_the_company_mid_session_refuses_the_next_employee_write()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R7"))).IsSuccess);

    await fixture.DeactivateCompanyAsync(fixture.CompanyA);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R8"))).IsFailure);
    Assert.Equal(1, await fixture.EmployeeCountAsync());
  }

  [Fact]
  public async Task Revoking_the_session_refuses_the_next_employee_write()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R9"))).IsSuccess);

    await fixture.RevokeSessionAsync(graph.SessionId);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-R10"))).IsFailure);
    Assert.Equal(1, await fixture.EmployeeCountAsync());
  }

  // ================================================================================================
  // UNIQUENESS — COMPANY-WIDE, AND DELIBERATELY NOT BRANCH-WIDE
  // ================================================================================================

  [Fact]
  public async Task An_employee_number_is_unique_within_a_company()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-100"))).IsSuccess);

    var duplicate = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-100"));

    Assert.True(duplicate.IsFailure);
    Assert.Equal(EmployeeErrors.NumberConflict.Code, duplicate.Error.Code);
  }

  // ---- THE ONE THAT MAKES BR-HR-0001 CONCRETE: uniqueness spans branches of the same company, because
  // BranchId deliberately does not participate in the index.
  [Fact]
  public async Task An_employee_number_is_unique_across_branches_of_the_same_company()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    Assert.True((await fixture.Graph(fixture.BranchA).Create().HandleAsync(fixture.NewEmployee("EMP-200"))).IsSuccess);

    var otherBranch = await fixture.Graph(fixture.BranchB).Create().HandleAsync(fixture.NewEmployee("EMP-200"));

    Assert.True(otherBranch.IsFailure);
    Assert.Equal(EmployeeErrors.NumberConflict.Code, otherBranch.Error.Code);
  }

  [Fact]
  public async Task The_same_employee_number_is_free_in_a_different_company()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    Assert.True((await fixture.Graph(fixture.BranchA).Create()
      .HandleAsync(fixture.NewEmployee("EMP-300"))).IsSuccess);

    // The DEPARTMENT follows the company, because a department belongs to exactly one. Naming CompanyA's
    // here would fail as not-found and the test would look like a uniqueness failure it is not.
    //
    // THE POSITION FOLLOWS IT FOR THE IDENTICAL REASON (FP-008 Phase 3). `NewEmployee` defaults to
    // CompanyA's position, so this call must name CompanyB's — and when Phase 3 first made the column
    // required, this test failed with `PositionNotFound` and looked exactly like the uniqueness failure the
    // comment above already warned about. The warning was right and applied to one field too few.
    var otherCompany = await fixture.Graph(fixture.BranchA, company: fixture.CompanyB).Create()
      .HandleAsync(fixture.NewEmployee(
        "EMP-300", department: fixture.DepartmentB, position: fixture.PositionB));

    Assert.True(otherCompany.IsSuccess, otherCompany.IsFailure ? otherCompany.Error.Code : null);
  }

  // Two spellings that normalize alike are the same number, and the binary-collated index is what refuses
  // the second under concurrency.
  [Fact]
  // ⚠ CITED BY ITEM 218, body-confirmed -- AND THE OBVIOUS CANDIDATE WAS THE WRONG ONE.
  // `AC-EMP-0006` is UNIQUENESS: *"two employee numbers whose `Trim().ToUpperInvariant()` values are
  // equal CANNOT BOTH BE CREATED"*. `EmployeeDomainTests.Employee_numbers_that_normalize_alike_are_equal`
  // matches the words and asserts VALUE EQUALITY, which is a different claim. This one creates
  // " emp-400 " and then "EMP-400" and asserts the second fails with `NumberConflict`.
  [Trait("Criterion", "AC-EMP-0006")]
  public async Task Employee_numbers_that_normalize_alike_collide()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee(" emp-400 "))).IsSuccess);

    var equivalent = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-400"));

    Assert.True(equivalent.IsFailure);
    Assert.Equal(EmployeeErrors.NumberConflict.Code, equivalent.Error.Code);
  }

  // ---- NATIONAL ID: unique where present, and many absent values remain possible.
  [Fact]
  public async Task A_national_id_is_unique_within_a_company_but_may_be_absent_many_times()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-500", nationalId: "NID-1"))).IsSuccess);

    var duplicate = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-501", nationalId: "nid-1"));
    Assert.True(duplicate.IsFailure);
    Assert.Equal(EmployeeErrors.NationalIdConflict.Code, duplicate.Error.Code);

    // Two employees with no national identifier at all are fine: the unique index is filtered.
    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-502"))).IsSuccess);
    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-503"))).IsSuccess);
  }

  // ================================================================================================
  // INITIAL ASSIGNMENT, APPEND-ONLY HISTORY AND PHYSICAL DELETE
  // ================================================================================================

  [Fact]
  public async Task Creating_an_employee_writes_its_initial_assignment_atomically()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var created = await fixture.Graph(fixture.BranchA).Create().HandleAsync(fixture.NewEmployee("EMP-600"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var history = await fixture.HistoryAsync(created.Value);
    var initial = Assert.Single(history);

    Assert.Null(initial.SourceBranchId);
    Assert.Equal(fixture.BranchA, initial.DestinationBranchId);
    Assert.Equal(nameof(EmployeeBranchTransferReason.InitialAssignment), initial.ReasonCode);
  }

  // If the employee cannot be written, neither is its history: they are one transaction.
  [Fact]
  public async Task A_refused_employee_create_writes_no_history_row()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-700"))).IsSuccess);
    Assert.True((await graph.Create().HandleAsync(fixture.NewEmployee("EMP-700"))).IsFailure);

    Assert.Equal(1, await fixture.EmployeeCountAsync());
    Assert.Equal(1, await fixture.HistoryRowCountAsync());
  }

  [Fact]
  public async Task A_history_row_cannot_be_updated_or_deleted()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var created = await fixture.Graph(fixture.BranchA).Create().HandleAsync(fixture.NewEmployee("EMP-800"));
    Assert.True(created.IsSuccess);

    // ONE context for both attempts, and deliberately not disposed between them: the provider is scoped, so
    // disposing it would end the very scope the second attempt needs.
    var graph = fixture.Graph(fixture.BranchA);
    var context = await graph.ContextAsync();

    var assignment = await context.Set<EmployeeBranchAssignment>()
      .SingleAsync(record => record.EmployeeId == created.Value);

    // UPDATE is refused, even though the property is mapped and the value is otherwise writable.
    context.Entry(assignment).Property(nameof(EmployeeBranchAssignment.ReasonText)).CurrentValue = "rewritten";

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("Append-only", refusal.Message, StringComparison.Ordinal);

    // Put the entry back so the delete attempt is judged on its own terms rather than trailing the update.
    context.Entry(assignment).State = EntityState.Unchanged;

    context.Set<EmployeeBranchAssignment>().Remove(assignment);

    var deleteRefusal = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    Assert.Contains("Append-only", deleteRefusal.Message, StringComparison.Ordinal);

    Assert.Equal(1, await fixture.HistoryRowCountAsync());
  }

  // ---- PHYSICAL DELETE IS PROHIBITED. Termination is retention, not removal.
  [Fact]
  public async Task An_employee_cannot_be_physically_deleted()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-900", fixture.BranchA);

    var graph = fixture.Graph(fixture.BranchA);
    await using var context = await graph.ContextAsync();
    var employee = await context.Set<Employee>().SingleAsync(candidate => candidate.Id == employeeId);

    context.Set<Employee>().Remove(employee);

    // Refused by the restricted foreign key from its own history, which is itself append-only: an employee
    // cannot be erased without erasing the record of where they worked, and that is not permitted either.
    await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
    Assert.Equal(1, await fixture.EmployeeCountAsync());

    // And no delete operation is exposed anywhere in the repository contract.
    Assert.DoesNotContain(
      typeof(IEmployeeRepository).GetMethods().Select(method => method.Name),
      name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Remove", StringComparison.OrdinalIgnoreCase));
  }

  // Termination retains the record, its identifiers and its history.
  [Fact]
  public async Task Termination_retains_the_employee_and_its_history()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);
    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-1000"));
    Assert.True(created.IsSuccess);

    var terminated = await graph.Terminate().HandleAsync(new TerminateEmployeeCommand(
      created.Value,
      DateTimeOffset.UtcNow,
      EmployeeStatusChangeReason.Resignation,
      await fixture.RowVersionAsync(created.Value)));

    Assert.True(terminated.IsSuccess, terminated.IsFailure ? terminated.Error.Code : null);
    Assert.Equal(1, await fixture.EmployeeCountAsync());
    Assert.Equal(1, await fixture.HistoryRowCountAsync());

    // The number stays reserved: a terminated employee still occupies it.
    var reuse = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-1000"));
    Assert.True(reuse.IsFailure);
  }

  // ================================================================================================
  // TRANSFER — THE SANCTIONED CHANNEL, ON A REAL EMPLOYEE
  // ================================================================================================

  [Fact]
  public async Task A_transfer_moves_the_employee_and_appends_exactly_one_record()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);
    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-T1"));
    Assert.True(created.IsSuccess);

    var before = await fixture.RowVersionAsync(created.Value);

    var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchB, EmployeeBranchTransferReason.Reorganisation, "consolidating", before));

    Assert.True(moved.IsSuccess, moved.IsFailure ? moved.Error.Code : null);
    Assert.Equal(fixture.BranchB, await fixture.EmployeeBranchAsync(created.Value));

    var history = (await fixture.HistoryAsync(created.Value)).OrderBy(record => record.EffectiveFromUtc).ToArray();
    Assert.Equal(2, history.Length);

    // THE INITIAL RECORD IS UNTOUCHED. History is appended, never rewritten.
    Assert.Null(history[0].SourceBranchId);
    Assert.Equal(fixture.BranchA, history[0].DestinationBranchId);

    Assert.Equal(fixture.BranchA, history[1].SourceBranchId);
    Assert.Equal(fixture.BranchB, history[1].DestinationBranchId);
    Assert.Equal(nameof(EmployeeBranchTransferReason.Reorganisation), history[1].ReasonCode);

    // The rowversion moved, so a caller holding the old one cannot act on stale state.
    Assert.NotEqual(before, await fixture.RowVersionAsync(created.Value));
  }

  [Fact]
  public async Task A_transfer_with_a_stale_rowversion_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);
    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-T2"));
    Assert.True(created.IsSuccess);

    var stale = await fixture.RowVersionAsync(created.Value);

    Assert.True((await graph.Update().HandleAsync(new UpdateEmployeeProfileCommand(
      created.Value, "Renamed", null, stale))).IsSuccess);

    var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchB, EmployeeBranchTransferReason.Reorganisation, null, stale));

    Assert.True(moved.IsFailure);
    Assert.Equal(EmployeeErrors.ConcurrencyConflict.Code, moved.Error.Code);
    Assert.Equal(fixture.BranchA, await fixture.EmployeeBranchAsync(created.Value));
  }

  [Fact]
  public async Task A_transfer_to_an_unreachable_destination_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    // The normal user reaches A and B, never C.
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);
    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-T3"));
    Assert.True(created.IsSuccess);

    var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchC, EmployeeBranchTransferReason.Reorganisation, null,
      await fixture.RowVersionAsync(created.Value)));

    Assert.True(moved.IsFailure);
    Assert.Equal(BranchErrors.InvalidSelection.Code, moved.Error.Code);
    Assert.Equal(fixture.BranchA, await fixture.EmployeeBranchAsync(created.Value));
  }

  [Fact]
  public async Task A_transfer_into_an_inactive_destination_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);
    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-T4"));
    Assert.True(created.IsSuccess);

    await fixture.DeactivateBranchAsync(fixture.BranchB);

    var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchB, EmployeeBranchTransferReason.Reorganisation, null,
      await fixture.RowVersionAsync(created.Value)));

    Assert.True(moved.IsFailure);
    Assert.Equal(fixture.BranchA, await fixture.EmployeeBranchAsync(created.Value));
  }

  [Fact]
  public async Task A_terminated_employee_cannot_be_transferred()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);
    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-T5"));
    Assert.True(created.IsSuccess);

    Assert.True((await graph.Terminate().HandleAsync(new TerminateEmployeeCommand(
      created.Value, DateTimeOffset.UtcNow, EmployeeStatusChangeReason.Resignation,
      await fixture.RowVersionAsync(created.Value)))).IsSuccess);

    var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchB, EmployeeBranchTransferReason.Reorganisation, null,
      await fixture.RowVersionAsync(created.Value)));

    Assert.True(moved.IsFailure);
    Assert.Equal(EmployeeErrors.TransferAfterTermination.Code, moved.Error.Code);
  }

  // ---- REVOKING SOURCE ACCESS BEFORE THE SAVE REFUSES THE TRANSFER: the boundary re-asks even though the
  // handler authorized moments earlier.
  [Fact]
  public async Task Revoking_source_branch_access_before_the_transfer_save_refuses_it()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);
    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-T6"));
    Assert.True(created.IsSuccess);

    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchA);

    var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchB, EmployeeBranchTransferReason.Reorganisation, null,
      await fixture.RowVersionAsync(created.Value)));

    Assert.True(moved.IsFailure);
    Assert.Equal(fixture.BranchA, await fixture.EmployeeBranchAsync(created.Value));
  }

  [Fact]
  public async Task Revoking_destination_branch_access_before_the_transfer_refuses_it()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);
    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-T7"));
    Assert.True(created.IsSuccess);

    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchB);

    var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchB, EmployeeBranchTransferReason.Reorganisation, null,
      await fixture.RowVersionAsync(created.Value)));

    Assert.True(moved.IsFailure);
    Assert.Equal(fixture.BranchA, await fixture.EmployeeBranchAsync(created.Value));
  }

  // ---- ADR-024 DECISION 12: the narrow recovery out of a deactivated source.
  [Fact]
  public async Task A_tenant_administrator_can_recover_an_employee_from_an_inactive_branch()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var seeded = await fixture.Graph(fixture.BranchC, asUserId: fixture.AdministratorUserId)
      .Create().HandleAsync(fixture.NewEmployee("EMP-T8"));
    Assert.True(seeded.IsSuccess, seeded.IsFailure ? seeded.Error.Code : null);

    await fixture.DeactivateBranchAsync(fixture.BranchC);

    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.AdministratorUserId);
    var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      seeded.Value, fixture.BranchB, EmployeeBranchTransferReason.BranchClosure, null,
      await fixture.RowVersionAsync(seeded.Value), InactiveSourceRecovery: true));

    Assert.True(moved.IsSuccess, moved.IsFailure ? moved.Error.Code : null);
    Assert.Equal(fixture.BranchB, await fixture.EmployeeBranchAsync(seeded.Value));
    Assert.Equal(2, (await fixture.HistoryAsync(seeded.Value)).Count);
  }

  [Fact]
  public async Task The_same_recovery_without_administrator_authority_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var seeded = await fixture.Graph(fixture.BranchC, asUserId: fixture.AdministratorUserId)
      .Create().HandleAsync(fixture.NewEmployee("EMP-T9"));
    Assert.True(seeded.IsSuccess);

    await fixture.DeactivateBranchAsync(fixture.BranchC);

    // The normal user already reaches the DESTINATION, so the only thing they lack is the administration
    // authority the recovery requires. That is what makes this a clean negative control.
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);
    var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      seeded.Value, fixture.BranchB, EmployeeBranchTransferReason.BranchClosure, null,
      await fixture.RowVersionAsync(seeded.Value), InactiveSourceRecovery: true));

    Assert.True(moved.IsFailure);
    Assert.Equal(fixture.BranchC, await fixture.EmployeeBranchAsync(seeded.Value));
  }

  // ---- TWO SIMULTANEOUS TRANSFERS: exactly one wins, and the history cannot fork.
  [Fact]
  public async Task Two_transfers_from_the_same_rowversion_produce_one_winner()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var created = await fixture.Graph(fixture.BranchA).Create().HandleAsync(fixture.NewEmployee("EMP-T10"));
    Assert.True(created.IsSuccess);

    var shared = await fixture.RowVersionAsync(created.Value);

    var first = await fixture.Graph(fixture.BranchA).Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchB, EmployeeBranchTransferReason.Reorganisation, null, shared));

    // The second holds the SAME rowversion the first started from.
    var second = await fixture.Graph(fixture.BranchA).Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchC, EmployeeBranchTransferReason.Reorganisation, null, shared));

    Assert.True(first.IsSuccess, first.IsFailure ? first.Error.Code : null);
    Assert.True(second.IsFailure);

    // One move, one appended record — the log did not fork.
    Assert.Equal(fixture.BranchB, await fixture.EmployeeBranchAsync(created.Value));
    Assert.Equal(2, (await fixture.HistoryAsync(created.Value)).Count);
  }

  // ---- POINT-IN-TIME ATTRIBUTION uses the history, and gives a different answer from the current branch.
  [Fact]
  public async Task Point_in_time_attribution_differs_from_the_current_branch()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);
    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-T11"));
    Assert.True(created.IsSuccess);

    var beforeTransfer = DateTimeOffset.UtcNow;
    await Task.Delay(50);

    Assert.True((await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchB, EmployeeBranchTransferReason.Reorganisation, null,
      await fixture.RowVersionAsync(created.Value)))).IsSuccess);

    // Current state.
    Assert.Equal(fixture.BranchB, await fixture.EmployeeBranchAsync(created.Value));

    // The branch effective before the transfer: the record with the greatest EffectiveFromUtc <= T.
    var history = await fixture.HistoryAsync(created.Value);
    var effective = history
      .Where(record => record.EffectiveFromUtc <= beforeTransfer)
      .OrderByDescending(record => record.EffectiveFromUtc)
      .First();

    Assert.Equal(fixture.BranchA, effective.DestinationBranchId);
  }

  // ================================================================================================
  // CLASSIFICATION — TS-EMP-0113, AT THE MODEL LEVEL
  // ================================================================================================

  // The architecture test asserts the interface set; this asserts what EF actually MAPPED, which is where a
  // shadow property would appear.
  [Fact]
  public async Task The_branch_assignment_is_not_branch_owned_and_has_no_branch_id_property()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    await using var context = await fixture.Graph(fixture.BranchA).ContextAsync();

    var assignment = context.Model.FindEntityType(typeof(EmployeeBranchAssignment));
    Assert.NotNull(assignment);

    // NOT branch-owned.
    Assert.DoesNotContain(
      typeof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity),
      typeof(EmployeeBranchAssignment).GetInterfaces());

    // AND NO SHADOW `BranchId` — the failure mode a convention or a stray relationship would introduce.
    Assert.DoesNotContain(
      assignment!.GetProperties(),
      property => string.Equals(property.Name, "BranchId", StringComparison.Ordinal));

    Assert.Contains(assignment.GetProperties(),
      property => property.Name == nameof(EmployeeBranchAssignment.SourceBranchId));
    Assert.Contains(assignment.GetProperties(),
      property => property.Name == nameof(EmployeeBranchAssignment.DestinationBranchId));

    // Employee, by contrast, IS branch-owned and does map BranchId.
    var employee = context.Model.FindEntityType(typeof(Employee));
    Assert.NotNull(employee);
    Assert.Contains(employee!.GetProperties(), property => property.Name == nameof(Employee.BranchId));
  }

  // ================================================================================================
  // R — THE READ SCOPE AGAINST REAL SQL (FP-006C4, ADR-023 decision 22, ADR-025 decision 10)
  // ================================================================================================
  //
  // The architecture guards prove the read surface CANNOT be written unscoped. These prove the scope it
  // requires actually restricts what SQL Server returns.
  //
  // ---- EVERY PROOF CARRIES A NEGATIVE CONTROL.
  //
  // The data is seeded across TWO companies and THREE branches, and the rows that must not come back are
  // seeded FIRST. A scoping test on single-branch, single-company data passes whether or not any predicate
  // exists — it proves only that the row you inserted is the row you got. Where it matters, the proof also
  // counts the raw table, so "the predicate excluded it" is distinguishable from "it was never there".

  // ---- R1/R2/R3. ALL THREE COLUMNS APPEAR IN THE GENERATED SQL.
  //
  // Not "a filter was configured" and not "the right rows came back" — the actual command text that reached
  // the server, produced by the real read service. This is what ADR-025 decision 10 means by an EXPLICIT
  // predicate.
  [Fact]
  public async Task R1_R3_Every_employee_read_sends_tenant_company_and_branch_predicates_to_the_server()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    await fixture.SeedEmployeeAsync("EMP-R1", fixture.BranchA);

    var (context, sql) = fixture.LoggingContext();
    await using (context)
    {
      var reads = new EmployeeReadService(new StaticAccessor(context));
      using var graph = fixture.Graph(fixture.BranchA, fixture.NormalUserId);
      var scope = await graph.Scope().ResolveAsync(new EmployeeScopeRequest(
        CompanyScope: EmployeeCompanyScopeMode.AllAuthorizedCompanies,
        BranchScope: EmployeeBranchScopeMode.AllAuthorizedBranches));

      Assert.True(scope.IsSuccess, scope.IsFailure ? scope.Error.Code : null);

      await reads.SearchEmployeesAsync(scope.Value, new EmployeeSearchCriteria());
    }

    var commands = string.Join(Environment.NewLine, sql);
    var where = commands.IndexOf("WHERE", StringComparison.Ordinal);

    Assert.True(where >= 0, commands);
    Assert.Contains("[TenantId]", commands[where..], StringComparison.Ordinal);
    Assert.Contains("[CompanyId]", commands[where..], StringComparison.Ordinal);
    Assert.Contains("[BranchId]", commands[where..], StringComparison.Ordinal);
  }

  // ---- R4. THE DEFAULT SCOPE IS ONE BRANCH, and the other branches' employees are genuinely excluded.
  [Fact]
  public async Task R4_The_current_branch_scope_excludes_employees_of_other_branches()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    // NEGATIVE CONTROLS FIRST.
    await fixture.SeedEmployeeAsync("EMP-R4B", fixture.BranchB);
    await fixture.SeedEmployeeAsync("EMP-R4C", fixture.BranchC);
    var expected = await fixture.SeedEmployeeAsync("EMP-R4A", fixture.BranchA);

    var page = await fixture.SearchAsync(fixture.BranchA, new EmployeeScopeRequest());

    Assert.Equal([expected], page.Items.Select(item => item.EmployeeId));

    // AND THE EXCLUDED ROWS EXIST. Without this the assertion above would also pass on an empty table.
    Assert.Equal(3, await fixture.EmployeeCountAsync());
  }

  // ---- R5. A SUBSET OF THE AUTHORIZED BRANCHES IS HONOURED EXACTLY.
  [Fact]
  public async Task R5_A_selected_subset_returns_exactly_those_branches()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var inA = await fixture.SeedEmployeeAsync("EMP-R5A", fixture.BranchA);
    var inB = await fixture.SeedEmployeeAsync("EMP-R5B", fixture.BranchB);
    await fixture.SeedEmployeeAsync("EMP-R5C", fixture.BranchC);

    var page = await fixture.SearchAsync(fixture.BranchA, new EmployeeScopeRequest(
      BranchScope: EmployeeBranchScopeMode.SelectedAuthorizedBranches,
      SelectedBranchIds: [fixture.BranchA, fixture.BranchB]));

    // Compared as a SET: the server orders identifiers by its own uniqueidentifier collation, so asserting a
    // .NET-sorted sequence here would be a fact about the client that happens to hold for some Guids.
    Assert.Equal([inA, inB], page.Items.Select(item => item.EmployeeId).ToHashSet());
    Assert.Equal(3, await fixture.EmployeeCountAsync());
  }

  // ---- R6. A NON-SUBSET SELECTION IS REFUSED, and no query is issued at all.
  //
  // Refused rather than intersected down to BranchA: a caller must never be told they saw every branch they
  // named when one of them was silently dropped.
  [Fact]
  public async Task R6_A_selection_outside_the_authorized_set_is_refused_before_any_query_runs()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    await fixture.SeedEmployeeAsync("EMP-R6", fixture.BranchA);

    using var graph = fixture.Graph(fixture.BranchA, fixture.NormalUserId);

    // The normal user reaches A and B, never C.
    var scope = await graph.Scope().ResolveAsync(new EmployeeScopeRequest(
      BranchScope: EmployeeBranchScopeMode.SelectedAuthorizedBranches,
      SelectedBranchIds: [fixture.BranchA, fixture.BranchC]));

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.BranchScopeDenied, scope.Error);
  }

  // ================================================================================================
  // R7. THE PROOF THAT "ALL" IS MATERIALIZED RATHER THAN OMITTED.
  // ================================================================================================
  //
  // This is the single most important read proof in the slice. An implementation that dropped the branch
  // predicate when the caller asks for "all authorized branches" would pass every other test here — it would
  // return MORE rows, and every other test asserts on rows it expects to see.
  //
  // So the fixture seeds an employee in BranchC, which the normal user is NOT authorized for, and asserts it
  // does not come back. A predicate-omission implementation returns it. There is no other way to tell.
  [Fact]
  public async Task R7_All_authorized_branches_is_a_materialized_list_not_a_missing_predicate()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var inA = await fixture.SeedEmployeeAsync("EMP-R7A", fixture.BranchA);
    var inB = await fixture.SeedEmployeeAsync("EMP-R7B", fixture.BranchB);
    var unauthorized = await fixture.SeedEmployeeAsync("EMP-R7C", fixture.BranchC);

    var page = await fixture.SearchAsync(
      fixture.BranchA,
      new EmployeeScopeRequest(BranchScope: EmployeeBranchScopeMode.AllAuthorizedBranches),
      asUserId: fixture.NormalUserId);

    var returned = page.Items.Select(item => item.EmployeeId).ToHashSet();

    Assert.Equal([inA, inB], returned);
    Assert.DoesNotContain(unauthorized, returned);
    Assert.Equal(3, await fixture.EmployeeCountAsync());
  }

  // ---- R8. AN EMPTY AUTHORIZED SET REFUSES; it never degrades to unfiltered.
  //
  // The dangerous version of this bug returns EVERY employee in the tenant, because an empty set became an
  // empty condition. Here the user's last branch grant is removed and the read is asked for again.
  [Fact]
  public async Task R8_An_empty_authorized_branch_set_refuses_rather_than_returning_everything()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    await fixture.SeedEmployeeAsync("EMP-R8A", fixture.BranchA);
    await fixture.SeedEmployeeAsync("EMP-R8B", fixture.BranchB);

    using var graph = fixture.Graph(fixture.BranchA, fixture.NormalUserId);

    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchA);
    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchB);

    var scope = await graph.Scope().ResolveAsync(
      new EmployeeScopeRequest(BranchScope: EmployeeBranchScopeMode.AllAuthorizedBranches));

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.BranchScopeDenied, scope.Error);
    Assert.Equal(2, await fixture.EmployeeCountAsync());
  }

  // ---- R9. COMPANY AND BRANCH ARE INDEPENDENT DIMENSIONS.
  //
  // Two employees in the SAME branch, in DIFFERENT companies. Branch scope alone would return both. This is
  // what makes the company predicate load-bearing rather than incidental.
  [Fact]
  public async Task R9_The_company_predicate_excludes_a_sibling_company_in_the_same_branch()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var inA = await fixture.SeedEmployeeAsync("EMP-R9A", fixture.BranchA, fixture.CompanyA);
    var inB = await fixture.SeedEmployeeAsync("EMP-R9B", fixture.BranchA, fixture.CompanyB);

    var page = await fixture.SearchAsync(
      fixture.BranchA, new EmployeeScopeRequest(), company: fixture.CompanyA);

    Assert.Equal([inA], page.Items.Select(item => item.EmployeeId));
    Assert.DoesNotContain(inB, page.Items.Select(item => item.EmployeeId));
    Assert.Equal(2, await fixture.EmployeeCountAsync());
  }

  // ---- R10. SEARCH MAY SPAN THE CALLER'S OWN AUTHORIZED COMPANIES, and only those.
  [Fact]
  public async Task R10_All_authorized_companies_spans_companies_and_stays_inside_the_authorized_set()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var inA = await fixture.SeedEmployeeAsync("EMP-R10A", fixture.BranchA, fixture.CompanyA);
    var inB = await fixture.SeedEmployeeAsync("EMP-R10B", fixture.BranchA, fixture.CompanyB);

    // The user keeps CompanyA only. CompanyB's employee must disappear from the "all companies" read, which
    // is what makes "all" mean "all AUTHORIZED".
    await fixture.RevokeCompanyAssignmentAsync(fixture.NormalUserId, fixture.CompanyB);

    var page = await fixture.SearchAsync(
      fixture.BranchA,
      new EmployeeScopeRequest(CompanyScope: EmployeeCompanyScopeMode.AllAuthorizedCompanies),
      asUserId: fixture.NormalUserId);

    Assert.Equal([inA], page.Items.Select(item => item.EmployeeId));
    Assert.DoesNotContain(inB, page.Items.Select(item => item.EmployeeId));
  }

  // ---- R11. AN EMPTY AUTHORIZED COMPANY SET REFUSES, for the same reason R8 does.
  [Fact]
  public async Task R11_An_empty_authorized_company_set_refuses_rather_than_returning_everything()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    await fixture.SeedEmployeeAsync("EMP-R11", fixture.BranchA);

    using var graph = fixture.Graph(fixture.BranchA, fixture.NormalUserId);

    await fixture.RevokeCompanyAssignmentAsync(fixture.NormalUserId, fixture.CompanyA);
    await fixture.RevokeCompanyAssignmentAsync(fixture.NormalUserId, fixture.CompanyB);

    var scope = await graph.Scope().ResolveAsync(
      new EmployeeScopeRequest(CompanyScope: EmployeeCompanyScopeMode.AllAuthorizedCompanies));

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.CompanyScopeDenied, scope.Error);
  }

  // ---- R12. AN EMPLOYEE OUTSIDE THE SCOPE IS NOT FOUND, not forbidden.
  //
  // The caller names a real identifier and is told nothing about it — the same answer they get for an
  // identifier that never existed, so the read cannot be used to probe.
  [Fact]
  public async Task R12_An_employee_outside_the_scope_is_indistinguishable_from_one_that_does_not_exist()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var elsewhere = await fixture.SeedEmployeeAsync("EMP-R12", fixture.BranchB);

    var outOfScope = await fixture.GetAsync(fixture.BranchA, elsewhere);
    var nonexistent = await fixture.GetAsync(fixture.BranchA, Guid.NewGuid());

    Assert.True(outOfScope.IsFailure);
    Assert.Equal(EmployeeErrors.NotFound, outOfScope.Error);
    Assert.Equal(outOfScope.Error, nonexistent.Error);
  }

  // ---- R13. AND AN EMPLOYEE INSIDE THE SCOPE IS RETURNED, with its scope columns on the row.
  [Fact]
  public async Task R13_An_employee_inside_the_scope_is_returned_with_its_company_and_branch()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var employeeId = await fixture.SeedEmployeeAsync("EMP-R13", fixture.BranchA);

    var result = await fixture.GetAsync(fixture.BranchA, employeeId);

    Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
    Assert.Equal(employeeId, result.Value.EmployeeId);
    Assert.Equal(fixture.CompanyA, result.Value.CompanyId);
    Assert.Equal(fixture.BranchA, result.Value.BranchId);
    Assert.Equal("EMP-R13", result.Value.EmployeeNumber);
    Assert.Equal(EmployeeStatus.Active, result.Value.Status);

    // ---- AND A TERMINATED EMPLOYEE IS STILL RETRIEVABLE BY IDENTIFIER.
    //
    // The Terminated default belongs to SEARCH, where it keeps routine lists from quietly including people
    // who have left. A direct lookup carries no such default: the record is retained for history and
    // reporting (`BRULE-EMP-0021`), and hiding it here would make a retained record unreachable — which is
    // indistinguishable, to a caller, from having deleted it.
    var terminated = await fixture.SeedEmployeeAsync("EMP-R13T", fixture.BranchA);
    await fixture.TerminateAsync(terminated, fixture.BranchA);

    var afterTermination = await fixture.GetAsync(fixture.BranchA, terminated);

    Assert.True(afterTermination.IsSuccess, afterTermination.IsFailure ? afterTermination.Error.Code : null);
    Assert.Equal(EmployeeStatus.Terminated, afterTermination.Value.Status);
    Assert.NotNull(afterTermination.Value.TerminationDate);
  }

  // ================================================================================================
  // R14. THE HISTORY LEAK THAT THE EMPLOYEE-FIRST ORDERING PREVENTS.
  // ================================================================================================
  //
  // EmployeeBranchAssignment is NOT branch-owned, so no branch predicate can be written over it. If the
  // history read did not prove the employee was in scope first, a caller confined to BranchA could name an
  // employee in BranchB and receive every branch that employee has ever worked in — a list of branch
  // identifiers they are not authorized to see, from a table that cannot filter them out.
  [Fact]
  public async Task R14_Branch_history_of_an_out_of_scope_employee_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var elsewhere = await fixture.SeedEmployeeAsync("EMP-R14", fixture.BranchB);

    // The rows exist and name BranchB. Only the employee scope check stands between them and the caller.
    Assert.NotEmpty(await fixture.HistoryAsync(elsewhere));

    var history = await fixture.HistoryQueryAsync(fixture.BranchA, elsewhere);

    Assert.True(history.IsFailure);
    Assert.Equal(EmployeeErrors.NotFound, history.Error);

    // ================================================================================================
    // THE OTHER HALF OF THE RULE: HISTORICAL BRANCHES ARE NOT RE-AUTHORIZED INDIVIDUALLY.
    // ================================================================================================
    //
    // Access to history is authorized through the employee's CURRENT authorized scope, once. A past
    // assignment naming a branch the caller can no longer reach is still returned — the employee genuinely
    // worked there, and suppressing the row would silently corrupt the record rather than protect anything.
    //
    // Here an employee moves from BranchB to BranchA, and the reader's access to BranchB is then revoked.
    // The caller can still reach the employee through BranchA, so the BranchB row must still come back.
    var moved = await fixture.SeedEmployeeAsync("EMP-R14M", fixture.BranchB);
    await fixture.TransferAsync(moved, fixture.BranchB, fixture.BranchA);

    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchB);

    var retained = await fixture.HistoryQueryAsync(fixture.BranchA, moved);

    Assert.True(retained.IsSuccess, retained.IsFailure ? retained.Error.Code : null);
    Assert.Equal(2, retained.Value.Count);

    // The now-unauthorized branch is still named in the history.
    Assert.Contains(retained.Value, entry => entry.DestinationBranchId == fixture.BranchB);
    Assert.Contains(retained.Value, entry => entry.SourceBranchId == fixture.BranchB);
  }

  // ---- R15. IN SCOPE, THE HISTORY COMES BACK IN EFFECTIVE ORDER.
  [Fact]
  public async Task R15_Branch_history_is_returned_in_effective_order_with_the_initial_assignment_first()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var employeeId = await fixture.SeedEmployeeAsync("EMP-R15", fixture.BranchA);
    await fixture.TransferAsync(employeeId, fixture.BranchA, fixture.BranchB);

    var history = await fixture.HistoryQueryAsync(fixture.BranchB, employeeId);

    Assert.True(history.IsSuccess, history.IsFailure ? history.Error.Code : null);
    Assert.Equal(2, history.Value.Count);

    // The initial assignment records where employment STARTED, so it has no source.
    Assert.Null(history.Value[0].SourceBranchId);
    Assert.Equal(fixture.BranchA, history.Value[0].DestinationBranchId);

    Assert.Equal(fixture.BranchA, history.Value[1].SourceBranchId);
    Assert.Equal(fixture.BranchB, history.Value[1].DestinationBranchId);

    Assert.True(history.Value[0].EffectiveFromUtc <= history.Value[1].EffectiveFromUtc);
  }

  // ================================================================================================
  // R16. THE POINT-IN-TIME PRIMITIVE, AT EVERY BOUNDARY.
  // ================================================================================================
  //
  // "Where was this employee at time T" is the LAST row whose `EffectiveFromUtc` is at or before T. The read
  // returns a TOTAL order — `EffectiveFromUtc` then `AssignmentId` — so that answer is deterministic rather
  // than dependent on an ordering that merely happened to hold. Two assignments can share an instant, and
  // without the identifier tiebreaker the answer at that instant would be whichever row the server felt like
  // returning first.
  //
  // Every boundary is checked, because the interesting failures are all at the edges: an implementation
  // using `<` instead of `<=` is correct everywhere except exactly on a transfer instant.
  //
  // ---- AND IT IS ANSWERED FROM HISTORY, NEVER FROM Employee.BranchId.
  //
  // The employee's CURRENT branch answers "where are they now". Using it for a historical question would
  // report today's branch for every date in the past — which is precisely the attribution error the
  // append-only history exists to prevent, and it is asserted against below.
  [Fact]
  public async Task R16_The_history_answers_point_in_time_at_every_boundary()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var employeeId = await fixture.SeedEmployeeAsync("EMP-R16", fixture.BranchA);
    await fixture.TransferAsync(employeeId, fixture.BranchA, fixture.BranchB);
    await fixture.TransferAsync(employeeId, fixture.BranchB, fixture.BranchC);

    // Read from BranchC, the employee's current branch, so the read itself is in scope.
    var history = await fixture.HistoryQueryAsync(fixture.BranchC, employeeId, asUserId: fixture.AdministratorUserId);
    Assert.True(history.IsSuccess, history.IsFailure ? history.Error.Code : null);
    Assert.Equal(3, history.Value.Count);

    var initial = history.Value[0].EffectiveFromUtc;
    var firstTransfer = history.Value[1].EffectiveFromUtc;
    var secondTransfer = history.Value[2].EffectiveFromUtc;

    // 1. BEFORE THE INITIAL ASSIGNMENT — no row applies. The employee was not employed yet, and the correct
    //    answer is "nothing", not the earliest branch on file.
    Assert.Empty(history.Value.Where(entry => entry.EffectiveFromUtc <= initial.AddTicks(-1)));

    // 2. EXACTLY AT THE INITIAL INSTANT — inclusive, so the initial assignment applies.
    Assert.Equal(fixture.BranchA, BranchAt(history.Value, initial));

    // 3. BETWEEN THE TWO TRANSFERS.
    Assert.Equal(fixture.BranchB, BranchAt(history.Value, firstTransfer.AddTicks(1)));

    // 4. EXACTLY AT A TRANSFER INSTANT — inclusive, so the transfer has already taken effect. This is the
    //    case that separates `<=` from `<`.
    Assert.Equal(fixture.BranchB, BranchAt(history.Value, firstTransfer));
    Assert.Equal(fixture.BranchC, BranchAt(history.Value, secondTransfer));

    // 5. AFTER THE LATEST TRANSFER.
    Assert.Equal(fixture.BranchC, BranchAt(history.Value, secondTransfer.AddDays(1)));

    // ---- THE ATTRIBUTION CONTROL.
    //
    // The employee's CURRENT branch is BranchC. If historical attribution came from that column instead of
    // from the history, every assertion above would answer BranchC — so the fact that the earlier instants
    // answer BranchA and BranchB is what proves it does not.
    Assert.Equal(fixture.BranchC, await fixture.EmployeeBranchAsync(employeeId));
    Assert.NotEqual(fixture.BranchC, BranchAt(history.Value, initial));
  }

  // The primitive itself: the greatest EffectiveFromUtc at or before T, broken by assignment identifier.
  private static Guid BranchAt(IReadOnlyList<EmployeeBranchHistoryEntry> history, DateTimeOffset at) =>
    history
      .Where(entry => entry.EffectiveFromUtc <= at)
      .OrderBy(entry => entry.EffectiveFromUtc)
      .ThenBy(entry => entry.AssignmentId)
      .Last()
      .DestinationBranchId;

  // ---- R17. TERMINATED IS EXCLUDED FROM A ROUTINE SEARCH.
  [Fact]
  public async Task R17_Search_excludes_terminated_employees_by_default()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var active = await fixture.SeedEmployeeAsync("EMP-R17A", fixture.BranchA);
    var terminated = await fixture.SeedEmployeeAsync("EMP-R17T", fixture.BranchA);
    await fixture.TerminateAsync(terminated, fixture.BranchA);

    var page = await fixture.SearchAsync(fixture.BranchA, new EmployeeScopeRequest());

    Assert.Equal([active], page.Items.Select(item => item.EmployeeId));
    Assert.Equal(1, page.TotalCount);
    Assert.Equal(2, await fixture.EmployeeCountAsync());
  }

  // ---- R18. AND IS AVAILABLE WHEN ASKED FOR BY NAME, so audit reads remain possible.
  [Fact]
  public async Task R18_Search_includes_terminated_employees_when_the_status_is_named()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    await fixture.SeedEmployeeAsync("EMP-R18A", fixture.BranchA);
    var terminated = await fixture.SeedEmployeeAsync("EMP-R18T", fixture.BranchA);
    await fixture.TerminateAsync(terminated, fixture.BranchA);

    var page = await fixture.SearchAsync(
      fixture.BranchA,
      new EmployeeScopeRequest(),
      statuses: [EmployeeStatus.Terminated]);

    Assert.Equal([terminated], page.Items.Select(item => item.EmployeeId));
  }

  // ---- R19. THE EMPLOYEE NUMBER FILTER MATCHES THE NORMALIZED COLUMN.
  //
  // The stored column is binary-collated, so a case-sensitive comparison against the display value would
  // miss — and would disagree with the unique index that decides what "the same number" means on write.
  [Fact]
  public async Task R19_The_employee_number_filter_is_an_exact_normalized_match()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var employeeId = await fixture.SeedEmployeeAsync("EMP-R19", fixture.BranchA);
    await fixture.SeedEmployeeAsync("EMP-R19X", fixture.BranchA);

    var matched = await fixture.SearchAsync(
      fixture.BranchA, new EmployeeScopeRequest(), employeeNumber: "  emp-r19  ");

    Assert.Equal([employeeId], matched.Items.Select(item => item.EmployeeId));

    // EXACT, not a prefix: EMP-R19X must not answer a search for EMP-R19.
    Assert.Equal(1, matched.TotalCount);
  }

  // ---- R20. PAGING IS STABLE, because the ordering is total.
  //
  // FullName alone is not unique. An unstable sort silently drops rows from one page and repeats them on the
  // next, which looks like data loss and is nearly impossible to reproduce.
  [Fact]
  public async Task R20_Paging_is_stable_across_pages_with_duplicate_names()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    for (var index = 0; index < 5; index++)
    {
      await fixture.SeedEmployeeAsync($"EMP-R20-{index}", fixture.BranchA, name: "Identical Name");
    }

    var first = await fixture.SearchAsync(fixture.BranchA, new EmployeeScopeRequest(), pageNumber: 1, pageSize: 2);
    var second = await fixture.SearchAsync(fixture.BranchA, new EmployeeScopeRequest(), pageNumber: 2, pageSize: 2);
    var third = await fixture.SearchAsync(fixture.BranchA, new EmployeeScopeRequest(), pageNumber: 3, pageSize: 2);

    var seen = first.Items.Concat(second.Items).Concat(third.Items)
      .Select(item => item.EmployeeId).ToArray();

    Assert.Equal(5, seen.Length);
    Assert.Equal(5, seen.Distinct().Count());
    Assert.Equal(5, first.TotalCount);

    // ---- AND REPRODUCIBLE, not merely complete.
    //
    // Asking for the same page again returns the same rows in the same order. The tiebreaker is the
    // EmployeeId column, so the sequence is whatever SQL Server's uniqueidentifier collation makes of it —
    // deliberately NOT compared against .NET Guid ordering, which sorts the bytes differently and would make
    // this assert a fact about the client rather than about the paging.
    var firstAgain = await fixture.SearchAsync(fixture.BranchA, new EmployeeScopeRequest(), pageNumber: 1, pageSize: 2);

    Assert.Equal(
      first.Items.Select(item => item.EmployeeId),
      firstAgain.Items.Select(item => item.EmployeeId));
  }

  // ---- R21. THE TOTAL COUNT IS SCOPED TOO.
  //
  // A count computed from a wider query would leak the SIZE of the data outside the caller's scope even
  // though none of the rows were returned — and would make the pager offer pages that come back empty.
  [Fact]
  public async Task R21_The_total_count_is_computed_through_the_same_scoped_query()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    await fixture.SeedEmployeeAsync("EMP-R21A", fixture.BranchA);
    await fixture.SeedEmployeeAsync("EMP-R21B", fixture.BranchB);
    await fixture.SeedEmployeeAsync("EMP-R21C", fixture.BranchC);

    // ---- AND A ROW BELONGING TO ANOTHER TENANT ENTIRELY.
    //
    // Inserted with raw SQL, because no application path can create one: every write stamps the routed
    // tenant. It sits in the same physical table, in the caller's own company and branch, differing ONLY in
    // TenantId — so it is exactly the row that a lost tenant predicate would return.
    await fixture.InsertForeignTenantEmployeeAsync("EMP-R21F", fixture.CompanyA, fixture.BranchA);

    var page = await fixture.SearchAsync(fixture.BranchA, new EmployeeScopeRequest());

    Assert.Equal(1, page.TotalCount);
    Assert.Equal(4, await fixture.EmployeeCountAsync());

    // The foreign-tenant row is in the table and out of the result.
    Assert.DoesNotContain(page.Items, item => item.EmployeeNumber == "EMP-R21F");
  }

  // ---- R22. THE SCOPE IS RE-RESOLVED, NOT CACHED.
  //
  // Access is revocable inside a session's lifetime. The same graph — the same session, the same context —
  // is asked twice, with a grant removed in between, and the second answer must reflect the removal.
  [Fact]
  public async Task R22_Revoking_branch_access_mid_session_narrows_the_next_read()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var inA = await fixture.SeedEmployeeAsync("EMP-R22A", fixture.BranchA);
    var inB = await fixture.SeedEmployeeAsync("EMP-R22B", fixture.BranchB);

    using var graph = fixture.Graph(fixture.BranchA, fixture.NormalUserId);
    var reads = graph.Reads();
    var request = new EmployeeScopeRequest(BranchScope: EmployeeBranchScopeMode.AllAuthorizedBranches);

    var before = await graph.Scope().ResolveAsync(request);
    Assert.True(before.IsSuccess, before.IsFailure ? before.Error.Code : null);

    var firstPage = await reads.SearchEmployeesAsync(before.Value, new EmployeeSearchCriteria());
    Assert.Equal(2, firstPage.TotalCount);

    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchB);

    var after = await graph.Scope().ResolveAsync(request);
    Assert.True(after.IsSuccess, after.IsFailure ? after.Error.Code : null);

    var secondPage = await reads.SearchEmployeesAsync(after.Value, new EmployeeSearchCriteria());

    Assert.Equal([inA], secondPage.Items.Select(item => item.EmployeeId));
    Assert.DoesNotContain(inB, secondPage.Items.Select(item => item.EmployeeId));
  }

  // ---- R23. NO GLOBAL FILTER IS DOING THIS WORK.
  //
  // The composed model — the real one, with HR's contributor applied — carries a TENANT filter and nothing
  // else. If a company or branch filter were ever added, every proof above would still pass while the
  // explicit predicates quietly became decoration, and IgnoreQueryFilters() would become a tenant-wide read
  // of every employee.
  [Fact]
  public async Task R23_The_composed_model_filters_on_tenant_only()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var (context, _) = fixture.LoggingContext();
    await using (context)
    {
      var employee = context.Model.FindEntityType(typeof(Employee));
      Assert.NotNull(employee);

      var filter = employee!.GetQueryFilter()?.ToString();
      Assert.NotNull(filter);
      Assert.Contains("TenantId", filter!, StringComparison.Ordinal);
      Assert.DoesNotContain("CompanyId", filter!, StringComparison.Ordinal);
      Assert.DoesNotContain("BranchId", filter!, StringComparison.Ordinal);

      // The append-only history has no branch column at all, which is why its scope has to be inherited.
      var assignment = context.Model.FindEntityType(typeof(EmployeeBranchAssignment));
      Assert.DoesNotContain(
        assignment!.GetProperties(),
        property => string.Equals(property.Name, "BranchId", StringComparison.Ordinal));
    }
  }


  // MOVED ONTO THE FIXTURE in FP-007 Phase 3. Employee creation now names a department, and a department is
  // a real row in a real company — so the command cannot be built without asking the fixture which one it
  // seeded. Defaulting it here rather than at 41 call sites keeps every existing test asserting what it
  // always asserted; the tests that care about a SPECIFIC department pass one.

  // ================================================================================================
  // D — EMPLOYEE DEPARTMENT, AGAINST REAL SQL (FP-007 Phase 3 §13, §14, §15, §29, §30, §31)
  // ================================================================================================

  // ---- CREATION WRITES THE COLUMN AND THE FIRST HISTORY ROW IN ONE TRANSACTION.
  [Fact]
  public async Task D1_Creating_an_employee_persists_the_department_and_one_initial_record()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D1"));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
    Assert.Equal(fixture.DepartmentA, await fixture.EmployeeDepartmentAsync(created.Value));

    var history = Assert.Single(await fixture.DepartmentHistoryAsync(created.Value));

    Assert.Null(history.Source);
    Assert.Equal(fixture.DepartmentA, history.Destination);
  }

  [Fact]
  public async Task D2_Creating_an_employee_into_an_inactive_department_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(
      fixture.NewEmployee("EMP-D2", department: fixture.DepartmentAInactive));

    Assert.True(created.IsFailure);
    Assert.Equal(EmployeeErrors.DepartmentInactive.Code, created.Error.Code);

    // ---- AND NOTHING WAS WRITTEN. A refusal that left an employee behind would be worse than one that
    // wrote a bad department, because nothing would point at the inconsistency.
    Assert.Equal(0, await fixture.DepartmentHistoryRowCountAsync());
  }

  // A department in ANOTHER company. Reported absent rather than refused, so employee creation cannot be
  // used to probe which departments exist outside the caller's company.
  [Fact]
  public async Task D3_Creating_an_employee_into_another_companys_department_is_not_found()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(
      fixture.NewEmployee("EMP-D3", department: fixture.DepartmentB));

    Assert.True(created.IsFailure);
    Assert.Equal(EmployeeErrors.DepartmentNotFound.Code, created.Error.Code);
    Assert.Equal(0, await fixture.DepartmentHistoryRowCountAsync());
  }

  // ---- A VALID CHANGE MOVES THE COLUMN AND APPENDS EXACTLY ONE ROW.
  [Fact]
  public async Task D4_A_department_change_appends_one_record_and_leaves_the_first_untouched()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D4"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var second = await fixture.SeedDepartmentAsync(fixture.CompanyA, "DEPC", active: true);

    var changed = await graph.ChangeDepartment().HandleAsync(new ChangeEmployeeDepartmentCommand(
      created.Value, second, await fixture.RowVersionAsync(created.Value), "Reorg", "Northern division"));

    Assert.True(changed.IsSuccess, changed.IsFailure ? changed.Error.Code : null);
    Assert.Equal(second, await fixture.EmployeeDepartmentAsync(created.Value));

    var history = await fixture.DepartmentHistoryAsync(created.Value);

    Assert.Equal(2, history.Count);

    // The INITIAL row still says what it said. Append-only means the earlier record is never rewritten.
    Assert.Null(history[0].Source);
    Assert.Equal(fixture.DepartmentA, history[0].Destination);

    Assert.Equal(fixture.DepartmentA, history[1].Source);
    Assert.Equal(second, history[1].Destination);
  }

  [Fact]
  public async Task D5_A_change_to_the_same_department_is_refused_and_appends_nothing()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D5"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var changed = await graph.ChangeDepartment().HandleAsync(new ChangeEmployeeDepartmentCommand(
      created.Value, fixture.DepartmentA, await fixture.RowVersionAsync(created.Value)));

    Assert.True(changed.IsFailure);
    Assert.Equal(EmployeeErrors.DepartmentUnchanged.Code, changed.Error.Code);
    Assert.Equal(1, await fixture.DepartmentHistoryCountForAsync(created.Value));
  }

  [Fact]
  public async Task D6_A_change_into_an_inactive_department_is_refused_and_appends_nothing()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D6"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var changed = await graph.ChangeDepartment().HandleAsync(new ChangeEmployeeDepartmentCommand(
      created.Value, fixture.DepartmentAInactive, await fixture.RowVersionAsync(created.Value)));

    Assert.True(changed.IsFailure);
    Assert.Equal(EmployeeErrors.DepartmentInactive.Code, changed.Error.Code);
    Assert.Equal(fixture.DepartmentA, await fixture.EmployeeDepartmentAsync(created.Value));
    Assert.Equal(1, await fixture.DepartmentHistoryCountForAsync(created.Value));
  }

  [Fact]
  public async Task D7_A_change_into_another_companys_department_is_not_found()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D7"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var changed = await graph.ChangeDepartment().HandleAsync(new ChangeEmployeeDepartmentCommand(
      created.Value, fixture.DepartmentB, await fixture.RowVersionAsync(created.Value)));

    Assert.True(changed.IsFailure);
    Assert.Equal(EmployeeErrors.DepartmentNotFound.Code, changed.Error.Code);
    Assert.Equal(1, await fixture.DepartmentHistoryCountForAsync(created.Value));
  }

  [Fact]
  public async Task D8_A_stale_row_version_is_refused_and_appends_nothing()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D8"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var stale = await fixture.RowVersionAsync(created.Value);
    var second = await fixture.SeedDepartmentAsync(fixture.CompanyA, "DEPC", active: true);
    var third = await fixture.SeedDepartmentAsync(fixture.CompanyA, "DEPD", active: true);

    // One successful change advances the version, which makes the captured token stale.
    Assert.True((await graph.ChangeDepartment().HandleAsync(new ChangeEmployeeDepartmentCommand(
      created.Value, second, stale))).IsSuccess);

    var refused = await graph.ChangeDepartment().HandleAsync(
      new ChangeEmployeeDepartmentCommand(created.Value, third, stale));

    Assert.True(refused.IsFailure);
    Assert.Equal(EmployeeErrors.ConcurrencyConflict.Code, refused.Error.Code);
    Assert.Equal(second, await fixture.EmployeeDepartmentAsync(created.Value));
    Assert.Equal(2, await fixture.DepartmentHistoryCountForAsync(created.Value));
  }

  // ---- THE PERMISSION IS ENFORCED IN THE APPLICATION BOUNDARY, not only at the endpoint.
  [Fact]
  public async Task D9_A_caller_without_the_update_permission_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D9"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var second = await fixture.SeedDepartmentAsync(fixture.CompanyA, "DEPC", active: true);

    var refused = await graph.ChangeDepartment(canUpdate: false).HandleAsync(
      new ChangeEmployeeDepartmentCommand(
        created.Value, second, await fixture.RowVersionAsync(created.Value)));

    Assert.True(refused.IsFailure);
    Assert.Equal(EmployeeErrors.WritePermissionDenied.Code, refused.Error.Code);
    Assert.Equal(fixture.DepartmentA, await fixture.EmployeeDepartmentAsync(created.Value));
    Assert.Equal(1, await fixture.DepartmentHistoryCountForAsync(created.Value));
  }

  // ================================================================================================
  // D10 — THE CONCURRENCY PROOF (§13)
  // ================================================================================================
  //
  // Finance → HR and Finance → Operations, both holding the SAME expected version, both against real SQL.
  // Exactly one wins. The loser gets the existing concurrency failure and writes NOTHING — no history row,
  // no department move — because its SaveChanges finds no row matching the version it declared.
  //
  // Without this, two concurrent changes would produce two history rows claiming the same source and
  // different destinations, and the employee's log would fork.
  [Fact]
  public async Task D10_Two_concurrent_department_changes_leave_exactly_one_winner()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var seedGraph = fixture.Graph(fixture.BranchA);
    var created = await seedGraph.Create().HandleAsync(fixture.NewEmployee("EMP-D10"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var humanResources = await fixture.SeedDepartmentAsync(fixture.CompanyA, "DEPHR", active: true);
    var operations = await fixture.SeedDepartmentAsync(fixture.CompanyA, "DEPOP", active: true);

    var version = await fixture.RowVersionAsync(created.Value);

    // TWO INDEPENDENT GRAPHS — separate contexts, separate connections — so they genuinely contend inside
    // SQL Server rather than being serialised by a shared change tracker.
    using var first = fixture.Graph(fixture.BranchA);
    using var second = fixture.Graph(fixture.BranchA);

    var firstChange = first.ChangeDepartment().HandleAsync(
      new ChangeEmployeeDepartmentCommand(created.Value, humanResources, version));
    var secondChange = second.ChangeDepartment().HandleAsync(
      new ChangeEmployeeDepartmentCommand(created.Value, operations, version));

    var results = await Task.WhenAll(firstChange, secondChange);

    Assert.Equal(1, results.Count(result => result.IsSuccess));
    Assert.Equal(1, results.Count(result => result.IsFailure));

    // ONE final department, and it is one of the two that were attempted.
    var finalDepartment = await fixture.EmployeeDepartmentAsync(created.Value);

    Assert.True(finalDepartment == humanResources || finalDepartment == operations);

    // ---- AND EXACTLY ONE NEW HISTORY ROW. Two would mean the log forked; the initial record plus one
    // change is the only correct outcome.
    var history = await fixture.DepartmentHistoryAsync(created.Value);

    Assert.Equal(2, history.Count);
    Assert.Equal(fixture.DepartmentA, history[1].Source);
    Assert.Equal(finalDepartment, history[1].Destination);
  }

  // ================================================================================================
  // D11, D12 — THE INDEPENDENCE REGRESSIONS (§14, §15)
  // ================================================================================================

  // A BRANCH TRANSFER PRESERVES THE DEPARTMENT. +1 branch record, +0 department records.
  [Fact]
  public async Task D11_A_branch_transfer_preserves_the_department_and_writes_no_department_history()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D11"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var branchHistoryBefore = await fixture.HistoryRowCountAsync();

    var transferred = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
      created.Value, fixture.BranchB, EmployeeBranchTransferReason.OperationalNeed, null,
      await fixture.RowVersionAsync(created.Value)));

    Assert.True(transferred.IsSuccess, transferred.IsFailure ? transferred.Error.Code : null);

    Assert.Equal(fixture.BranchB, await fixture.EmployeeBranchAsync(created.Value));
    Assert.Equal(branchHistoryBefore + 1, await fixture.HistoryRowCountAsync());

    // The department did not move, and nothing was appended to its log.
    Assert.Equal(fixture.DepartmentA, await fixture.EmployeeDepartmentAsync(created.Value));
    Assert.Equal(1, await fixture.DepartmentHistoryCountForAsync(created.Value));
  }

  // AND THE CONVERSE. A department change does not move the branch or append branch history.
  [Fact]
  public async Task D12_A_department_change_preserves_the_branch_and_writes_no_branch_history()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D12"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var branchHistoryBefore = await fixture.HistoryRowCountAsync();
    var second = await fixture.SeedDepartmentAsync(fixture.CompanyA, "DEPC", active: true);

    Assert.True((await graph.ChangeDepartment().HandleAsync(new ChangeEmployeeDepartmentCommand(
      created.Value, second, await fixture.RowVersionAsync(created.Value)))).IsSuccess);

    Assert.Equal(fixture.BranchA, await fixture.EmployeeBranchAsync(created.Value));
    Assert.Equal(branchHistoryBefore, await fixture.HistoryRowCountAsync());
  }

  // TERMINATION KEEPS THE DEPARTMENT. Not cleared, not moved to UNASSIGNED, no history appended.
  [Fact]
  public async Task D13_Termination_preserves_the_department_and_writes_no_department_history()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-D13"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var terminated = await graph.Terminate().HandleAsync(new TerminateEmployeeCommand(
      created.Value, DateTimeOffset.UtcNow, EmployeeStatusChangeReason.Resignation,
      await fixture.RowVersionAsync(created.Value)));

    Assert.True(terminated.IsSuccess, terminated.IsFailure ? terminated.Error.Code : null);

    Assert.Equal(fixture.DepartmentA, await fixture.EmployeeDepartmentAsync(created.Value));
    Assert.Equal(1, await fixture.DepartmentHistoryCountForAsync(created.Value));
  }

  // ---- AN EMPLOYEE MAY REMAIN IN A DEPARTMENT THAT IS LATER DEACTIVATED (§16).
  //
  // Deactivating a department stops it accepting NEW members; it does not evict the ones it has. Moving
  // them automatically would rewrite where people work as a side effect of an org-structure change.
  [Fact]
  public async Task D14_An_employee_stays_in_a_department_that_is_deactivated_afterwards()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var department = await fixture.SeedDepartmentAsync(fixture.CompanyA, "DEPC", active: true);

    var created = await graph.Create().HandleAsync(
      fixture.NewEmployee("EMP-D14", department: department));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    await fixture.DeactivateDepartmentAsync(department);

    Assert.Equal(department, await fixture.EmployeeDepartmentAsync(created.Value));
    Assert.Equal(1, await fixture.DepartmentHistoryCountForAsync(created.Value));
  }

  // ================================================================================================
  // D15 — THE DEPARTMENT FILTER NEVER WIDENS BRANCH VISIBILITY (§25)
  // ================================================================================================
  //
  // Finance spans both branches. The caller is authorized for Riyadh only. Filtering by Finance must return
  // the Riyadh member and NOT the Jeddah one — the department filter narrows, and the branch scope still
  // decides who is visible.
  //
  // This is the leak the filter could plausibly have introduced: a department is company-wide, so a filter
  // written as "employees of this department" instead of "employees of this department WITHIN my scope"
  // would have quietly returned everyone.
  [Fact]
  public async Task D15_A_department_filter_still_obeys_branch_scope()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var finance = await fixture.SeedDepartmentAsync(fixture.CompanyA, "FIN", active: true);

    var inA = await fixture.Graph(fixture.BranchA).Create().HandleAsync(
      fixture.NewEmployee("EMP-RUH", department: finance));
    Assert.True(inA.IsSuccess, inA.IsFailure ? inA.Error.Code : null);

    var inB = await fixture.Graph(fixture.BranchB).Create().HandleAsync(
      fixture.NewEmployee("EMP-JED", department: finance));
    Assert.True(inB.IsSuccess, inB.IsFailure ? inB.Error.Code : null);

    // A caller who may see BranchA only.
    using var narrow = fixture.Graph(fixture.BranchA);

    var page = await narrow.Search().HandleAsync(new SearchEmployeesQuery(
      new EmployeeScopeRequest(
        EmployeeCompanyScopeMode.CurrentCompany,
        EmployeeBranchScopeMode.SelectedAuthorizedBranches,
        [fixture.BranchA]),
      DepartmentId: finance));

    Assert.True(page.IsSuccess, page.IsFailure ? page.Error.Code : null);

    var found = Assert.Single(page.Value.Items);

    Assert.Equal(inA.Value, found.EmployeeId);
    Assert.Equal(finance, found.Department.DepartmentId);

    // And the Jeddah member exists — so the single result above is scope working, not an empty department.
    Assert.Equal(finance, await fixture.EmployeeDepartmentAsync(inB.Value));
  }

  // ================================================================================================
  // D16 — THE DEPARTMENT SUB-OBJECT IS JOINED IN SQL, ON BOTH READS
  // ================================================================================================
  //
  // The wire tests assert the sub-object reaches the caller; they cannot assert it holds the RIGHT
  // department, because a stub returns whatever it was seeded with. This reads two employees in two
  // different departments through the real query and checks that each row carries its own — which is what
  // rules out the join being written on a constant, or on the first department the query happened to see.
  //
  // Both reads are covered in one test on purpose: the detail and the search share the join but not the
  // code path, and shipping it on one of the two is the likelier mistake than shipping it on neither.
  [Fact]
  public async Task D16_Both_employee_reads_carry_their_own_department_resolved()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var finance = await fixture.SeedDepartmentAsync(fixture.CompanyA, "FIN", active: true);
    var operations = await fixture.SeedDepartmentAsync(fixture.CompanyA, "OPS", active: true);

    using var graph = fixture.Graph(fixture.BranchA);

    var inFinance = await graph.Create().HandleAsync(
      fixture.NewEmployee("EMP-FIN", department: finance));
    Assert.True(inFinance.IsSuccess, inFinance.IsFailure ? inFinance.Error.Code : null);

    var inOperations = await graph.Create().HandleAsync(
      fixture.NewEmployee("EMP-OPS", department: operations));
    Assert.True(inOperations.IsSuccess, inOperations.IsFailure ? inOperations.Error.Code : null);

    // ---- THE DETAIL READ.
    var detail = await graph.Get().HandleAsync(new GetEmployeeQuery(inFinance.Value));

    Assert.True(detail.IsSuccess, detail.IsFailure ? detail.Error.Code : null);
    Assert.Equal(finance, detail.Value.Department.DepartmentId);
    Assert.Equal("FIN", detail.Value.Department.Code);
    Assert.Equal("Department FIN", detail.Value.Department.Name);

    // ---- AND THE SEARCH, WHERE THE TWO ROWS MUST DISAGREE WITH EACH OTHER.
    var page = await graph.Search().HandleAsync(new SearchEmployeesQuery(
      new EmployeeScopeRequest(
        EmployeeCompanyScopeMode.CurrentCompany,
        EmployeeBranchScopeMode.SelectedAuthorizedBranches,
        [fixture.BranchA])));

    Assert.True(page.IsSuccess, page.IsFailure ? page.Error.Code : null);

    var financeRow = page.Value.Items.Single(item => item.EmployeeId == inFinance.Value);
    var operationsRow = page.Value.Items.Single(item => item.EmployeeId == inOperations.Value);

    Assert.Equal("FIN", financeRow.Department.Code);
    Assert.Equal("Department FIN", financeRow.Department.Name);
    Assert.Equal("OPS", operationsRow.Department.Code);
    Assert.Equal("Department OPS", operationsRow.Department.Name);
  }

  // ==================================================================================================
  // P — THE POSITION BOUNDARY (FP-008 Phase 3, BR-HR-0006)
  // ==================================================================================================
  //
  // Every employee has a position, it is validated exactly as the department is, and it moves only through
  // `ChangePosition`. These are the department proofs re-asked of the third dimension — not because the code
  // is shared, but because it deliberately is not: three independent classifications, three sets of rules
  // that have to hold independently.

  // ---- P1. AN INACTIVE POSITION ACCEPTS NO NEW ARRIVAL (OD-POS-005, BRULE-POS-0013).
  //
  // The assignment reading gives Position the same asymmetry Department has: deactivating one keeps its
  // incumbents and refuses the next hire. This is the refusal half; P8 proves the incumbent half.
  [Fact]
  [Trait("Decision", "OD-POS-005")]
  public async Task P1_Creating_an_employee_into_an_inactive_position_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var refused = await fixture.Graph(fixture.BranchA).Create().HandleAsync(
      fixture.NewEmployee("EMP-P1", position: fixture.PositionAInactive));

    Assert.True(refused.IsFailure);
    Assert.Equal(EmployeeErrors.PositionInactive.Code, refused.Error.Code);
    Assert.Equal(0, await fixture.EmployeeCountAsync());
  }

  // ---- P2. A POSITION IN ANOTHER COMPANY IS ABSENT, NOT FORBIDDEN (BRULE-POS-0016, BR-PLT-0002).
  //
  // The position genuinely exists, so this proves the COMPANY predicate rather than a lookup miss — and the
  // answer is PositionNotFound rather than a distinct refusal, because naming it would make employee
  // creation a probe for positions in companies the caller cannot see.
  [Fact]
  [Trait("Rule", "BRULE-POS-0016")]
  public async Task P2_Creating_an_employee_into_another_companys_position_is_refused_as_absent()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var refused = await fixture.Graph(fixture.BranchA).Create().HandleAsync(
      fixture.NewEmployee("EMP-P2", position: fixture.PositionB));

    var nonexistent = await fixture.Graph(fixture.BranchA).Create().HandleAsync(
      fixture.NewEmployee("EMP-P2B", position: Guid.NewGuid()));

    Assert.True(refused.IsFailure);
    Assert.Equal(EmployeeErrors.PositionNotFound.Code, refused.Error.Code);

    // Indistinguishable from a position that does not exist at all. That equality IS the property.
    Assert.Equal(nonexistent.Error.Code, refused.Error.Code);
  }

  // ---- P3. THE ORDINARY UPDATE CANNOT REACH PositionId (DEC-POS-0010, BRULE-POS-0017).
  //
  // The X-test analog: not "does not", but CANNOT. There is no field on the command, so the change is not
  // expressible — and a profile update leaves the position and its history exactly where they were.
  [Fact]
  [Trait("Decision", "DEC-POS-0010")]
  public async Task P3_An_ordinary_profile_update_cannot_express_a_position_change()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-P3"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    // STRUCTURAL, not behavioural: the command type has no position member to set.
    Assert.DoesNotContain(
      typeof(UpdateEmployeeProfileCommand).GetProperties(),
      property => property.Name.Contains("Position", StringComparison.OrdinalIgnoreCase));

    Assert.True((await graph.Update().HandleAsync(new UpdateEmployeeProfileCommand(
      created.Value, "Renamed Person", null,
      await fixture.RowVersionAsync(created.Value)))).IsSuccess);

    Assert.Equal(fixture.PositionA, await fixture.EmployeePositionAsync(created.Value));
    Assert.Equal(1, await fixture.PositionHistoryCountForAsync(created.Value));
  }

  // ---- P4. A CHANGE MOVES THE COLUMN AND APPENDS EXACTLY ONE ROW (BRULE-POS-0018).
  [Fact]
  [Trait("Requirement", "FR-POS-0211")]
  public async Task P4_A_position_change_appends_exactly_one_history_row()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-P4"));
    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    // The initial record, written by creation.
    Assert.Equal(1, await fixture.PositionHistoryCountForAsync(created.Value));

    var destination = await fixture.SeedPositionAsync(fixture.CompanyA, "POSC", active: true);

    var changed = await graph.ChangePosition().HandleAsync(new ChangeEmployeePositionCommand(
      created.Value, destination, await fixture.RowVersionAsync(created.Value),
      "Promotion", "annual review"));

    Assert.True(changed.IsSuccess, changed.IsFailure ? changed.Error.Code : null);
    Assert.Equal(destination, await fixture.EmployeePositionAsync(created.Value));
    Assert.Equal(2, await fixture.PositionHistoryCountForAsync(created.Value));

    // ---- AND NEITHER OF THE OTHER TWO DIMENSIONS MOVED (BRULE-POS-0019).
    Assert.Equal(fixture.BranchA, await fixture.EmployeeBranchAsync(created.Value));
    Assert.Equal(fixture.DepartmentA, await fixture.EmployeeDepartmentAsync(created.Value));
    Assert.Equal(1, await fixture.DepartmentHistoryCountForAsync(created.Value));
  }

  // ---- P5. A CHANGE TO THE CURRENT POSITION IS REFUSED, not silently succeeded.
  //
  // A no-op that returned success would append a history row describing no movement at all.
  [Fact]
  public async Task P5_A_change_to_the_current_position_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-P5"));

    var refused = await graph.ChangePosition().HandleAsync(new ChangeEmployeePositionCommand(
      created.Value, fixture.PositionA, await fixture.RowVersionAsync(created.Value)));

    Assert.True(refused.IsFailure);
    Assert.Equal(EmployeeErrors.PositionUnchanged.Code, refused.Error.Code);
    Assert.Equal(1, await fixture.PositionHistoryCountForAsync(created.Value));
  }

  // ---- P6. A TERMINATED EMPLOYEE KEEPS THEIR POSITION AND CANNOT BE MOVED (BRULE-POS-0020).
  //
  // Both halves in one test, because they are one rule: termination does NOT clear the position — a
  // historical employment record without a job is unreadable — and a closed record's history stops moving.
  [Fact]
  [Trait("Rule", "BRULE-POS-0020")]
  public async Task P6_A_terminated_employee_retains_their_position_and_refuses_a_change()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-P6"));
    var destination = await fixture.SeedPositionAsync(fixture.CompanyA, "POSD", active: true);

    Assert.True((await graph.Terminate().HandleAsync(new TerminateEmployeeCommand(
      created.Value, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
      EmployeeStatusChangeReason.Resignation,
      await fixture.RowVersionAsync(created.Value)))).IsSuccess);

    // RETAINED. Termination cleared nothing.
    Assert.Equal(fixture.PositionA, await fixture.EmployeePositionAsync(created.Value));
    Assert.Equal(1, await fixture.PositionHistoryCountForAsync(created.Value));

    var refused = await graph.ChangePosition().HandleAsync(new ChangeEmployeePositionCommand(
      created.Value, destination, await fixture.RowVersionAsync(created.Value)));

    Assert.True(refused.IsFailure);
    Assert.Equal(EmployeeErrors.InvalidTransition.Code, refused.Error.Code);
    Assert.Equal(1, await fixture.PositionHistoryCountForAsync(created.Value));
  }

  // ---- P7. TWO CONCURRENT CHANGES LEAVE ONE WINNER (DEC-POS-0021).
  //
  // The assignment record carries no RowVersion of its own precisely because it is never updated;
  // concurrent changes serialize on the EMPLOYEE's. Both callers hold the same token, so the loser's save
  // finds no row to update and appends nothing — which is what stops two history rows describing one move.
  [Fact]
  [Trait("Decision", "DEC-POS-0021")]
  public async Task P7_Two_concurrent_position_changes_leave_one_winner_and_one_history_row()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-P7"));
    var first = await fixture.SeedPositionAsync(fixture.CompanyA, "POSE", active: true);
    var second = await fixture.SeedPositionAsync(fixture.CompanyA, "POSF", active: true);

    var token = await fixture.RowVersionAsync(created.Value);

    var won = await fixture.Graph(fixture.BranchA).ChangePosition().HandleAsync(
      new ChangeEmployeePositionCommand(created.Value, first, token));

    // The SAME token, current when both callers read it and not current now.
    var lost = await fixture.Graph(fixture.BranchA).ChangePosition().HandleAsync(
      new ChangeEmployeePositionCommand(created.Value, second, token));

    Assert.True(won.IsSuccess, won.IsFailure ? won.Error.Code : null);
    Assert.True(lost.IsFailure);
    Assert.Equal(EmployeeErrors.ConcurrencyConflict.Code, lost.Error.Code);

    Assert.Equal(first, await fixture.EmployeePositionAsync(created.Value));
    Assert.Equal(2, await fixture.PositionHistoryCountForAsync(created.Value));
  }

  // ---- P8. THE DESTINATION'S STATUS IS VALIDATED INSIDE THE WRITING TRANSACTION (DEC-POS-0021).
  //
  // Both orders of the race that matters: a position deactivated BEFORE the change is refused, and a
  // position deactivated AFTER it stays held by the employee who already has it. There is no interleave in
  // which a deactivated position quietly acquires a new holder.
  [Fact]
  [Trait("Decision", "DEC-POS-0021")]
  public async Task P8_A_position_deactivated_before_the_change_is_refused_and_after_it_is_retained()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-P8"));
    var destination = await fixture.SeedPositionAsync(fixture.CompanyA, "POSG", active: true);

    // ---- ORDER ONE: deactivate, then change. The handler reads the destination inside its own
    // transaction, so it sees Inactive and refuses.
    await fixture.DeactivatePositionDirectlyAsync(destination);

    var refused = await graph.ChangePosition().HandleAsync(new ChangeEmployeePositionCommand(
      created.Value, destination, await fixture.RowVersionAsync(created.Value)));

    Assert.True(refused.IsFailure);
    Assert.Equal(EmployeeErrors.PositionInactive.Code, refused.Error.Code);
    Assert.Equal(fixture.PositionA, await fixture.EmployeePositionAsync(created.Value));

    // ---- ORDER TWO: change, then deactivate. The employee keeps the position they moved into, because
    // OD-POS-005 qualifies the ASSIGNMENT and not the position's lifecycle status — the incumbent half of
    // the same ruling P1 proves the refusal half of.
    var other = await fixture.SeedPositionAsync(fixture.CompanyA, "POSH", active: true);

    Assert.True((await graph.ChangePosition().HandleAsync(new ChangeEmployeePositionCommand(
      created.Value, other, await fixture.RowVersionAsync(created.Value)))).IsSuccess);

    await fixture.DeactivatePositionDirectlyAsync(other);

    Assert.Equal(other, await fixture.EmployeePositionAsync(created.Value));
    Assert.Equal(2, await fixture.PositionHistoryCountForAsync(created.Value));
  }

  // ---- P9. THE PERMISSION IS ENFORCED IN THE APPLICATION BOUNDARY, not only at the endpoint.
  [Fact]
  [Trait("Decision", "DEC-POS-0019")]
  public async Task P9_A_caller_without_the_update_permission_cannot_change_a_position()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    var created = await graph.Create().HandleAsync(fixture.NewEmployee("EMP-P9"));
    var destination = await fixture.SeedPositionAsync(fixture.CompanyA, "POSI", active: true);

    var refused = await graph.ChangePosition(canUpdate: false).HandleAsync(
      new ChangeEmployeePositionCommand(
        created.Value, destination, await fixture.RowVersionAsync(created.Value)));

    Assert.True(refused.IsFailure);
    Assert.Equal(EmployeeErrors.WritePermissionDenied.Code, refused.Error.Code);
    Assert.Equal(1, await fixture.PositionHistoryCountForAsync(created.Value));
  }

  // ================================================================================================
  // D16 — THE EMPLOYEE NUMBER FILTER, WHICH HAD NO COVERAGE UNTIL FP-008 PHASE 2
  // ================================================================================================
  //
  // Added while auditing every HR search after `DEC-POS-0030`. The department search was broken from FP-007
  // because it filtered on a VALUE-CONVERTED property inside a predicate, which EF Core cannot translate;
  // this filter was checked at the same time and is a different shape — it compares
  // `NormalizedEmployeeNumber`, a plain string column, with `==`.
  //
  // That difference is the rule of thumb worth keeping: a converted member translates in a PROJECTION and
  // not in a PREDICATE, so a search must filter a plain column. This filter always did, which is why it
  // never broke. It had no test, though — so "it was fine" rested on reading it rather than running it, and
  // an uncovered search branch is precisely what let the department defect ship unnoticed.
  [Fact]
  [Trait("Decision", "DEC-POS-0030")]
  public async Task D16_The_employee_number_filter_matches_exactly_and_ignores_case()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var wanted = await fixture.Graph(fixture.BranchA).Create().HandleAsync(
      fixture.NewEmployee("EMP-0001"));
    Assert.True(wanted.IsSuccess, wanted.IsFailure ? wanted.Error.Code : null);

    var other = await fixture.Graph(fixture.BranchA).Create().HandleAsync(
      fixture.NewEmployee("EMP-0002"));
    Assert.True(other.IsSuccess, other.IsFailure ? other.Error.Code : null);

    using var graph = fixture.Graph(fixture.BranchA);

    async Task<int> CountForAsync(string employeeNumber)
    {
      var page = await graph.Search().HandleAsync(new SearchEmployeesQuery(
        new EmployeeScopeRequest(
          EmployeeCompanyScopeMode.CurrentCompany,
          EmployeeBranchScopeMode.AllAuthorizedBranches),
        EmployeeNumber: employeeNumber));

      Assert.True(page.IsSuccess, page.IsFailure ? page.Error.Code : null);

      return page.Value.Items.Count;
    }

    // FOUND, and exactly one — the filter is an equality on the normalized column, not a prefix.
    Assert.Equal(1, await CountForAsync("EMP-0001"));

    // CASE-INSENSITIVE, because both sides are upper-invariant: the stored column by the domain, the
    // criterion by the read service. The column's binary collation makes that normalization the only thing
    // standing between "emp-0001" and no results.
    Assert.Equal(1, await CountForAsync("emp-0001"));
    Assert.Equal(1, await CountForAsync("  EmP-0001  "));

    // NOT FOUND is an empty page rather than a failure — the same distinction the department search tests
    // draw, and the one that separates "no match" from "the query threw".
    Assert.Equal(0, await CountForAsync("EMP-9999"));

    // AND IT IS NOT A PREFIX MATCH. `EMP-000` would return both employees if it were.
    Assert.Equal(0, await CountForAsync("EMP-000"));
  }

  // ================================================================================================
  // FIXTURE
  // ================================================================================================

  // ================================================================================================
  // P — THE PERMISSION IS ACTUALLY GRANTABLE (FP-006P, ADR-012 r1.2, DEC-EMP-0030, AC-EMP-0040).
  // ================================================================================================
  //
  // ---- WHAT ESCAPED EVERY EARLIER TEST.
  //
  // Every Employee test above supplies its permissions directly, and the API tests mint them into a token.
  // Both are legitimate ways to test what a permission ALLOWS, and neither touches the question of whether
  // a real tenant role can ever hold one. It could not: the five names existed as constants that no catalog
  // defined, `AssignPermissionToRoleCommandHandler` refuses anything the catalog does not know, and
  // `Role.AssignPermission` needs a definition only the catalog can produce. Production answered 403 to
  // every caller while 2,202 tests passed.
  //
  // These proofs travel the real path end to end: composed catalog, real handler, real role, real role
  // assignment, real access-token claims, and a real Employee read authorized by NOTHING BUT those claims.

  // ---- P2. THE REAL HANDLER GRANTS IT, AND THE ASSIGNMENT IS PERSISTED.
  //
  // `AssignPermissionToRoleCommandHandler` over real SQL, with the real role repository and the real
  // platform unit of work. No claim minting anywhere: this is the path a tenant administrator's request
  // takes.
  [Fact]
  public async Task P2_The_real_role_assignment_handler_grants_an_hr_permission_through_the_composed_catalog()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var granted = await fixture.GrantThroughRealHandlerAsync(
      fixture.NormalUserId, HrPermissionNames.ViewEmployees, EmployeeFixture.ComposedCatalog());

    Assert.True(granted.IsSuccess, granted.IsFailure ? granted.Error.Code : null);

    // Read back from SQL, so "granted" means a row exists rather than a Result that said so.
    Assert.True(await fixture.RoleHoldsPermissionAsync(granted.Value, HrPermissionNames.ViewEmployees));
  }

  // ---- P2b. AND THE CATALOG IS WHAT MAKES THE DIFFERENCE.
  //
  // The identical command against the Platform-only catalog is refused as an invalid permission. This is
  // the blocker reproduced: without composition the handler cannot grant an HR permission at all.
  [Fact]
  public async Task P2b_The_same_command_is_refused_by_the_platform_only_catalog()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var refused = await fixture.GrantThroughRealHandlerAsync(
      fixture.NormalUserId, HrPermissionNames.ViewEmployees, new PlatformPermissionCatalog());

    Assert.True(refused.IsFailure);
    Assert.Equal(IdentityAccessErrors.InvalidPermission.Code, refused.Error.Code);
  }

  // ---- P3. GRANTED -> CLAIMS -> A REAL EMPLOYEE READ SUCCEEDS.
  //
  // The permission set handed to the read path comes from the REAL access-token claims provider, which
  // derives it from the role assignments in SQL and filters it through the real tenant-scope filter. If the
  // permission were not genuinely assignable, or did not survive to a token, this read could not succeed —
  // which is precisely the gap that let the blocker ship.
  [Fact]
  public async Task P3_A_permission_granted_to_a_real_role_authorizes_a_real_employee_read()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-P3", fixture.BranchA);

    var granted = await fixture.GrantThroughRealHandlerAsync(
      fixture.NormalUserId, HrPermissionNames.ViewEmployees, EmployeeFixture.ComposedCatalog());
    Assert.True(granted.IsSuccess, granted.IsFailure ? granted.Error.Code : null);

    var claimed = await fixture.ClaimedPermissionsAsync(fixture.NormalUserId);

    // The permission survived assignment, persistence, the claims join and the tenant-scope filter.
    Assert.Contains(HrPermissionNames.ViewEmployees, claimed);

    var read = await fixture.GetWithClaimedPermissionsAsync(
      fixture.BranchA, employeeId, fixture.NormalUserId, claimed);

    Assert.True(read.IsSuccess, read.IsFailure ? read.Error.Code : null);
    Assert.Equal(employeeId, read.Value.EmployeeId);
  }

  // ---- P4. NEGATIVE CONTROL: THE SAME USER WITHOUT THE PERMISSION IS REFUSED.
  //
  // Granted a real, legitimate tenant permission that is simply not this one. Everything else about the
  // request is identical, so the refusal is attributable to the functional permission and nothing else.
  [Fact]
  public async Task P4_A_real_role_without_the_hr_permission_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-P4", fixture.BranchA);

    var granted = await fixture.GrantThroughRealHandlerAsync(
      fixture.NormalUserId, PlatformPermissionNames.ViewCompanies, EmployeeFixture.ComposedCatalog());
    Assert.True(granted.IsSuccess, granted.IsFailure ? granted.Error.Code : null);

    var claimed = await fixture.ClaimedPermissionsAsync(fixture.NormalUserId);

    Assert.Contains(PlatformPermissionNames.ViewCompanies, claimed);
    Assert.DoesNotContain(HrPermissionNames.ViewEmployees, claimed);

    var read = await fixture.GetWithClaimedPermissionsAsync(
      fixture.BranchA, employeeId, fixture.NormalUserId, claimed);

    Assert.True(read.IsFailure);
    Assert.Equal(EmployeeErrors.ReadPermissionDenied.Code, read.Error.Code);
  }

  // ---- P5. NEGATIVE CONTROL: TENANT ADMINISTRATOR ALONE IS STILL REFUSED.
  //
  // `Platform.Tenant.Administer` widens the two SCOPE dimensions and grants no functional authority
  // (ADR-025 decision 8). Composing HR's permissions into the catalog must not have quietly made an
  // administrator able to read employees — which is the failure mode a shared catalog invites.
  [Fact]
  public async Task P5_A_tenant_administrator_without_the_hr_permission_is_still_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var employeeId = await fixture.SeedEmployeeAsync("EMP-P5", fixture.BranchA);

    // The administrator user is seeded holding Platform.Tenant.Administer and nothing else.
    var claimed = await fixture.ClaimedPermissionsAsync(fixture.AdministratorUserId);

    Assert.Contains(PlatformPermissionNames.AdministerTenant, claimed);
    Assert.DoesNotContain(HrPermissionNames.ViewEmployees, claimed);

    var read = await fixture.GetWithClaimedPermissionsAsync(
      fixture.BranchA, employeeId, fixture.AdministratorUserId, claimed);

    Assert.True(read.IsFailure);
    Assert.Equal(EmployeeErrors.ReadPermissionDenied.Code, read.Error.Code);

    // And the scope dimensions really were reachable for this user, so the refusal is the permission and
    // not an incidental scope failure.
    var withPermission = await fixture.GetWithClaimedPermissionsAsync(
      fixture.BranchA, employeeId, fixture.AdministratorUserId,
      [.. claimed, HrPermissionNames.ViewEmployees]);

    Assert.True(withPermission.IsSuccess, withPermission.IsFailure ? withPermission.Error.Code : null);
  }

  // ================================================================================================
  // FP-009 PHASE 1 — THE IMPORT PIPELINE, AGAINST REAL SQL SERVER
  // ================================================================================================
  //
  // Driven through the handler rather than a route, because Phase 1 exposes none. Everything asserted below
  // is a rule the handler owns: all-or-nothing, create-only, resolution by code under the caller's company,
  // and a run record for every outcome including the ones that write nothing else.
  //
  // Real SQL Server rather than a double, for the reason every boundary test here is: the unique indexes are
  // authoritative, the transaction is real, and "nothing was written" is a claim only a database can settle.

  // ---- I1. THE HAPPY PATH, AND THE FOUR ROWS EACH EMPLOYEE COSTS.
  //
  // Every imported employee goes through `CreateEmployeeCommandHandler`, so each one produces an employee,
  // an initial branch assignment, an initial department assignment and an initial position assignment. That
  // is `BRULE-DOC-0603` observable from the database: an import that assembled `Employee.Create` itself
  // would have to remember all four, and this is what would notice when it forgot one.
  [Fact]
  [Trait("Decision", "BRULE-DOC-0603")]
  public async Task I1_An_applied_import_creates_every_employee_through_the_ordinary_create_path()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nIMP-1,Layla Haddad,2026-03-01,DEPA,POSA\nIMP-2,Omar Nasser,2026-04-15,DEPA,POSA",
      "batch-i1", "people.csv", ByteCount: 128));

    Assert.True(report.IsSuccess, report.IsFailure ? report.Error.Code : null);
    Assert.Equal(EmployeeImportOutcome.Applied, report.Value.Outcome);
    Assert.Equal(2, report.Value.RowCount);
    Assert.Equal(2, report.Value.AcceptedCount);
    Assert.Equal(0, report.Value.RejectedCount);
    Assert.Empty(report.Value.Errors);

    Assert.Equal(2, await fixture.EmployeeCountAsync("IMP-"));

    // The ordinary create path's four rows per employee, none of them special-cased for imports.
    Assert.Equal(2, await fixture.CountAsync("EmployeeBranchAssignments", "IMP-"));
    Assert.Equal(2, await fixture.CountAsync("EmployeeDepartmentAssignments", "IMP-"));
    Assert.Equal(2, await fixture.CountAsync("EmployeePositionAssignments", "IMP-"));
  }

  // ---- I2. ALL OR NOTHING: ONE BAD ROW IN A THOUSAND LEAVES ZERO EMPLOYEES (OD-DOC-003).
  //
  // The headline observability criterion. A partial-success import would leave the system in a state no
  // single file describes, and the operator with no way to know which 999 landed.
  [Fact]
  [Trait("Decision", "OD-DOC-003")]
  public async Task I2_One_bad_row_in_a_thousand_leaves_no_employees_at_all()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var rows = Enumerable.Range(1, 1_000)
      .Select(index => index == 743
        ? $"BULK-{index},Broken Row,NOT-A-DATE,DEPA,POSA"
        : $"BULK-{index},Person {index},2026-03-01,DEPA,POSA");

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\n{string.Join('\n', rows)}", "batch-i2", "bulk.csv", ByteCount: 64_000));

    Assert.True(report.IsSuccess);
    Assert.Equal(EmployeeImportOutcome.Refused, report.Value.Outcome);
    Assert.Equal(1_000, report.Value.RowCount);
    Assert.Equal(0, report.Value.AcceptedCount);
    Assert.Equal(1, report.Value.RejectedCount);

    // The report names the row the operator has to go and fix, by the line number their editor shows.
    var error = Assert.Single(report.Value.Errors);
    Assert.Equal(744, error.RowNumber);
    Assert.Equal(EmployeeImportColumns.EmploymentDate, error.Column);

    // NOT ONE EMPLOYEE. This is the assertion the whole ruling exists for.
    Assert.Equal(0, await fixture.EmployeeCountAsync("BULK-"));

    // And the refused run is recorded, so the attempt is auditable and its key is consumed.
    Assert.Equal(1, await fixture.ImportRunCountAsync("BATCH-I2"));
  }

  // ---- I3. EVERY ROW IS VALIDATED, NOT THE FIRST (DEC-DOC-0003).
  //
  // A report naming one bad row in a thousand costs the operator a thousand round trips to find the rest.
  [Fact]
  [Trait("Decision", "DEC-DOC-0003")]
  public async Task I3_Every_bad_row_is_reported_rather_than_the_first()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nMULTI-1,,2026-03-01,DEPA,POSA" +
      "\nMULTI-2,Fine,2026-03-01,NOPE,POSA" +
      "\nMULTI-3,Fine,not-a-date,DEPA,NOPE",
      "batch-i3", "people.csv", ByteCount: 256));

    Assert.True(report.IsSuccess);
    Assert.Equal(EmployeeImportOutcome.Refused, report.Value.Outcome);
    Assert.Equal(3, report.Value.RejectedCount);

    // Every row appears, and so does every problem WITHIN a row — the same argument applies inside a row as
    // between rows, so row 4 reports both its date and its position code.
    Assert.Equal([2, 3, 4, 4], report.Value.Errors.Select(error => error.RowNumber));
    Assert.Equal(0, await fixture.EmployeeCountAsync("MULTI-"));
  }

  // ---- I4. AN IMPORT NEVER CREATES ORGANIZATIONAL STRUCTURE (BRULE-DOC-0601, OD-DOC-004).
  //
  // A typo becomes a rejected row, which is recoverable. A typo becoming a permanent org unit is not.
  [Fact]
  [Trait("Decision", "BRULE-DOC-0601")]
  public async Task I4_An_unresolvable_code_is_a_row_error_and_creates_nothing()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var departmentsBefore = await fixture.TableCountAsync("Departments");
    var positionsBefore = await fixture.TableCountAsync("Positions");

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nNEW-1,Layla,2026-03-01,FINANCE,POSA", "batch-i4", "people.csv", ByteCount: 96));

    Assert.True(report.IsSuccess);
    Assert.Equal(EmployeeImportOutcome.Refused, report.Value.Outcome);

    var error = Assert.Single(report.Value.Errors);
    Assert.Equal(EmployeeImportColumns.DepartmentCode, error.Column);
    Assert.Equal(EmployeeErrors.DepartmentNotFound.Code, error.Error.Code);

    Assert.Equal(departmentsBefore, await fixture.TableCountAsync("Departments"));
    Assert.Equal(positionsBefore, await fixture.TableCountAsync("Positions"));
  }

  // ---- I5. THE NO-STRUCTURE-ENUMERATION PROPERTY (OD-DOC-004).
  //
  // ================================================================================================
  // A CODE IN ANOTHER COMPANY AND A CODE THAT EXISTS NOWHERE MUST BE INDISTINGUISHABLE.
  // ================================================================================================
  //
  // Otherwise a file discovers which department codes exist in companies the caller cannot see, one
  // rejection message at a time — a spreadsheet as an enumeration oracle. The two rejections below are
  // compared FIELD BY FIELD rather than merely both being failures, because "both refused" would still hold
  // if one said `department.not_found` and the other said `company.scope_denied`.
  [Fact]
  [Trait("Decision", "OD-DOC-004")]
  public async Task I5_A_code_in_another_company_is_refused_identically_to_a_code_that_exists_nowhere()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    // A real department, in a company this caller is not acting within. It exists; it must not be found.
    await fixture.SeedDepartmentAsync(fixture.CompanyB, "SECRET", active: true);

    var elsewhere = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nENUM-1,Layla,2026-03-01,SECRET,POSA", "batch-i5a", "people.csv", ByteCount: 96));

    var nowhere = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nENUM-1,Layla,2026-03-01,ZZZZZZ,POSA", "batch-i5b", "people.csv", ByteCount: 96));

    var elsewhereError = Assert.Single(elsewhere.Value.Errors);
    var nowhereError = Assert.Single(nowhere.Value.Errors);

    Assert.Equal(elsewhere.Value.Outcome, nowhere.Value.Outcome);
    Assert.Equal(elsewhereError.Column, nowhereError.Column);
    Assert.Equal(elsewhereError.Error.Code, nowhereError.Error.Code);
    Assert.Equal(elsewhereError.Error.Message, nowhereError.Error.Message);

    // The premise, so this is not vacuous: the department really is there, in the other company.
    Assert.Equal(1, await fixture.DepartmentCountAsync(fixture.CompanyB, "SECRET"));
  }

  // ---- I6. CREATE-ONLY: AN EXISTING EMPLOYEE NUMBER IS A ROW ERROR, NEVER AN UPDATE (OD-DOC-002).
  //
  // An import that quietly updated would let a spreadsheet rewrite employment records with no history of the
  // edit. The existing employee is read back afterwards to prove it was not touched.
  [Fact]
  [Trait("Decision", "OD-DOC-002")]
  public async Task I6_An_existing_employee_number_is_a_row_error_and_the_existing_employee_is_untouched()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    await fixture.SeedEmployeeAsync("DUP-1", fixture.BranchA);
    var before = await fixture.FullNameAsync("DUP-1");

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nDUP-1,Rewritten Name,2026-03-01,DEPA,POSA",
      "batch-i6", "people.csv", ByteCount: 96));

    Assert.Equal(EmployeeImportOutcome.Refused, report.Value.Outcome);
    Assert.Equal(
      EmployeeErrors.NumberConflict.Code, Assert.Single(report.Value.Errors).Error.Code);

    Assert.Equal(before, await fixture.FullNameAsync("DUP-1"));
  }

  // ---- I7. A DUPLICATE WITHIN THE FILE, WHICH THE DATABASE CANNOT SEE UNTIL IT IS TOO LATE.
  //
  // Two rows claiming one employee number both pass the existence probe — neither exists yet — and would
  // then collide on the unique index partway through the apply, in a save the all-or-nothing rule rolls back
  // with nothing to tell the operator. Caught in validation so the report names the second row.
  [Fact]
  [Trait("Decision", "OD-DOC-003")]
  public async Task I7_Two_rows_claiming_one_employee_number_are_reported_before_anything_is_written()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nSAME-1,First,2026-03-01,DEPA,POSA\nsame-1,Second,2026-03-01,DEPA,POSA",
      "batch-i7", "people.csv", ByteCount: 160));

    Assert.Equal(EmployeeImportOutcome.Refused, report.Value.Outcome);

    var error = Assert.Single(report.Value.Errors);

    // The SECOND row is the one named — and the two differ only in case, which the normalized comparison is
    // what catches.
    Assert.Equal(3, error.RowNumber);
    Assert.Equal(EmployeeImportErrors.DuplicateWithinFile, error.Error);
    Assert.Equal(0, await fixture.EmployeeCountAsync("SAME-"));
  }

  // ---- I8. THE DRY RUN WRITES ITS RECORD AND NOTHING ELSE (FR-DOC-0101).
  [Fact]
  [Trait("Decision", "FR-DOC-0101")]
  public async Task I8_A_validate_only_run_writes_a_record_and_no_employees()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nDRY-1,Layla,2026-03-01,DEPA,POSA",
      "batch-i8", "people.csv", ByteCount: 96, ValidateOnly: true));

    Assert.Equal(EmployeeImportOutcome.Validated, report.Value.Outcome);
    Assert.Equal(1, report.Value.AcceptedCount);

    Assert.Equal(0, await fixture.EmployeeCountAsync("DRY-"));
    Assert.Equal(1, await fixture.ImportRunCountAsync("BATCH-I8"));
  }

  // ---- I9. THE KEY REPLAY ANSWERS "DID MY IMPORT HAPPEN?" WITHOUT IMPORTING AGAIN (DEC-DOC-0004).
  //
  // The operator whose connection dropped cannot tell whether the employees exist. Replaying the key answers
  // exactly that, and the second submission must create nothing — even though the file it carries is now a
  // file of duplicate employee numbers, which would ordinarily be a refusal.
  [Fact]
  [Trait("Decision", "DEC-DOC-0004")]
  public async Task I9_Replaying_an_import_key_returns_the_original_result_and_imports_nothing()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    const string file = "REPLAY-1,Layla,2026-03-01,DEPA,POSA";

    var first = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\n{file}", "batch-i9", "people.csv", ByteCount: 96));

    var second = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\n{file}", "BATCH-I9", "people.csv", ByteCount: 96));

    Assert.True(second.IsSuccess);
    Assert.True(second.Value.WasReplayed);
    Assert.False(first.Value.WasReplayed);

    // THE ORIGINAL RUN, not a new one that happens to look like it.
    Assert.Equal(first.Value.ImportRunId, second.Value.ImportRunId);
    Assert.Equal(first.Value.Outcome, second.Value.Outcome);
    Assert.Equal(first.Value.AcceptedCount, second.Value.AcceptedCount);

    // One employee and one run record, from two submissions. The key was matched case-insensitively, which
    // is what the normalized column is for.
    Assert.Equal(1, await fixture.EmployeeCountAsync("REPLAY-"));
    Assert.Equal(1, await fixture.ImportRunCountAsync("BATCH-I9"));
  }

  // ---- I10. A REFUSED RUN CONSUMES ITS KEY (DEC-DOC-0004).
  //
  // Releasing it would let the submission the key exists to make unrepeatable be replayed under it.
  [Fact]
  [Trait("Decision", "DEC-DOC-0004")]
  public async Task I10_A_refused_run_consumes_its_key_and_a_later_valid_file_replays_the_refusal()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var refused = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nKEY-1,Layla,not-a-date,DEPA,POSA", "batch-i10", "people.csv", ByteCount: 96));

    Assert.Equal(EmployeeImportOutcome.Refused, refused.Value.Outcome);

    var retried = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nKEY-1,Layla,2026-03-01,DEPA,POSA", "batch-i10", "people.csv", ByteCount: 96));

    Assert.True(retried.Value.WasReplayed);
    Assert.Equal(EmployeeImportOutcome.Refused, retried.Value.Outcome);
    Assert.Equal(0, await fixture.EmployeeCountAsync("KEY-"));
  }

  // ---- I11. A BAD HEADER IS A REFUSED RUN, NOT MERELY A 400 (DEC-DOC-0006).
  //
  // It consumed the key like any other attempt, and the audit trail records that somebody tried to import a
  // file this company would not accept. The `companyId` column is the one worth naming: it is refused for
  // being unrecognised, which is what "absent by construction" means at the header row.
  [Theory]
  [InlineData("employeeNumber,fullName,employmentDate,departmentCode,positionCode,companyId")]
  [InlineData("employeeNumber,fullName,departmentCode,positionCode")]
  [InlineData("")]
  [Trait("Decision", "DEC-DOC-0006")]
  public async Task I11_A_file_refused_before_its_first_row_still_records_a_run(string header)
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var key = $"header-{header.Length}";

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      header, key, "people.csv", ByteCount: 32));

    Assert.True(report.IsSuccess);
    Assert.Equal(EmployeeImportOutcome.Refused, report.Value.Outcome);
    Assert.Equal(0, report.Value.RowCount);

    // Zero rejected ROWS out of zero rows — the honest record of a file refused before any row was read.
    Assert.Equal(0, report.Value.RejectedCount);
    Assert.Equal(1, await fixture.ImportRunCountAsync(key.ToUpperInvariant()));
  }

  // ---- I12. THE CAPS, RE-CHECKED AT THE APPLICATION LAYER (DEC-DOC-0005, R2).
  //
  // The transport floor stops the bytes; this owns the contract's error shape. Both are refusals that write
  // a run record, so an operator who exceeded a cap can see the attempt in the history.
  [Fact]
  [Trait("Decision", "DEC-DOC-0005")]
  public async Task I12_A_file_over_either_cap_is_refused_by_the_application_and_recorded()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    // A deliberately small limit rather than a 10 MB body: the number is configuration (`DEC-DOC-0005`), so
    // the behaviour under test is the CHECK, and building ten megabytes to prove it would be theatre.
    var tiny = new EmployeeImportLimits(MaximumRows: 2, MaximumBytes: 64);

    var tooBig = await graph.Import(tiny).HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nCAP-1,Layla,2026-03-01,DEPA,POSA", "cap-bytes", "big.csv", ByteCount: 65));

    Assert.Equal(EmployeeImportOutcome.Refused, tooBig.Value.Outcome);
    Assert.Equal(
      EmployeeImportErrors.ByteLimitExceeded, Assert.Single(tooBig.Value.Errors).Error);

    var tooMany = await graph.Import(tiny).HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nCAP-1,A,2026-03-01,DEPA,POSA" +
      "\nCAP-2,B,2026-03-01,DEPA,POSA\nCAP-3,C,2026-03-01,DEPA,POSA",
      "cap-rows", "big.csv", ByteCount: 60));

    Assert.Equal(EmployeeImportOutcome.Refused, tooMany.Value.Outcome);
    Assert.Equal(
      EmployeeImportErrors.RowLimitExceeded, Assert.Single(tooMany.Value.Errors).Error);

    Assert.Equal(0, await fixture.EmployeeCountAsync("CAP-"));
    Assert.Equal(1, await fixture.ImportRunCountAsync("CAP-BYTES"));
    Assert.Equal(1, await fixture.ImportRunCountAsync("CAP-ROWS"));
  }

  // ---- I13. AN INACTIVE CLASSIFICATION IS NAMED, NOT REPORTED ABSENT.
  //
  // The distinction the single-create path draws, preserved here: an inactive department is one the caller
  // can see, so saying so tells them something true. A department in another company is not acknowledged at
  // all. An import that collapsed the two would either leak or mislead.
  [Fact]
  [Trait("Decision", "OD-DOC-004")]
  public async Task I13_An_inactive_department_is_named_while_another_companys_is_not_acknowledged()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    await fixture.SeedDepartmentAsync(fixture.CompanyA, "CLOSED", active: false);

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nINACT-1,Layla,2026-03-01,CLOSED,POSA",
      "batch-i13", "people.csv", ByteCount: 96));

    Assert.Equal(
      EmployeeErrors.DepartmentInactive.Code, Assert.Single(report.Value.Errors).Error.Code);
  }

  // ---- I14. THE RUN RECORD AND THE EMPLOYEES COMMIT TOGETHER (DEC-DOC-0006).
  //
  // An applied run whose record did not land would be five thousand employees nobody can account for; a run
  // record without its employees would be an audit trail describing something that did not happen.
  [Fact]
  [Trait("Decision", "DEC-DOC-0006")]
  public async Task I14_An_applied_run_records_what_it_did_and_the_record_is_append_only()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nRUN-1,Layla,2026-03-01,DEPA,POSA", "batch-i14", "people.csv", ByteCount: 96));

    Assert.Equal(EmployeeImportOutcome.Applied, report.Value.Outcome);

    var stored = await fixture.ImportRunAsync("BATCH-I14");

    Assert.NotNull(stored);
    Assert.Equal("Applied", stored!.Outcome);
    Assert.Equal(1, stored.RowCount);
    Assert.Equal(1, stored.AcceptedCount);
    Assert.Equal("people.csv", stored.FileName);

    // The display casing the caller submitted survives; the normalized column is what the index compares.
    Assert.Equal("batch-i14", stored.ImportKey);
    Assert.Equal("BATCH-I14", stored.NormalizedImportKey);

    // And the audit stamp is the infrastructure's, not the domain's — two different facts about one row.
    Assert.NotEqual(default, stored.CreatedUtc);
  }

  // ================================================================================================
  // FP-009 PHASE 1 — THE EXPORT PIPELINE, AGAINST REAL SQL SERVER
  // ================================================================================================

  // ---- X1. IT RUNS UNDER THE CALLER'S OWN SCOPE, AND THE FILE PROVES IT.
  //
  // A caller confined to one branch exports that branch's employees and nobody else's. This is the whole
  // security claim of the feature, and it is asserted against the BYTES rather than against the row list,
  // because the bytes are what leaves.
  [Fact]
  [Trait("Decision", "BRULE-DOC-0605")]
  public async Task X1_An_export_carries_only_the_employees_the_callers_scope_admits()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    await fixture.SeedEmployeeAsync("EXP-HERE", fixture.BranchA);
    await fixture.SeedEmployeeAsync("EXP-THERE", fixture.BranchB);

    using var confined = fixture.Graph(fixture.BranchA, fixture.NormalUserId);

    var exported = await confined.Export().HandleAsync(new ExportEmployeesQuery());

    Assert.True(exported.IsSuccess, exported.IsFailure ? exported.Error.Code : null);
    Assert.Contains("EXP-HERE", exported.Value.Content, StringComparison.Ordinal);
    Assert.DoesNotContain("EXP-THERE", exported.Value.Content, StringComparison.Ordinal);
  }

  // ---- X2. `nationalId` IS ABSENT FOR EVERY CALLER SHAPE (OD-DOC-006).
  //
  // ================================================================================================
  // NOT "EXCLUDED BY DEFAULT" — ABSENT FROM THE CONTRACT.
  // ================================================================================================
  //
  // There is no permission, parameter or caller for which the column appears, so this asserts across the
  // caller shapes that could plausibly differ: the confined user and the administrator, with and without
  // every HR permission the module defines. The employee genuinely HAS a national identifier, so the
  // absence is a distinction rather than an empty-database artefact.
  [Fact]
  [Trait("Decision", "OD-DOC-006")]
  public async Task X2_No_caller_shape_can_make_an_export_carry_a_national_identifier()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    await fixture.SeedEmployeeAsync("EXP-NID", fixture.BranchA, nationalId: "1098765432");

    foreach (var userId in new[] { fixture.AdministratorUserId, fixture.NormalUserId })
    {
      using var graph = fixture.Graph(fixture.BranchA, userId);

      var exported = await graph.Export().HandleAsync(new ExportEmployeesQuery());

      Assert.True(exported.IsSuccess, exported.IsFailure ? exported.Error.Code : null);

      // The value, the column name, and any casing of either.
      Assert.DoesNotContain("1098765432", exported.Value.Content, StringComparison.Ordinal);
      Assert.DoesNotContain("national", exported.Value.Content, StringComparison.OrdinalIgnoreCase);

      // And the run record's column-set line is what proves it to somebody reading the audit trail later,
      // which is a stronger guarantee than a rule nobody re-reads.
      Assert.DoesNotContain(
        "national", await fixture.ExportColumnSetAsync(exported.Value.ExportRunId),
        StringComparison.OrdinalIgnoreCase);
    }

    // The premise: the employee really does carry one, so the absence above is not vacuous.
    Assert.Equal("1098765432", await fixture.NationalIdAsync("EXP-NID"));
  }

  // ---- X3. THE `departmentId` FILTER IS INHERITED, NOT COPIED (TS-DOC-0016).
  //
  // The HR cleanup added this filter to the employee search's allowlist. The export takes the SAME criteria
  // type through the SAME shared predicate, so it honours the filter without anybody writing a second
  // conjunct — and the day a third filter is added, the export gets it for the same reason.
  [Fact]
  [Trait("Decision", "TS-DOC-0016")]
  public async Task X3_The_export_honours_the_department_filter_the_search_declares()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var finance = await fixture.SeedDepartmentAsync(fixture.CompanyA, "XFIN", active: true);

    await fixture.SeedEmployeeAsync("EXP-FIN", fixture.BranchA, departmentId: finance);
    await fixture.SeedEmployeeAsync("EXP-OTHER", fixture.BranchA);

    using var graph = fixture.Graph(fixture.BranchA);

    var all = await graph.Export().HandleAsync(new ExportEmployeesQuery());
    var filtered = await graph.Export().HandleAsync(new ExportEmployeesQuery(DepartmentId: finance));

    Assert.Contains("EXP-FIN", all.Value.Content, StringComparison.Ordinal);
    Assert.Contains("EXP-OTHER", all.Value.Content, StringComparison.Ordinal);

    // NARROWS, never widens — the property the shared predicate exists to make structural.
    Assert.Contains("EXP-FIN", filtered.Value.Content, StringComparison.Ordinal);
    Assert.DoesNotContain("EXP-OTHER", filtered.Value.Content, StringComparison.Ordinal);
    Assert.True(filtered.Value.RowCount < all.Value.RowCount);
  }

  // ---- X4. TERMINATED EMPLOYEES ARE EXCLUDED UNLESS ASKED FOR BY NAME (DEC-DOC-0009).
  //
  // The search has always behaved this way, and an export is a search that leaves the building. Asking by
  // name still works, so audit and payroll extracts remain possible.
  [Fact]
  [Trait("Decision", "DEC-DOC-0009")]
  public async Task X4_Terminated_employees_are_excluded_unless_the_caller_asks_for_them()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var employeeId = await fixture.SeedEmployeeAsync("EXP-GONE", fixture.BranchA);
    await fixture.TerminateAsync(employeeId);

    using var graph = fixture.Graph(fixture.BranchA);

    var routine = await graph.Export().HandleAsync(new ExportEmployeesQuery());
    var asked = await graph.Export().HandleAsync(
      new ExportEmployeesQuery(Statuses: [EmployeeStatus.Terminated]));

    Assert.DoesNotContain("EXP-GONE", routine.Value.Content, StringComparison.Ordinal);
    Assert.Contains("EXP-GONE", asked.Value.Content, StringComparison.Ordinal);
  }

  // ---- X5. THE RUN RECORD SAYS WHAT LEFT, AND UNDER WHICH SCOPE (SEC-DOC-0404).
  //
  // "Who exported employee data?" is answerable from the actor alone. "Could that person have exported THIS
  // employee?" is not, unless the scope in force at the time is recorded — and scope changes over time, so
  // reconstructing it later from current authorization is unsound.
  [Fact]
  [Trait("Decision", "SEC-DOC-0404")]
  public async Task X5_The_export_run_records_the_column_set_and_the_scope_that_was_in_force()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    await fixture.SeedEmployeeAsync("EXP-AUDIT", fixture.BranchA);

    using var graph = fixture.Graph(fixture.BranchA, fixture.NormalUserId);

    var exported = await graph.Export().HandleAsync(new ExportEmployeesQuery());

    var stored = await fixture.ExportRunAsync(exported.Value.ExportRunId);

    Assert.NotNull(stored);
    Assert.Equal(
      "employeeNumber,fullName,employmentDate,departmentCode,positionCode,status", stored!.ColumnSet);
    Assert.Equal(exported.Value.RowCount, stored.RowCount);

    // The scope the read actually ran under, sorted so two records describing one scope are textually
    // identical — an investigator comparing them is comparing scopes, not enumeration orders.
    Assert.Contains(fixture.CompanyA.ToString(), stored.ScopeCompanyIds, StringComparison.Ordinal);
    Assert.Contains(fixture.BranchA.ToString(), stored.ScopeBranchIds, StringComparison.Ordinal);
    Assert.DoesNotContain(fixture.BranchB.ToString(), stored.ScopeBranchIds, StringComparison.Ordinal);
  }

  // ---- X6. THE EXPORT PERMISSION IS CHECKED, AND IT IS NOT THE READ PERMISSION.
  //
  // A caller holding `HR.Employees.View` and not `HR.Employees.Export` can list employees and cannot extract
  // them to a file. That separation is the whole of `OD-DOC-005`: anyone who may view an employee must not
  // thereby be able to take the whole authorized set out of the system.
  [Fact]
  [Trait("Decision", "OD-DOC-005")]
  public async Task X6_A_caller_with_the_read_permission_but_not_the_export_permission_is_refused()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    await fixture.SeedEmployeeAsync("EXP-DENIED", fixture.BranchA);

    using var graph = fixture.Graph(fixture.BranchA);

    var refused = await graph.Export(canExport: false).HandleAsync(new ExportEmployeesQuery());

    Assert.True(refused.IsFailure);

    // NOTHING WAS RECORDED. A refusal is not an export, and an audit trail of non-events dilutes the one
    // signal this table exists to carry.
    Assert.Equal(0, await fixture.ExportRunCountAsync());

    // And the same caller WITH the permission succeeds, so the refusal is the permission and not an
    // incidental scope failure.
    Assert.True((await graph.Export().HandleAsync(new ExportEmployeesQuery())).IsSuccess);
  }

  // ---- X7. OVER THE CEILING IS A REFUSAL, NOT A TRUNCATION.
  //
  // Returning the first N of a larger set would hand the operator a file that LOOKS complete, and an
  // operator who believes they have everybody is worse off than one who was refused. No run record either:
  // no bytes left.
  [Fact]
  [Trait("Decision", "DEC-DOC-0005")]
  public async Task X7_An_export_larger_than_its_ceiling_is_refused_and_records_nothing()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    await fixture.SeedEmployeeAsync("EXP-C1", fixture.BranchA);
    await fixture.SeedEmployeeAsync("EXP-C2", fixture.BranchA);
    await fixture.SeedEmployeeAsync("EXP-C3", fixture.BranchA);

    using var graph = fixture.Graph(fixture.BranchA);

    var refused = await graph.Export().HandleAsync(new ExportEmployeesQuery(Ceiling: 2));

    Assert.True(refused.IsFailure);
    Assert.Equal(EmployeeImportErrors.RowLimitExceeded, refused.Error);
    Assert.Equal(0, await fixture.ExportRunCountAsync());

    // EXACTLY at the ceiling is fine — the read asks for one row past the limit precisely so "at" and
    // "over" are distinguishable rather than both looking like "full".
    var accepted = await graph.Export().HandleAsync(new ExportEmployeesQuery(Ceiling: 3));

    Assert.True(accepted.IsSuccess);
    Assert.Equal(3, accepted.Value.RowCount);
    Assert.Equal(1, await fixture.ExportRunCountAsync());
  }

  // ---- X8. THE ROUND TRIP CLOSES, AND THE ONE CASE IT REFUSES REFUSES HONESTLY (OD-DOC-010).
  //
  // ================================================================================================
  // RULED 2026-08-22: `status` IS A RECOGNIZED OPTIONAL IMPORT COLUMN WITH ONE ACCEPTED VALUE.
  // ================================================================================================
  //
  // Before the ruling this test demonstrated a gap: `api-contracts.md` gave the export a `status` column,
  // `AC-DOC-0002` said status was not an import column, and `DEC-DOC-0008` required an exported file to
  // re-import — three approved statements that could not all hold. The ruling closed it without reopening
  // any of them: the column is RECOGNIZED (so the allowlist stays strict and nothing is silently ignored),
  // empty or `Active` passes, and any other value is a row error whose message names the remedy.
  //
  // A `status=Terminated` export therefore refuses on re-import, and that refusal is the correct behaviour
  // rather than a residual gap: create-only cannot recreate a terminated person's employment history, and
  // resurrecting them as new Active hires would look like it worked.
  [Fact]
  [Trait("Decision", "OD-DOC-010")]
  public async Task X8_An_exported_file_re_imports_and_a_terminated_export_refuses_by_name()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    await fixture.SeedEmployeeAsync("EXP-TRIP", fixture.BranchA);

    using var graph = fixture.Graph(fixture.BranchA);

    var exported = await graph.Export().HandleAsync(new ExportEmployeesQuery());

    // THE CLOSURE. The export's own bytes, unmodified, under a fresh employee number — create-only means the
    // numbers must differ, which is the file being "edited and re-submitted" exactly as `DEC-DOC-0008` says.
    var reimported = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      exported.Value.Content.Replace("EXP-TRIP", "EXP-TRIP2", StringComparison.Ordinal),
      "round-trip", "employees.csv", ByteCount: 256));

    Assert.Equal(EmployeeImportOutcome.Applied, reimported.Value.Outcome);
    Assert.Empty(reimported.Value.Errors);
    Assert.Equal(1, await fixture.EmployeeCountAsync("EXP-TRIP2"));

    // ---- AND THE TERMINATED HALF REFUSES BY NAME, not by header rejection.
    var terminatedId = await fixture.SeedEmployeeAsync("EXP-DEAD", fixture.BranchA);
    await fixture.TerminateAsync(terminatedId);

    var terminatedExport = await graph.Export().HandleAsync(
      new ExportEmployeesQuery(Statuses: [EmployeeStatus.Terminated]));

    var refused = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      terminatedExport.Value.Content.Replace("EXP-DEAD", "EXP-DEAD2", StringComparison.Ordinal),
      "round-trip-terminated", "employees.csv", ByteCount: 256));

    Assert.Equal(EmployeeImportOutcome.Refused, refused.Value.Outcome);

    var error = Assert.Single(refused.Value.Errors);

    // A ROW error naming the STATUS column — which is what the ruling bought over a header rejection, because
    // the message can now tell the operator what to do about it.
    Assert.Equal(EmployeeImportColumns.Status, error.Column);
    Assert.Equal(EmployeeImportErrors.StatusNotCreatable, error.Error);
    Assert.Equal(0, await fixture.EmployeeCountAsync("EXP-DEAD2"));
  }

  // ---- X9. THE RULING'S PREMISE ABOUT THE DEFAULT EXPORT IS NARROWER THAN IT WAS STATED, AND THIS IS IT.
  //
  // ================================================================================================
  // A DEFAULT EXPORT IS NOT "ALL ACTIVE". IT IS ACTIVE **AND INACTIVE**.
  // ================================================================================================
  //
  // The ruling's reasoning was that "terminated are excluded (`DEC-DOC-0009`), so every exported row is
  // Active and the file re-imports legally". The first clause is right and the second does not follow: the
  // employee search's default has always been **Active AND Inactive** — both are current employment — and
  // `DEC-DOC-0009` narrows only the terminated third of that.
  //
  // So a default export containing an INACTIVE employee also refuses on re-import, with the same named row
  // error. That is the ruling's own logic applied consistently, and it is the RIGHT answer for the same
  // reason the terminated case is: creation produces `Active`, and an import that accepted `Inactive` and
  // created an Active employee would report success while doing something else.
  //
  // The round trip therefore closes for an export of ACTIVE employees, not for every default export. This
  // test exists so that fact is a property of the suite rather than a sentence in a report.
  [Fact]
  [Trait("Decision", "OD-DOC-010")]
  public async Task X9_A_default_export_containing_an_inactive_employee_also_refuses_on_re_import()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var employeeId = await fixture.SeedEmployeeAsync("EXP-IDLE", fixture.BranchA);
    await fixture.DeactivateAsync(employeeId);

    using var graph = fixture.Graph(fixture.BranchA);

    // NO STATUS FILTER — this is the ordinary export an operator gets by asking for one.
    var exported = await graph.Export().HandleAsync(new ExportEmployeesQuery());

    // The premise: the default export really does carry the inactive employee.
    Assert.Contains("EXP-IDLE", exported.Value.Content, StringComparison.Ordinal);
    Assert.Contains("Inactive", exported.Value.Content, StringComparison.Ordinal);

    var refused = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      exported.Value.Content.Replace("EXP-IDLE", "EXP-IDLE2", StringComparison.Ordinal),
      "round-trip-inactive", "employees.csv", ByteCount: 256));

    Assert.Equal(EmployeeImportOutcome.Refused, refused.Value.Outcome);
    Assert.Equal(
      EmployeeImportErrors.StatusNotCreatable, Assert.Single(refused.Value.Errors).Error);
  }

  // ================================================================================================
  // I15. THE REFUSAL RECORD SURVIVES THE TRANSACTION THAT ROLLED ITS ROWS BACK
  // ================================================================================================
  //
  // ---- IT GUARDS A CONTRACT, AND — SAID PLAINLY — IT WAS WRITTEN TO GUARD A DEFECT THAT DID NOT EXIST.
  //
  // The suspicion was that writing the refusal record inside the `await using var transaction` scope, just
  // after `RollbackAsync`, would save against an already-rolled-back transaction. Checked by reintroducing
  // that exact shape and running this test against real SQL: **it passed either way**, because
  // `EfUnitOfWork.RollbackAsync` DISPOSES the transaction and nulls its field, so the next save opens its
  // own. There was no defect.
  //
  // The test earns its place regardless, on the contract rather than the scare: `DEC-DOC-0006` makes the run
  // record the audit trail, and nothing else asserted that a refusal ARRIVING AFTER A ROLLBACK still leaves
  // one. A refusal whose record failed to persist would be an attempt that left no trace and a consumed
  // import key released — so the submission the key exists to make unrepeatable could be replayed. That
  // property now has a test instead of an assumption.
  //
  // ---- REACHING THE RACED PATH DETERMINISTICALLY.
  //
  // The path needs validation to PASS and the create to then FAIL, which is ordinarily a concurrency race.
  // Revoking the caller's branch assignment after the graph is built produces it exactly: import validation
  // never consults a branch — it checks the number, the name, the date and the two classification codes —
  // while `CreateEmployeeCommandHandler` resolves the current branch before it writes anything. So the file
  // validates cleanly, every row's create is refused, and the rollback-then-record sequence runs against a
  // REAL transaction rather than a no-op.
  [Fact]
  [Trait("Decision", "DEC-DOC-0006")]
  public async Task I15_A_refusal_after_rollback_still_persists_its_run_record()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    // Built FIRST: the graph establishes a session against the branch, and revoking beforehand would fail
    // the arrange step rather than the write.
    using var graph = fixture.Graph(fixture.BranchA, fixture.NormalUserId);

    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchA);

    var report = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nROLL-1,Layla Haddad,2026-03-01,DEPA,POSA",
      "rollback-guard", "people.csv", ByteCount: 96));

    Assert.True(report.IsSuccess, report.IsFailure ? report.Error.Code : null);
    Assert.Equal(EmployeeImportOutcome.Refused, report.Value.Outcome);
    Assert.Equal(0, report.Value.AcceptedCount);

    // ---- THE ROWS ARE GONE. The transaction rolled back, so no employee survives.
    Assert.Equal(0, await fixture.EmployeeCountAsync("ROLL-"));

    // ---- AND THE RECORD IS THERE. This is the assertion the whole test exists for: the save that writes it
    // runs AFTER the rolled-back transaction's scope has closed, so it commits on its own.
    var stored = await fixture.ImportRunAsync("ROLLBACK-GUARD");

    Assert.NotNull(stored);
    Assert.Equal("Refused", stored!.Outcome);
    Assert.Equal(0, stored.AcceptedCount);
    Assert.Equal("people.csv", stored.FileName);

    // ---- AND THE KEY IS THEREFORE CONSUMED, which is what the record existing MEANS.
    //
    // Replaying it returns the original refusal rather than importing — proving the record is not merely
    // present but authoritative. Branch access is still revoked, so a second attempt could not succeed
    // anyway; what this shows is that it never reaches the attempt.
    var replayed = await graph.Import().HandleAsync(new ImportEmployeesCommand(
      $"{ImportHeader}\nROLL-2,Other Person,2026-03-01,DEPA,POSA",
      "rollback-guard", "people.csv", ByteCount: 96));

    Assert.True(replayed.Value.WasReplayed);
    Assert.Equal("ROLLBACK-GUARD", stored.NormalizedImportKey);
    Assert.Equal(1, await fixture.ImportRunCountAsync("ROLLBACK-GUARD"));
  }

  // ================================================================================================
  // FP-009 PHASE 2 SLICE 1 — THE RUN-HISTORY READS (FR-DOC-0103, FR-DOC-0202)
  // ================================================================================================

  // ---- H1. NEWEST FIRST, PAGED, AND THE COUNTS COME BACK.
  [Fact]
  [Trait("Decision", "FR-DOC-0103")]
  public async Task H1_The_import_run_history_lists_the_companys_runs_newest_first()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    // Three runs, written by the real pipeline so the rows are the ones production writes.
    foreach (var (key, number) in new[] { ("hist-1", "H1-A"), ("hist-2", "H1-B"), ("hist-3", "H1-C") })
    {
      var applied = await graph.Import().HandleAsync(new ImportEmployeesCommand(
        $"{ImportHeader}\n{number},Person,2026-03-01,DEPA,POSA", key, "people.csv", ByteCount: 96));

      Assert.Equal(EmployeeImportOutcome.Applied, applied.Value.Outcome);
    }

    var listed = await graph.SearchImportRuns().HandleAsync(new SearchImportRunsQuery());

    Assert.True(listed.IsSuccess, listed.IsFailure ? listed.Error.Code : null);
    Assert.Equal(3, listed.Value.TotalCount);

    // NEWEST FIRST. Asserted as a non-increasing sequence rather than by naming an order, because two runs
    // can share a timestamp and the claim is about the ordering rule, not about these three rows.
    var executed = listed.Value.Items.Select(item => item.ExecutedUtc).ToArray();
    Assert.Equal(executed.OrderByDescending(value => value), executed);

    // The counts and the caller's own values come back — `DEC-DOC-0016` approves both as caller-supplied.
    var newest = listed.Value.Items.First();
    Assert.Equal(1, newest.RowCount);
    Assert.Equal(1, newest.AcceptedCount);
    Assert.Equal("people.csv", newest.FileName);
    Assert.Contains("hist-", newest.ImportKey, StringComparison.Ordinal);
  }

  // ---- H2. PAGINATION IS REFUSED, NOT CLAMPED.
  //
  // Silently reducing an over-large page would return a page the caller did not ask for and let them believe
  // they had seen the rest — the rule every other list in the module applies.
  // Each row names the parameter it refuses (T-260).
  [Theory]
  [InlineData(0, 50, false)]
  [InlineData(1, 0, true)]
  [InlineData(1, EmployeeRunHistoryCriteria.MaxPageSize + 1, true)]
  [Trait("Decision", "FR-DOC-0103")]
  public async Task H2_An_out_of_range_page_is_refused_rather_than_clamped(
    int page, int size, bool sizeIsTheFault)
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var imports = await graph.SearchImportRuns().HandleAsync(new SearchImportRunsQuery(page, size));
    var exports = await graph.SearchExportRuns().HandleAsync(new SearchExportRunsQuery(page, size));

    Assert.True(imports.IsFailure);
    Assert.True(exports.IsFailure);
    var expected = sizeIsTheFault
      ? EmployeeErrors.InvalidPageSize.Code
      : EmployeeErrors.InvalidPageNumber.Code;

    Assert.Equal(expected, imports.Error.Code);
    Assert.Equal(expected, exports.Error.Code);

    // And the boundary itself is accepted, so the refusal is a ceiling rather than an off-by-one.
    Assert.True((await graph.SearchImportRuns().HandleAsync(
      new SearchImportRunsQuery(1, EmployeeRunHistoryCriteria.MaxPageSize))).IsSuccess);
  }

  // ---- H3. SCOPED TO THE CALLER'S COMPANIES.
  //
  // A run belonging to a company the caller is not authorized for is absent — and the TOTAL is computed
  // through the same scoped query, so the count cannot disclose how many runs exist outside it either.
  [Fact]
  [Trait("Decision", "FR-DOC-0103")]
  public async Task H3_A_run_in_another_company_is_absent_and_uncounted()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    using (var inB = fixture.Graph(fixture.BranchB, company: fixture.CompanyB))
    {
      var applied = await inB.Import().HandleAsync(new ImportEmployeesCommand(
        $"{ImportHeader}\nH3-B,Person,2026-03-01,DEPB,POSB", "other-company", "b.csv", ByteCount: 96));

      Assert.Equal(EmployeeImportOutcome.Applied, applied.Value.Outcome);
    }

    // A caller whose authorized company set is CompanyA only.
    await fixture.RevokeCompanyAssignmentAsync(fixture.NormalUserId, fixture.CompanyB);

    using var confined = fixture.Graph(fixture.BranchA, fixture.NormalUserId);

    var listed = await confined.SearchImportRuns().HandleAsync(new SearchImportRunsQuery());

    Assert.True(listed.IsSuccess, listed.IsFailure ? listed.Error.Code : null);
    Assert.Equal(0, listed.Value.TotalCount);
    Assert.Empty(listed.Value.Items);
  }

  // ================================================================================================
  // H4. THE EXPORT LISTING CARRIES `ColumnSet` AND NOT THE SCOPE SNAPSHOT (DEC-DOC-0016)
  // ================================================================================================
  //
  // Asserted from BOTH directions, because either alone is weak:
  //
  //   * the TYPE has no property that could hold the snapshot, so no projection could leak it and no flag
  //     could re-add one — this is the guarantee;
  //   * and the stored row genuinely HAS a non-empty snapshot, so the absence above is a distinction rather
  //     than an artefact of an empty column.
  [Fact]
  [Trait("Decision", "DEC-DOC-0016")]
  public async Task H4_The_export_history_reports_the_column_set_and_never_the_scope_snapshot()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    await fixture.SeedEmployeeAsync("H4-1", fixture.BranchA);

    using var graph = fixture.Graph(fixture.BranchA);

    var exported = await graph.Export().HandleAsync(new ExportEmployeesQuery());
    Assert.True(exported.IsSuccess, exported.IsFailure ? exported.Error.Code : null);

    // THE PREMISE: the stored row really does carry a scope snapshot naming this company.
    var stored = await fixture.ExportRunAsync(exported.Value.ExportRunId);
    Assert.NotNull(stored);
    Assert.Contains(fixture.CompanyA.ToString(), stored!.ScopeCompanyIds, StringComparison.Ordinal);
    Assert.NotEmpty(stored.ScopeBranchIds);

    var listed = await graph.SearchExportRuns().HandleAsync(new SearchExportRunsQuery());

    Assert.True(listed.IsSuccess, listed.IsFailure ? listed.Error.Code : null);

    var item = Assert.Single(listed.Value.Items);

    // What DOES ship: the column set, in order, as a list rather than the stored comma-joined string.
    Assert.Equal(
      ["employeeNumber", "fullName", "employmentDate", "departmentCode", "positionCode", "status"],
      item.ColumnSet);
    Assert.Equal(1, item.RowCount);

    // ---- AND THE TYPE ITSELF CANNOT CARRY THE SNAPSHOT.
    //
    // The strong half. A value assertion would only prove this instance omitted it; this proves no instance
    // can hold it, which is what `DEC-DOC-0016` decided.
    var properties = typeof(EmployeeExportRunListItem).GetProperties().Select(p => p.Name).ToArray();

    Assert.DoesNotContain("ScopeCompanyIds", properties);
    Assert.DoesNotContain("ScopeBranchIds", properties);
    Assert.DoesNotContain(properties, name => name.Contains("Scope", StringComparison.Ordinal));

    // The import listing has no snapshot to omit, and equally must not grow one.
    Assert.DoesNotContain(
      typeof(EmployeeImportRunListItem).GetProperties().Select(p => p.Name),
      name => name.Contains("Scope", StringComparison.Ordinal));
  }

  // ---- H5. BOTH HISTORIES REQUIRE `HR.Employees.View`, AND NEITHER REQUIRES `Export`.
  //
  // Reading the record that an export happened is an employee read; PERFORMING one is the separately granted
  // capability. Gating the history on `Export` would mean the people who audit extractions must also be able
  // to perform them, which is the opposite of what separating the permission was for (`OD-DOC-005`).
  [Fact]
  [Trait("Decision", "DEC-DOC-0015")]
  public async Task H5_The_run_histories_are_gated_on_the_employee_read_permission()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    using var graph = fixture.Graph(fixture.BranchA);

    var importsDenied = await graph.SearchImportRuns(canView: false)
      .HandleAsync(new SearchImportRunsQuery());
    var exportsDenied = await graph.SearchExportRuns(canView: false)
      .HandleAsync(new SearchExportRunsQuery());

    Assert.True(importsDenied.IsFailure);
    Assert.True(exportsDenied.IsFailure);
    Assert.Equal(EmployeeErrors.ReadPermissionDenied.Code, importsDenied.Error.Code);
    Assert.Equal(EmployeeErrors.ReadPermissionDenied.Code, exportsDenied.Error.Code);

    // The same caller WITH the read permission — and WITHOUT `HR.Employees.Export` — succeeds on both, which
    // is the half that proves the export permission is not silently required here.
    Assert.True((await graph.SearchImportRuns().HandleAsync(new SearchImportRunsQuery())).IsSuccess);
    Assert.True((await graph.SearchExportRuns().HandleAsync(new SearchExportRunsQuery())).IsSuccess);
  }

  private const string ImportHeader =
    "employeeNumber,fullName,employmentDate,departmentCode,positionCode";

  // The persisted run record as raw SQL sees it. A record rather than a tuple so the assertions read as
  // claims about named columns.
  public sealed record ExportRunRow(
    int RowCount,
    string ColumnSet,
    string ScopeCompanyIds,
    string ScopeBranchIds,
    string ExecutedBy);

  public sealed record ImportRunRow(
    string ImportKey,
    string NormalizedImportKey,
    string FileName,
    int RowCount,
    int AcceptedCount,
    int RejectedCount,
    string Outcome,
    DateTimeOffset CreatedUtc);

  private sealed class EmployeeFixture : IAsyncDisposable
  {
    private const string ServerKey = "PrimarySqlServer";
    private const string Actor = "employee-c3-tests";
    private static readonly DateTimeOffset Seeded = DateTimeOffset.UtcNow.AddDays(-1);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];
    private readonly TenantStorageOptions storage = new();
    private readonly TenantCutoverFreezeOptions freeze = new();
    private string platformCatalog = string.Empty;
    private string tenantCatalog = string.Empty;

    public Guid Tenant { get; private set; }

    public Guid CompanyA { get; private set; }

    public Guid CompanyB { get; private set; }

    public Guid BranchA { get; private set; }

    public Guid BranchB { get; private set; }

    public Guid BranchC { get; private set; }

    // FP-007 Phase 3. One Active department per company, plus an inactive one in CompanyA, so employee
    // creation has something valid to name and the refusals have something real to be refused by.
    public Guid DepartmentA { get; private set; }

    public Guid DepartmentAInactive { get; private set; }

    public Guid DepartmentB { get; private set; }

    // ---- FP-008 PHASE 3. EVERY EMPLOYEE NEEDS ONE, so the fixture seeds them beside the departments.
    //
    // `PositionAInactive` exists so "refused because inactive" and "refused because absent" stay
    // distinguishable, exactly as `DepartmentAInactive` does for the department half.
    public Guid PositionA { get; private set; }

    public Guid PositionAInactive { get; private set; }

    public Guid PositionB { get; private set; }

    public long AdministratorUserId { get; private set; }

    public long NormalUserId { get; private set; }

    public static async Task<EmployeeFixture> CreateAsync()
    {
      var fixture = new EmployeeFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    private async Task InitializeAsync()
    {
      platformCatalog = $"SSAS_C3_Platform_{token}";
      tenantCatalog = $"SSAS_C3_Tenant_{token}";

      foreach (var catalog in new[] { platformCatalog, tenantCatalog })
      {
        await ExecuteAsync("master", $"CREATE DATABASE [{catalog}]");
      }

      // The tenant migration chain now includes AddHrEmployee, so the HR tables are created by the SAME
      // stream as Platform's — which is the whole point of the single tenant model (ADR-017).
      await using (var connection = new SqlConnection(ConnectionFor(tenantCatalog)))
      {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
          .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
            TenantPersistenceConstants.MigrationHistoryTable,
            TenantPersistenceConstants.MigrationHistorySchema))
          .Options;
        await using var context = new TenantDbContext(
          options, new TestUser(), new TestTenant(null), new TestClock(),
          modelContributors: [new HrTenantModelContributor()]);
        await context.Database.MigrateAsync();
      }

      storage.Servers[ServerKey] = new TenantStorageServerOptions { ConnectionString = Configured() };

      await using var platform = PlatformContext();
      await platform.Database.MigrateAsync();

      var databaseId = await RegisterAsync(platform, tenantCatalog);
      Tenant = await SeedTenantAsync(platform, "C3AAA", databaseId);

      AdministratorUserId = await SeedUserAsync("admin@example.test", administrator: true);
      NormalUserId = await SeedUserAsync("normal@example.test", administrator: false);

      CompanyA = await SeedCompanyAsync("CMPA");
      CompanyB = await SeedCompanyAsync("CMPB");

      BranchA = await SeedBranchAsync("BRA", main: true);
      BranchB = await SeedBranchAsync("BRB", main: false);
      BranchC = await SeedBranchAsync("BRC", main: false);

      DepartmentA = await SeedDepartmentAsync(CompanyA, "DEPA", active: true);
      DepartmentAInactive = await SeedDepartmentAsync(CompanyA, "DEPX", active: false);
      DepartmentB = await SeedDepartmentAsync(CompanyB, "DEPB", active: true);

      PositionA = await SeedPositionAsync(CompanyA, "POSA", active: true);
      PositionAInactive = await SeedPositionAsync(CompanyA, "POSX", active: false);
      PositionB = await SeedPositionAsync(CompanyB, "POSB", active: true);

      // The normal user reaches A and B, never C, so "authorized" and "exists" stay distinguishable.
      await GrantBranchAsync(NormalUserId, BranchA);
      await GrantBranchAsync(NormalUserId, BranchB);
      await GrantCompanyAsync(NormalUserId, CompanyA);
      await GrantCompanyAsync(NormalUserId, CompanyB);
    }

    // THE PRODUCTION GRAPH: real authorizers, real repository, real handlers, one scoped context.
    public EmployeeGraph Graph(Guid activeBranch, long? asUserId = null, Guid? company = null)
    {
      var tenantUserId = asUserId ?? AdministratorUserId;
      var sessionId = SessionFor(tenantUserId, activeBranch).GetAwaiter().GetResult();
      return new EmployeeGraph(this, tenantUserId, sessionId, company ?? CompanyA);
    }

    public TenantCompanyAccessResolver CompanyResolver()
    {
      var platform = PlatformContext(Tenant);
      return new TenantCompanyAccessResolver(
        platform, ReadContextFactory(), new TenantAdministratorAuthority(platform));
    }


    // ---- THE PERMISSION CATALOG THE HOST COMPOSES (FP-006P, ADR-012 r1.2).
    //
    // Platform's own definitions plus HR's contribution, exactly as Program.cs composes them. Built here
    // rather than stubbed, because the thing under test IS the composition.
    public static ComposedPermissionCatalog ComposedCatalog() =>
      new ComposedPermissionCatalog(new PlatformPermissionCatalog(), [new HrPermissionCatalogContributor()]);

    // ---- GRANT A PERMISSION THE WAY A TENANT ADMINISTRATOR'S REQUEST DOES.
    //
    // A real custom role, the real AssignPermissionToRoleCommandHandler over real SQL, and the real role
    // assignment onto the user. Nothing here writes a claim: if the catalog does not define the permission,
    // the handler refuses and this returns that refusal.
    public async Task<Result<long>> GrantThroughRealHandlerAsync(
      long tenantUserId, string permissionName, IPermissionCatalog catalog)
    {
      await using var platform = PlatformContext(Tenant);

      var role = Role.CreateCustom(
        Tenant, RoleName.Create($"Grant {Guid.NewGuid():N}"[..20]).Value, null, Guid.NewGuid(), Seeded);
      platform.Roles.Add(role);
      await platform.SaveChangesAsync();

      var handler = new AssignPermissionToRoleCommandHandler(
        new RoleRepository(platform),
        catalog,
        TestUnitOfWork.Platform(platform, new NoOpDispatcher()),
        new TestTenant(Tenant),
        new TestUser(),
        new TestClock());

      var assigned = await handler.HandleAsync(
        new AssignPermissionToRoleCommand(role.Id, permissionName, role.RowVersion));

      if (assigned.IsFailure)
      {
        return Result.Failure<long>(assigned.Error);
      }

      var user = await platform.TenantUsers
        .IgnoreQueryFilters()
        .SingleAsync(candidate => candidate.Id == tenantUserId);

      Assert.True(user.AssignRole(role, Actor, Guid.NewGuid(), Seeded).IsSuccess);
      await platform.SaveChangesAsync();

      return Result.Success(role.Id);
    }

    // Read back from SQL: "granted" has to mean a row exists, not that a Result said so.
    public async Task<bool> RoleHoldsPermissionAsync(long roleId, string permissionName)
    {
      await using var platform = PlatformContext(Tenant);

      return await platform.Database
        .SqlQueryRaw<int>(
          "SELECT COUNT(*) AS [Value] FROM [platform].[RolePermissionAssignments] " +
          "WHERE [RoleId] = {0} AND [PermissionName] = {1} AND [RemovedUtc] IS NULL",
          roleId,
          permissionName)
        .SingleAsync() == 1;
    }

    // ---- THE PERMISSIONS A REAL ACCESS TOKEN WOULD CARRY.
    //
    // The real AccessTokenClaimsProvider, over the real session, joining the real role assignments and
    // filtering through the real tenant-scope filter. This is the only permission source the read proofs
    // use, so a permission that could not be granted could not appear here.
    public async Task<IReadOnlyCollection<string>> ClaimedPermissionsAsync(long tenantUserId)
    {
      var sessionId = await SessionFor(tenantUserId, BranchA);

      await using var platform = PlatformContext(Tenant);

      var binding = await platform.TenantUsers
        .IgnoreQueryFilters()
        .Where(user => user.Id == tenantUserId)
        .Select(user => new { user.IdentityId })
        .SingleAsync();

      var claims = await new AccessTokenClaimsProvider(platform, ComposedCatalog()).GetClaimsAsync(
        sessionId,
        binding.IdentityId,
        tenantUserId,
        Tenant,
        AuthenticationClientId.Create("web").Value,
        1);

      Assert.True(claims.IsSuccess, claims.IsFailure ? claims.Error.Code : null);

      return claims.Value.Permissions;
    }

    // A real scoped Employee read whose FUNCTIONAL permission comes from the claims above and nowhere else.
    public async Task<Result<EmployeeDetail>> GetWithClaimedPermissionsAsync(
      Guid activeBranch, Guid employeeId, long tenantUserId, IReadOnlyCollection<string> permissions)
    {
      using var graph = Graph(activeBranch, tenantUserId, CompanyA);

      return await new GetEmployeeQueryHandler(graph.Scope(permissions), graph.Reads())
        .HandleAsync(new GetEmployeeQuery(employeeId));
    }

    public static Employee NewAggregate(string number) =>
      Employee.Create(
        EmployeeNumber.Create(number).Value,
        EmployeeFullName.Create("Spoof Attempt").Value,
        null,
        new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
        Actor,
        Guid.NewGuid(),
        DateTimeOffset.UtcNow).Value;

    // ---- THE READ PATH, ASKED THE WAY PRODUCTION ASKS IT (FP-006C4).
    //
    // Each call builds a fresh graph — a fresh session, a fresh scoped context — resolves a scope through the
    // real resolver, and reads through the real read service. The scope is never fabricated here, because a
    // test that fabricated one would prove nothing about the thing under test.
    public async Task<PagedResult<EmployeeSummary>> SearchAsync(
      Guid activeBranch,
      EmployeeScopeRequest request,
      long? asUserId = null,
      Guid? company = null,
      int pageNumber = 1,
      int pageSize = 50,
      string? employeeNumber = null,
      IReadOnlyCollection<EmployeeStatus>? statuses = null)
    {
      using var graph = Graph(activeBranch, asUserId ?? NormalUserId, company);

      var scope = await graph.Scope().ResolveAsync(request);
      Assert.True(scope.IsSuccess, scope.IsFailure ? scope.Error.Code : null);

      return await graph.Reads().SearchEmployeesAsync(
        scope.Value,
        new EmployeeSearchCriteria(pageNumber, pageSize, employeeNumber, statuses));
    }

    public async Task<Result<EmployeeDetail>> GetAsync(
      Guid activeBranch, Guid employeeId, long? asUserId = null, Guid? company = null)
    {
      using var graph = Graph(activeBranch, asUserId ?? NormalUserId, company);

      return await new GetEmployeeQueryHandler(graph.Scope(), graph.Reads())
        .HandleAsync(new GetEmployeeQuery(employeeId));
    }

    public async Task<Result<IReadOnlyList<EmployeeBranchHistoryEntry>>> HistoryQueryAsync(
      Guid activeBranch, Guid employeeId, long? asUserId = null, Guid? company = null)
    {
      using var graph = Graph(activeBranch, asUserId ?? NormalUserId, company);

      return await new GetEmployeeBranchHistoryQueryHandler(graph.Scope(), graph.Reads())
        .HandleAsync(new GetEmployeeBranchHistoryQuery(employeeId));
    }

    public async Task TransferAsync(Guid employeeId, Guid fromBranch, Guid toBranch)
    {
      using var graph = Graph(fromBranch);

      var moved = await graph.Transfer().HandleAsync(new TransferEmployeeCommand(
        employeeId, toBranch, EmployeeBranchTransferReason.Reorganisation, "read proofs",
        await RowVersionAsync(employeeId)));

      Assert.True(moved.IsSuccess, moved.IsFailure ? moved.Error.Code : null);
    }

    public async Task TerminateAsync(Guid employeeId, Guid activeBranch)
    {
      using var graph = Graph(activeBranch);

      var terminated = await graph.Terminate().HandleAsync(new TerminateEmployeeCommand(
        employeeId, DateTimeOffset.UtcNow, EmployeeStatusChangeReason.Resignation,
        await RowVersionAsync(employeeId)));

      Assert.True(terminated.IsSuccess, terminated.IsFailure ? terminated.Error.Code : null);
    }

    // A context that records the command text it sends, so a proof can assert on the SQL that actually
    // reached the server rather than on a configuration that was supposed to produce it. It is the REAL read
    // service that runs against it — only the logging is added.
    public (TenantDbContext Context, List<string> Sql) LoggingContext()
    {
      var sql = new List<string>();

      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(tenantCatalog))
        .LogTo(sql.Add, [RelationalEventId.CommandExecuting])
        .Options;

      return (
        new TenantDbContext(
          options, new TestUser(), new TestTenant(Tenant), new TestClock(),
          modelContributors: [new HrTenantModelContributor()]),
        sql);
    }

    // FP-009 added `nationalId` and `departmentId`, both optional and both defaulted to the previous
    // behaviour: the export tests need an employee that genuinely CARRIES a national identifier — otherwise
    // "the file does not contain it" would be true of an empty column — and one in a named department, so
    // the inherited filter has something to narrow to.
    public async Task<Guid> SeedEmployeeAsync(
      string number,
      Guid branchId,
      Guid? company = null,
      string name = "Seeded Person",
      string? nationalId = null,
      Guid? departmentId = null)
    {
      var created = await Graph(branchId, company: company).Create().HandleAsync(
        new CreateEmployeeCommand(number, name,
          new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), nationalId,
          departmentId ?? (company == CompanyB ? DepartmentB : DepartmentA),
          company == CompanyB ? PositionB : PositionA));
      Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
      return created.Value;
    }

    // ---- FP-009 EXPORT HELPERS.
    //
    // Terminating through the real handler rather than an UPDATE, because the status the export filter reads
    // has to be one the domain actually produced — a hand-written value could be one no transition allows.
    public async Task TerminateAsync(Guid employeeId)
    {
      var terminated = await Graph(BranchA).Terminate().HandleAsync(
        new TerminateEmployeeCommand(
          employeeId,
          new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
          EmployeeStatusChangeReason.Resignation,
          await RowVersionAsync(employeeId)));

      Assert.True(terminated.IsSuccess, terminated.IsFailure ? terminated.Error.Code : null);
    }

    public async Task DeactivateAsync(Guid employeeId)
    {
      var deactivated = await Graph(BranchA).Deactivate().HandleAsync(
        new DeactivateEmployeeCommand(
          employeeId,
          EmployeeStatusChangeReason.Administrative,
          await RowVersionAsync(employeeId)));

      Assert.True(deactivated.IsSuccess, deactivated.IsFailure ? deactivated.Error.Code : null);
    }

    public async Task<string?> NationalIdAsync(string employeeNumber)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT [NationalId] FROM [tenant].[Employees] " +
        $"WHERE [NormalizedEmployeeNumber] = N'{employeeNumber.ToUpperInvariant()}'";

      return (await command.ExecuteScalarAsync())?.ToString();
    }

    public Task<int> ExportRunCountAsync() => ScalarIntAsync(
      "SELECT COUNT(*) FROM [tenant].[EmployeeExportRuns]");

    public async Task<string> ExportColumnSetAsync(Guid exportRunId) =>
      (await ExportRunAsync(exportRunId))?.ColumnSet ?? string.Empty;

    public async Task<ExportRunRow?> ExportRunAsync(Guid exportRunId)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT [RowCount], [ColumnSet], [ScopeCompanyIds], [ScopeBranchIds], [ExecutedBy] " +
        $"FROM [tenant].[EmployeeExportRuns] WHERE [ExportRunId] = '{exportRunId:D}'";

      await using var reader = await command.ExecuteReaderAsync();

      return await reader.ReadAsync()
        ? new ExportRunRow(
          reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
          reader.GetString(4))
        : null;
    }


    public async Task<Guid?> EmployeeBranchAsync(Guid employeeId) =>
      await ScalarGuidAsync("Employees", "BranchId", "EmployeeId", employeeId);

    public async Task<Guid?> EmployeeCompanyAsync(Guid employeeId) =>
      await ScalarGuidAsync("Employees", "CompanyId", "EmployeeId", employeeId);

    public async Task<int> EmployeeCountAsync() => await CountAsync("Employees");

    // ---- A ROW BELONGING TO A DIFFERENT TENANT, WRITTEN THE ONLY WAY IT CAN BE.
    //
    // No application path can produce one: every write stamps the routed tenant and the boundary refuses a
    // spoofed value. Raw SQL is therefore the only way to create the negative control that proves the tenant
    // predicate is doing work — a row in the same table, the same company and the same branch as the
    // caller's own data, differing ONLY in TenantId.
    //
    // DepartmentId is supplied from FP-007 Phase 3 because the column is NOT NULL and the foreign key is
    // real. The foreign tenant's row therefore points at THIS tenant's department, which is harmless and
    // deliberate: the row exists solely to be excluded by the tenant predicate, and what makes it a valid
    // negative control is that it differs from the caller's data in TenantId ALONE.
    public Task InsertForeignTenantEmployeeAsync(string number, Guid companyId, Guid branchId) => ExecuteAsync(
      tenantCatalog,
      $"""
      INSERT INTO [tenant].[Employees]
        ([EmployeeId], [TenantId], [CompanyId], [BranchId], [DepartmentId], [PositionId],
         [EmployeeNumber],
         [NormalizedEmployeeNumber], [FullName], [EmploymentDate], [Status], [StatusChangeReasonCode],
         [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [ModifiedUtc])
      VALUES
        ('{Guid.NewGuid():D}', '{Guid.NewGuid():D}', '{companyId:D}', '{branchId:D}', '{DepartmentFor(companyId):D}',
         '{PositionFor(companyId):D}',
         N'{number}', N'{number}',
         N'Other Tenant Person', SYSDATETIMEOFFSET(), N'Active', N'Created', SYSDATETIMEOFFSET(),
         N'test', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())
      """);

    // The seeded department belonging to a given company. The foreign-tenant control above and the
    // per-company create helpers both need it, and picking the wrong company's would fail the foreign key
    // rather than silently mis-seed.
    public Guid DepartmentFor(Guid companyId) => companyId == CompanyB ? DepartmentB : DepartmentA;

    // The position twin. Note what this row does NOT need: the foreign key is on `PositionId` alone, so an
    // other-tenant employee may point at this tenant's position — which is exactly what makes the seeded
    // row a usable stand-in for "a row this caller must never see" without inventing a second position.
    public Guid PositionFor(Guid companyId) => companyId == CompanyB ? PositionB : PositionA;

    public async Task<int> HistoryRowCountAsync() => await CountAsync("EmployeeBranchAssignments");

    // ---- FP-007 PHASE 3 READ HELPERS.
    //
    // Deliberately raw SQL against the tables rather than the read service: these assertions are about what
    // was PERSISTED, and routing them through a scoped read would let a scoping bug hide a persistence bug.
    public async Task<Guid?> EmployeeDepartmentAsync(Guid employeeId) =>
      await ScalarGuidAsync("Employees", "DepartmentId", "EmployeeId", employeeId);

    public async Task<int> DepartmentHistoryRowCountAsync() =>
      await CountAsync("EmployeeDepartmentAssignments");

    public async Task<int> PositionHistoryCountForAsync(Guid employeeId)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        $"SELECT COUNT(*) FROM [tenant].[EmployeePositionAssignments] WHERE [EmployeeId] = '{employeeId}'";

      return (int)(await command.ExecuteScalarAsync())!;
    }

    public async Task<Guid> EmployeePositionAsync(Guid employeeId)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        $"SELECT [PositionId] FROM [tenant].[Employees] WHERE [EmployeeId] = '{employeeId}'";

      return (Guid)(await command.ExecuteScalarAsync())!;
    }

    public Task DeactivatePositionDirectlyAsync(Guid positionId) =>
      ExecuteAsync($"""
        UPDATE [tenant].[Positions]
        SET [Status] = N'Inactive', [StatusChangedUtc] = SYSDATETIMEOFFSET()
        WHERE [PositionId] = '{positionId}';
        """);

    public async Task<int> DepartmentHistoryCountForAsync(Guid employeeId)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        $"SELECT COUNT(*) FROM [tenant].[EmployeeDepartmentAssignments] WHERE [EmployeeId] = '{employeeId}'";

      return (int)(await command.ExecuteScalarAsync())!;
    }

    // The department log for one employee, in the deterministic order §12 requires: EffectiveFromUtc first,
    // then the identifier, so two changes inside one clock tick still read back the same way every time.
    public async Task<IReadOnlyList<(Guid? Source, Guid Destination, string ChangedBy)>>
      DepartmentHistoryAsync(Guid employeeId)
    {
      var rows = new List<(Guid?, Guid, string)>();

      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = $"""
        SELECT [SourceDepartmentId], [DestinationDepartmentId], [ChangedBy]
        FROM [tenant].[EmployeeDepartmentAssignments]
        WHERE [EmployeeId] = '{employeeId}'
        ORDER BY [EffectiveFromUtc], [EmployeeDepartmentAssignmentId]
        """;

      await using var reader = await command.ExecuteReaderAsync();

      while (await reader.ReadAsync())
      {
        rows.Add((
          reader.IsDBNull(0) ? null : reader.GetGuid(0),
          reader.GetGuid(1),
          reader.GetString(2)));
      }

      return rows;
    }

    public async Task<byte[]> RowVersionAsync(Guid employeeId)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT [RowVersion] FROM [tenant].[Employees] WHERE [EmployeeId] = @id";
      command.Parameters.AddWithValue("@id", employeeId);
      return (byte[])(await command.ExecuteScalarAsync())!;
    }

    public async Task<IReadOnlyList<HistoryRow>> HistoryAsync(Guid employeeId)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = """
        SELECT [SourceBranchId], [DestinationBranchId], [EffectiveFromUtc], [ReasonCode]
        FROM [tenant].[EmployeeBranchAssignments] WHERE [EmployeeId] = @id
        """;
      command.Parameters.AddWithValue("@id", employeeId);

      var rows = new List<HistoryRow>();
      await using var reader = await command.ExecuteReaderAsync();
      while (await reader.ReadAsync())
      {
        rows.Add(new HistoryRow(
          await reader.IsDBNullAsync(0) ? null : reader.GetGuid(0),
          reader.GetGuid(1),
          reader.GetDateTimeOffset(2),
          reader.GetString(3)));
      }

      return rows;
    }

    public Task RevokeBranchAssignmentAsync(long tenantUserId, Guid branchId) => ExecuteAsync(
      platformCatalog,
      $"DELETE FROM [platform].[UserBranchAccess] WHERE [TenantId] = '{Tenant:D}' AND [TenantUserId] = {tenantUserId} AND [BranchId] = '{branchId:D}'");

    public Task RevokeCompanyAssignmentAsync(long tenantUserId, Guid companyId) => ExecuteAsync(
      platformCatalog,
      $"DELETE FROM [platform].[UserCompanyAccess] WHERE [TenantId] = '{Tenant:D}' AND [TenantUserId] = {tenantUserId} AND [CompanyId] = '{companyId:D}'");

    public Task RevokeAdministratorAuthorityAsync() => ExecuteAsync(
      platformCatalog,
      $"UPDATE [platform].[RolePermissionAssignments] SET [RemovedUtc] = SYSDATETIMEOFFSET(), [RemovedBy] = 'test' WHERE [TenantId] = '{Tenant:D}'");

    public Task DeactivateBranchAsync(Guid branchId) => ExecuteAsync(
      tenantCatalog,
      $"UPDATE [tenant].[Branches] SET [IsActive] = 0, [IsMainBranch] = 0 WHERE [BranchId] = '{branchId:D}'");

    public Task DeactivateCompanyAsync(Guid companyId) => ExecuteAsync(
      tenantCatalog,
      $"UPDATE [tenant].[Companies] SET [Status] = N'Inactive', [StatusChangedUtc] = SYSDATETIMEOFFSET(), [StatusChangeReasonCode] = N'Administrative' WHERE [CompanyId] = '{companyId:D}'");

    public async Task RevokeSessionAsync(long sessionId)
    {
      await using var platform = PlatformContext(Tenant);
      var session = await platform.Set<SSAS.Platform.Domain.Authentication.AuthenticationSession>()
        .SingleAsync(candidate => candidate.Id == sessionId);

      Assert.True(session.Revoke(
        AuthenticationSessionRevocationReason.Administrative, "test", Guid.NewGuid(),
        DateTimeOffset.UtcNow).IsSuccess);
      await platform.SaveChangesAsync();
    }

    public async Task<int> BranchAccessRowCountAsync(long tenantUserId)
    {
      await using var platform = PlatformContext();
      return await platform.UserBranchAccess
        .CountAsync(access => access.TenantId == Tenant && access.TenantUserId == tenantUserId);
    }

    public async Task<int> CompanyAccessRowCountAsync(long tenantUserId)
    {
      await using var platform = PlatformContext();
      return await platform.UserCompanyAccess
        .CountAsync(access => access.TenantId == Tenant && access.TenantUserId == tenantUserId);
    }

    public async Task GrantBranchAsync(long tenantUserId, Guid branchId)
    {
      await using var platform = PlatformContext();
      platform.UserBranchAccess.Add(UserBranchAccess.Create(Tenant, tenantUserId, branchId).Value);
      await platform.SaveChangesAsync();
    }

    public async Task GrantCompanyAsync(long tenantUserId, Guid companyId)
    {
      await using var platform = PlatformContext();
      platform.UserCompanyAccess.Add(
        SSAS.Platform.Domain.Companies.UserCompanyAccess.Create(Tenant, tenantUserId, companyId).Value);
      await platform.SaveChangesAsync();
    }

    internal PlatformDbContext PlatformContext(Guid? tenantId = null)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(ConnectionFor(platformCatalog))
        .Options;
      return new PlatformDbContext(options, new TestUser(), new TestTenant(tenantId), new TestClock());
    }

    // The READ path: no write authorizers, because the resolvers only read.
    internal TenantDbContextFactory ReadContextFactory()
    {
      var platform = PlatformContext(Tenant);
      return new TenantDbContextFactory(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        new TenantDatabaseConnectionFactory(Options.Create(storage)),
        new TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness.Default),
        new TestUser(), new TestTenant(Tenant), new TestClock(),
        new TenantCutoverWriteFence(
          new TenantCutoverOperationStore(platform, new TestClock(), TimeSpan.FromSeconds(5)),
          Options.Create(freeze)));
    }

    // The WRITE path: every authorizer plus HR's model contribution.
    internal TenantDbContextFactory WriteContextFactory(
      IBranchWriteAuthorizer branchAuthorizer,
      ICompanyWriteAuthorizer companyAuthorizer,
      IBranchTransferAuthorizer transferAuthorizer)
    {
      var platform = PlatformContext(Tenant);
      return new TenantDbContextFactory(
        new TenantDatabaseResolver(new TenantDatabaseRegistryReadRepository(platform)),
        new TenantDatabaseConnectionFactory(Options.Create(storage)),
        new TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness.Default),
        new TestUser(), new TestTenant(Tenant), new TestClock(),
        new TenantCutoverWriteFence(
          new TenantCutoverOperationStore(platform, new TestClock(), TimeSpan.FromSeconds(5)),
          Options.Create(freeze)),
        branchAuthorizer,
        companyAuthorizer,
        transferAuthorizer,
        [new HrTenantModelContributor()]);
    }

    private async Task<long> SessionFor(long tenantUserId, Guid activeBranch)
    {
      await using var platform = PlatformContext(Tenant);

      var identityId = await platform.TenantUsers
        .IgnoreQueryFilters()
        .Where(user => user.Id == tenantUserId)
        .Select(user => user.IdentityId)
        .SingleAsync();

      var now = DateTimeOffset.UtcNow;
      var session = SSAS.Platform.Domain.Authentication.AuthenticationSession.Create(
        identityId, tenantUserId, Tenant, "web", Guid.NewGuid(), 1,
        now, now.AddDays(30), now.AddDays(90));
      platform.Set<SSAS.Platform.Domain.Authentication.AuthenticationSession>().Add(session);
      await platform.SaveChangesAsync();

      session.SelectBranch(activeBranch);
      await platform.SaveChangesAsync();

      return session.Id;
    }

    // The create command, with a department that actually exists in the company the graph will act in.
    public CreateEmployeeCommand NewEmployee(
      string number, string? nationalId = null, Guid? department = null, Guid? position = null) =>
      new(
        number,
        "Layla Haddad",
        new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
        nationalId,
        department ?? DepartmentA,
        position ?? PositionA);

    // Seeded through the REAL Department aggregate and the real context, so these rows are exactly what the
    // department application writes — including the company stamping the write boundary applies.
    // ---- DEPARTMENTS ARE SEEDED WITH RAW SQL, LIKE THE COMPANIES AND BRANCHES ABOVE.
    //
    // Not through the aggregate: Department is `ICompanyOwnedEntity`, so the tenant context refuses every
    // save unless an `ICompanyWriteAuthorizer` is present, and this fixture's seeding context deliberately
    // has none. That refusal is the production company write boundary doing its job — the fix is to seed
    // the way the other Platform rows are seeded, not to hand the fixture an authority it should not hold.
    //
    // The departments under test are still exercised through the real handlers; only the ARRANGE step is
    // direct.
    public async Task DeactivateDepartmentAsync(Guid departmentId)
    {
      await ExecuteAsync($"""
        UPDATE [tenant].[Departments]
        SET [Status] = N'Inactive', [StatusChangedUtc] = SYSDATETIMEOFFSET(), [StatusChangedBy] = N'{Actor}',
            [ModifiedUtc] = SYSDATETIMEOFFSET(), [ModifiedBy] = N'{Actor}'
        WHERE [DepartmentId] = '{departmentId}'
        """);
    }

    public async Task<Guid> SeedDepartmentAsync(Guid companyId, string code, bool active)
    {
      var departmentId = Guid.NewGuid();
      var status = active ? "Active" : "Inactive";

      await ExecuteAsync($"""
        INSERT INTO [tenant].[Departments]
          ([DepartmentId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name], [NormalizedName],
           [ParentDepartmentId], [Status], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc],
           [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{departmentId}', '{Tenant}', '{companyId}', N'{code}', N'{code.ToUpperInvariant()}',
           N'Department {code}', N'DEPARTMENT {code.ToUpperInvariant()}', NULL, N'{status}', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      return departmentId;
    }

    // Raw SQL for the same reason the department seeder uses it: these are PRECONDITIONS of the employee
    // tests rather than behaviour under test, and driving the position application surface here would make
    // every employee test depend on the position handlers passing too.
    public async Task<Guid> SeedPositionAsync(Guid companyId, string code, bool active)
    {
      var positionId = Guid.NewGuid();
      var status = active ? "Active" : "Inactive";

      await ExecuteAsync($"""
        INSERT INTO [tenant].[Positions]
          ([PositionId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Title], [NormalizedTitle],
           [JobGradeId], [Status], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc],
           [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{positionId}', '{Tenant}', '{companyId}', N'{code}', N'{code.ToUpperInvariant()}',
           N'Position {code}', N'POSITION {code.ToUpperInvariant()}', NULL, N'{status}',
           SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      return positionId;
    }

    private async Task ExecuteAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> SeedBranchAsync(string code, bool main)
    {
      await using var context = TenantOnlyContext();
      var branch = Branch.Create(
        Tenant, BranchCode.Create(code).Value, BranchName.Create($"Branch {code}").Value, main, Actor).Value;
      context.Branches.Add(branch);
      await context.SaveChangesAsync();
      return branch.Id;
    }

    private async Task<Guid> SeedCompanyAsync(string code)
    {
      await using var context = TenantOnlyContext();
      var company = PlatformCompany.Create(
        Tenant, CompanyCode.Create(code).Value, CompanyName.Create($"Company {code}").Value,
        BaseCurrencyCode.Create("SAR").Value, Actor, Guid.NewGuid(), Seeded).Value;
      Assert.True(company.Activate(
        CompanyStatusChangeReason.Administrative, Actor, Guid.NewGuid(), Seeded).IsSuccess);
      context.Companies.Add(company);
      await context.SaveChangesAsync();
      return company.Id;
    }

    private TenantDbContext TenantOnlyContext()
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(tenantCatalog), sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;
      return new TenantDbContext(
        options, new TestUser(), new TestTenant(Tenant), new TestClock(),
        modelContributors: [new HrTenantModelContributor()]);
    }

    private async Task<long> SeedUserAsync(string email, bool administrator)
    {
      await using var platform = PlatformContext(Tenant);

      var identity = Identity.Create(AuthenticationSubject.Create($"sub-{Guid.NewGuid():N}").Value);
      platform.Identities.Add(identity);
      await platform.SaveChangesAsync();

      var user = TenantUser.CreateActive(
        identity.Id, Tenant, EmailAddress.Create(email).Value,
        UserDisplayName.Create("Test User").Value, Guid.NewGuid(), Seeded);
      platform.TenantUsers.Add(user);
      await platform.SaveChangesAsync();

      if (!administrator)
      {
        return user.Id;
      }

      var role = Role.CreateCustom(
        Tenant, RoleName.Create($"HR Admins {Guid.NewGuid():N}"[..20]).Value, null, Guid.NewGuid(), Seeded);
      platform.Roles.Add(role);
      await platform.SaveChangesAsync();

      var definition = new PermissionDefinition(
        PermissionName.Create(PlatformPermissionNames.AdministerTenant).Value,
        PermissionScope.Tenant, "Administer the tenant");
      Assert.True(role.AssignPermission(definition, Actor, Guid.NewGuid(), Seeded).IsSuccess);
      Assert.True(user.AssignRole(role, Actor, Guid.NewGuid(), Seeded).IsSuccess);
      await platform.SaveChangesAsync();

      return user.Id;
    }

    private static async Task<long> RegisterAsync(PlatformDbContext platform, string databaseName)
    {
      var database = TenantDatabase.Register(
        TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Dedicated, ServerKey,
        databaseName, TenantDatabaseProvisioningStatus.Ready, Actor, Seeded).Value;

      var observedUtc = DateTimeOffset.UtcNow;
      database.RecordConnectivity(TenantDatabaseConnectivityStatus.Healthy, Actor, observedUtc);
      database.RecordSchemaHealth(
        TenantDatabaseSchemaCompatibilityStatus.UpToDate, null, null, Actor, observedUtc);
      platform.TenantDatabases.Add(database);
      await platform.SaveChangesAsync();
      return database.Id;
    }

    private static async Task<Guid> SeedTenantAsync(PlatformDbContext platform, string code, long databaseId)
    {
      var tenant = PlatformTenant.Create(
        TenantCode.Create(code).Value, TenantName.Create($"Employee {code}").Value,
        Actor, Guid.NewGuid(), Seeded).Value;
      platform.Tenants.Add(tenant);
      await platform.SaveChangesAsync();

      // ACTIVE, because the real access-token claims provider only issues claims for an active tenant and
      // the permission proofs below travel that path (FP-006P).
      Assert.True(tenant.Activate(Actor, Guid.NewGuid(), Seeded.AddMinutes(1)).IsSuccess);
      await platform.SaveChangesAsync();

      platform.TenantDatabaseAssignments.Add(
        TenantDatabaseAssignment.CreateInitial(tenant.Id, databaseId, "c3", Actor, Seeded).Value);
      await platform.SaveChangesAsync();
      return tenant.Id;
    }

    private async Task<Guid?> ScalarGuidAsync(string table, string column, string keyColumn, Guid key)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = $"SELECT [{column}] FROM [tenant].[{table}] WHERE [{keyColumn}] = @key";
      command.Parameters.AddWithValue("@key", key);
      var result = await command.ExecuteScalarAsync();
      return result is Guid value ? value : null;
    }

    // ---- FP-009 PHASE 1 READ HELPERS.
    //
    // Raw SQL against the tables for the reason the FP-007 helpers above give: these assertions are about
    // what was PERSISTED, and routing them through a scoped read would let a scoping bug hide a persistence
    // bug — or, here, hide the fact that nothing was persisted at all.
    public Task<int> EmployeeCountAsync(string numberPrefix) => ScalarIntAsync(
      "SELECT COUNT(*) FROM [tenant].[Employees] " +
      $"WHERE [NormalizedEmployeeNumber] LIKE N'{numberPrefix.ToUpperInvariant()}%'");

    // The per-employee rows the ordinary create path writes, counted through the employees that carry the
    // prefix — so an import that created employees WITHOUT their history would be visible here rather than
    // hidden behind a table-wide count that the fixture's own seeding contributes to.
    public Task<int> CountAsync(string table, string numberPrefix) => ScalarIntAsync(
      $"SELECT COUNT(*) FROM [tenant].[{table}] a " +
      "JOIN [tenant].[Employees] e ON e.[EmployeeId] = a.[EmployeeId] " +
      $"WHERE e.[NormalizedEmployeeNumber] LIKE N'{numberPrefix.ToUpperInvariant()}%'");

    public Task<int> TableCountAsync(string table) => CountAsync(table);

    public Task<int> DepartmentCountAsync(Guid companyId, string normalizedCode) => ScalarIntAsync(
      "SELECT COUNT(*) FROM [tenant].[Departments] " +
      $"WHERE [CompanyId] = '{companyId:D}' AND [NormalizedCode] = N'{normalizedCode}'");

    public async Task<string?> FullNameAsync(string employeeNumber)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT [FullName] FROM [tenant].[Employees] " +
        $"WHERE [NormalizedEmployeeNumber] = N'{employeeNumber.ToUpperInvariant()}'";

      return (await command.ExecuteScalarAsync())?.ToString();
    }

    public Task<int> ImportRunCountAsync(string normalizedKey) => ScalarIntAsync(
      "SELECT COUNT(*) FROM [tenant].[EmployeeImportRuns] " +
      $"WHERE [NormalizedImportKey] = N'{normalizedKey}'");

    public async Task<ImportRunRow?> ImportRunAsync(string normalizedKey)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText =
        "SELECT [ImportKey], [NormalizedImportKey], [FileName], [RowCount], [AcceptedCount], " +
        "[RejectedCount], [Outcome], [CreatedUtc] FROM [tenant].[EmployeeImportRuns] " +
        $"WHERE [NormalizedImportKey] = N'{normalizedKey}'";

      await using var reader = await command.ExecuteReaderAsync();

      return await reader.ReadAsync()
        ? new ImportRunRow(
          reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3),
          reader.GetInt32(4), reader.GetInt32(5), reader.GetString(6), reader.GetDateTimeOffset(7))
        : null;
    }

    private async Task<int> ScalarIntAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;

      return Convert.ToInt32(
        await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<int> CountAsync(string table)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = $"SELECT COUNT(*) FROM [tenant].[{table}]";
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Configured() =>
      IntegrationSqlEnvironment.BaseConnectionString;

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

    private static async Task ExecuteAsync(string catalog, string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      foreach (var catalog in new[] { tenantCatalog, platformCatalog })
      {
        try
        {
          await ExecuteAsync("master",
            $"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{catalog}]");
        }
        catch (SqlException error)
        {
          TestCatalogJanitor.RecordLeak(catalog, error);
        }
      }
    }

    // ---- THE PERMISSION SET IS A PARAMETER FROM FP-007 PHASE 3.
    //
    // It was empty, and could stay empty while every write path enforced its permission at the HTTP
    // endpoint. `ChangeEmployeeDepartmentCommandHandler` enforces `HR.Employees.Update` in the APPLICATION
    // boundary as well, so a caller here has to actually hold it — and a test proving the refusal needs a
    // caller who does not. Defaulting to granted keeps every existing test asserting what it always did.
    internal sealed class TestUser(bool canUpdate = true) : ICurrentUser
    {
      public string? UserId => Actor;

      public string? UserName => Actor;

      public string? Email => null;


      public string? SessionId => null;

      public string? TokenId => null;

      public IReadOnlyCollection<string> Roles => [];

      public IReadOnlyCollection<string> Permissions =>
        canUpdate ? [SSAS.HR.Application.Permissions.HrPermissionNames.UpdateEmployees] : [];
    }

    internal sealed class TestTenant(Guid? tenantId) : ICurrentTenant
    {
      public Guid? TenantId => tenantId;
    }

    internal sealed class TestClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
  }

  public sealed record HistoryRow(
    Guid? SourceBranchId, Guid DestinationBranchId, DateTimeOffset EffectiveFromUtc, string ReasonCode);

  // One request's worth of the production graph: one scoped context shared by the repository and the unit of
  // work, exactly as production composes it. A second context here would silently discard tracked changes.
  private sealed class EmployeeGraph : IDisposable
  {
    private readonly EmployeeFixture fixture;
    private readonly CountingBranchAuthorizer branchAuthorizer;
    private readonly CountingCompanyAuthorizer companyAuthorizer;
    private readonly IBranchTransferScope transferScope = new BranchTransferScope();
    private readonly TenantDbContextProvider provider;
    private readonly TenantDbContextAccessorShim accessor;
    private readonly TenantUnitOfWork unitOfWork;
    private readonly long tenantUserId;

    public EmployeeGraph(EmployeeFixture fixture, long tenantUserId, long sessionId, Guid company)
    {
      this.fixture = fixture;
      this.tenantUserId = tenantUserId;
      SessionId = sessionId;

      var platform = fixture.PlatformContext(fixture.Tenant);
      var session = new TestSession(fixture.Tenant, tenantUserId, sessionId);
      var branchAccess = new TenantBranchAccessResolver(
        platform, fixture.ReadContextFactory(), new TenantAdministratorAuthority(platform));

      branchAuthorizer = new CountingBranchAuthorizer(new BranchWriteAuthorizer(
        platform, branchAccess, new EmployeeFixture.TestClock(), session));

      var companyPlatform = fixture.PlatformContext(fixture.Tenant);
      companyAuthorizer = new CountingCompanyAuthorizer(new CompanyWriteAuthorizer(
        new CompanyContextResolver(
          new EmployeeFixture.TestTenant(fixture.Tenant),
          new TenantCompanyAccessResolver(
            companyPlatform, fixture.ReadContextFactory(),
            new TenantAdministratorAuthority(companyPlatform)),
          new TestSelection(company),
          session),
        new EmployeeFixture.TestTenant(fixture.Tenant)));

      var transferAuthorizer = new BranchTransferAuthorizer(
        transferScope,
        new TenantBranchAccessResolver(
          fixture.PlatformContext(fixture.Tenant), fixture.ReadContextFactory(),
          new TenantAdministratorAuthority(fixture.PlatformContext(fixture.Tenant))),
        new TenantAdministratorAuthority(fixture.PlatformContext(fixture.Tenant)),
        fixture.ReadContextFactory(),
        session);

      BranchAccess = branchAccess;
      Company = company;

      provider = new TenantDbContextProvider(
        fixture.WriteContextFactory(branchAuthorizer, companyAuthorizer, transferAuthorizer),
        new EmployeeFixture.TestTenant(fixture.Tenant));

      accessor = new TenantDbContextAccessorShim(provider);
      unitOfWork = TestUnitOfWork.Tenant(provider, new NoOpDispatcher());
    }

    public long SessionId { get; }

    public Guid Company { get; }

    public ITenantBranchAccessResolver BranchAccess { get; }

    public int BranchAuthorizerCalls => branchAuthorizer.Calls;

    public int CompanyAuthorizerCalls => companyAuthorizer.Calls;

    public async Task<TenantDbContext> ContextAsync() => await provider.GetRequiredAsync();


    // ---- THE READ SIDE, COMPOSED AS THE HOST COMPOSES IT (FP-006C4).
    //
    // Real company resolver, real branch resolver, real current-branch resolution, real read service, over
    // the SAME scoped context the write side uses. Nothing about the scope is stubbed, because the scope is
    // the thing under test.
    public EmployeeScopeResolver Scope(IReadOnlyCollection<string>? permissions = null) => new(
      fixture.CompanyResolver(),
      BranchAccess,
      new CurrentBranchResolverShim(branchAuthorizer, fixture.Tenant),
      new TestCurrentCompany(Company),
      new EmployeeFixture.TestTenant(fixture.Tenant),
      new TestCurrentTenantUser(tenantUserId),
      new HrUser(permissions ?? [HrPermissionNames.ViewEmployees]));

    public EmployeeReadService Reads() => new(accessor);

    // The real search handler over the real scope resolver, so a department filter is proven against the
    // production composition rather than against a read service called directly.
    public GetEmployeeQueryHandler Get() => new(Scope(), Reads());

    public SearchEmployeesQueryHandler Search() => new(Scope(), Reads());

    public CreateEmployeeCommandHandler Create() => new(
      new EmployeeRepository(accessor), unitOfWork,
      new CurrentBranchResolverShim(branchAuthorizer, fixture.Tenant),
      new EmployeeFixture.TestTenant(fixture.Tenant),
      new TestCurrentCompany(Company),
      new EmployeeFixture.TestUser(),
      new EmployeeFixture.TestClock());

    // ---- FP-009 PHASE 1. THE IMPORT, COMPOSED OVER THE REAL CREATE HANDLER (BRULE-DOC-0603).
    //
    // `Create()` is passed in rather than the repository, so the import genuinely goes through the same path
    // a single `POST` does. A graph that handed it a repository would let the import drift into a second
    // create path and every test here would still pass.
    public ImportEmployeesCommandHandler Import(EmployeeImportLimits? limits = null) => new(
      new EmployeeRepository(accessor),
      new EmployeeImportRunRepository(accessor),
      Create(),
      unitOfWork,
      new EmployeeFixture.TestTenant(fixture.Tenant),
      new TestCurrentCompany(Company),
      new EmployeeFixture.TestUser(),
      new EmployeeFixture.TestClock(),
      limits);

    // ---- FP-009 PHASE 1. THE EXPORT, OVER THE REAL SCOPE RESOLVER AND THE REAL READ SERVICE.
    //
    // `canExport: false` builds a caller holding `HR.Employees.View` and NOT `HR.Employees.Export`, which is
    // the only way to prove the application-boundary permission check actually runs — the same seam
    // `ChangeDepartment(canUpdate: false)` opened for its own check.
    //
    // The scope resolver is built with `HR.Employees.View` alone, deliberately: the two permissions are
    // resolved by two different mechanisms and a single list would hide which one refused.
    public ExportEmployeesQueryHandler Export(
      bool canExport = true, EmployeeImportLimits? limits = null) => new(
      Scope([HrPermissionNames.ViewEmployees]),
      Reads(),
      new EmployeeExportRunRepository(accessor),
      unitOfWork,
      new EmployeeFixture.TestTenant(fixture.Tenant),
      new TestCurrentCompany(Company),
      new HrUser(canExport
        ? [HrPermissionNames.ViewEmployees, HrPermissionNames.ExportEmployees]
        : [HrPermissionNames.ViewEmployees]),
      new EmployeeFixture.TestClock(),
      limits);

    // ---- FP-009 PHASE 2. THE RUN-HISTORY READS, over the real scope resolver.
    //
    // `canView: false` builds a caller WITHOUT `HR.Employees.View`, which is the only way to prove the
    // permission is genuinely what gates these — the scope resolver checks it as its first dimension, and a
    // graph that always supplied it would leave that untested.
    public SearchImportRunsQueryHandler SearchImportRuns(bool canView = true) => new(
      Scope(canView ? [HrPermissionNames.ViewEmployees] : []),
      new EmployeeRunHistoryReadService(accessor));

    public SearchExportRunsQueryHandler SearchExportRuns(bool canView = true) => new(
      Scope(canView ? [HrPermissionNames.ViewEmployees] : []),
      new EmployeeRunHistoryReadService(accessor));

    public UpdateEmployeeProfileCommandHandler Update() => new(
      new EmployeeRepository(accessor), unitOfWork,
      new EmployeeFixture.TestUser(), new EmployeeFixture.TestClock());

    // ---- T-091's DEACTIVATOR IS A NO-OP HERE, AND THAT IS STATED RATHER THAN CONVENIENT.
    //
    // These tests are about the TENANT database's boundaries. The account-closure half writes the PLATFORM
    // database, which this fixture does not stand up at all. A no-op keeps the boundary tests testing the
    // boundary — **and it means nothing here proves the account closes.** That is asserted where the two
    // sides both exist: `EmployeeTerminationAccountClosureTests` in the API suite.
    public TerminateEmployeeCommandHandler Terminate() => new(
      new EmployeeRepository(accessor), unitOfWork, new NoOpTenantUserDeactivator(),
      new EmployeeFixture.TestUser(), new EmployeeFixture.TestClock());

    private sealed class NoOpTenantUserDeactivator : SSAS.BuildingBlocks.Tenancy.ITenantUserDeactivator
    {
      public Task<Result> DeactivateForEmployeeAsync(
        Guid employeeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
    }

    // FP-009. The export's default status set is Active AND Inactive, so a test about what a DEFAULT export
    // carries needs a real inactive employee — produced by the real transition, because a hand-written status
    // could be one no transition allows.
    public DeactivateEmployeeCommandHandler Deactivate() => new(
      new EmployeeRepository(accessor), unitOfWork,
      new EmployeeFixture.TestUser(), new EmployeeFixture.TestClock());

    // The scoped context is owned by this graph, exactly as a request scope owns it in production.
    public void Dispose() => provider.DisposeAsync().AsTask().GetAwaiter().GetResult();

    // FP-007 Phase 3. `canUpdate: false` builds a caller without HR.Employees.Update, which is the only way
    // to prove the application-boundary permission check actually runs.
    public ChangeEmployeeDepartmentCommandHandler ChangeDepartment(bool canUpdate = true) => new(
      new EmployeeRepository(accessor), unitOfWork,
      new EmployeeFixture.TestTenant(fixture.Tenant),
      new TestCurrentCompany(Company),
      new EmployeeFixture.TestUser(canUpdate),
      new EmployeeFixture.TestClock());

    // FP-008 Phase 3. Same shape and same `canUpdate` seam as the department change, because
    // `DEC-POS-0019` gave it the same permission on the same prefix.
    public ChangeEmployeePositionCommandHandler ChangePosition(bool canUpdate = true) => new(
      new EmployeeRepository(accessor), unitOfWork,
      new EmployeeFixture.TestTenant(fixture.Tenant),
      new TestCurrentCompany(Company),
      new EmployeeFixture.TestUser(canUpdate),
      new EmployeeFixture.TestClock());

    public TransferEmployeeCommandHandler Transfer() => new(
      new EmployeeRepository(accessor), BranchAccess, transferScope, unitOfWork,
      new EmployeeFixture.TestTenant(fixture.Tenant),
      new TestCurrentTenantUser(tenantUserId),
      new EmployeeFixture.TestUser(),
      new EmployeeFixture.TestClock());
  }

  // Counts invocations so the WIRING can be proven, not just the rules: a boundary that never called its
  // authorizer would satisfy every stamping assertion and still be broken. It DELEGATES to the real one, so
  // the behaviour under test is production behaviour.
  private sealed class CountingBranchAuthorizer(IBranchWriteAuthorizer inner) : IBranchWriteAuthorizer
  {
    public int Calls { get; private set; }

    public Task<Result<Guid>> AuthorizeCurrentBranchAsync(
      Guid tenantId, CancellationToken cancellationToken = default)
    {
      Calls++;
      return inner.AuthorizeCurrentBranchAsync(tenantId, cancellationToken);
    }
  }

  private sealed class CountingCompanyAuthorizer(ICompanyWriteAuthorizer inner) : ICompanyWriteAuthorizer
  {
    public int Calls { get; private set; }

    public Task<Result<Guid>> AuthorizeCurrentCompanyAsync(
      Guid tenantId, CancellationToken cancellationToken = default)
    {
      Calls++;
      return inner.AuthorizeCurrentCompanyAsync(tenantId, cancellationToken);
    }
  }

  private sealed class TenantDbContextAccessorShim(TenantDbContextProvider provider)
    : SSAS.BuildingBlocks.Infrastructure.Persistence.ITenantDbContextAccessor
  {
    public async Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      await provider.GetRequiredAsync(cancellationToken);
  }

  private sealed class CurrentBranchResolverShim(IBranchWriteAuthorizer authorizer, Guid tenantId)
    : ICurrentBranchResolver
  {
    public Task<Result<Guid>> ResolveCurrentBranchAsync(CancellationToken cancellationToken = default) =>
      authorizer.AuthorizeCurrentBranchAsync(tenantId, cancellationToken);
  }

  // The acting user's FUNCTIONAL permissions, which are a separate question from either scope dimension. A
  // test can hand this an empty set to prove that scope alone reads nothing.
  private sealed class HrUser(IReadOnlyCollection<string> permissions) : ICurrentUser
  {
    public string? UserId => "employee-c4-tests";

    public string? UserName => "employee-c4-tests";

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => permissions;
  }

  // Hands the read service one already-open context, for the proof that inspects the SQL it sends.
  private sealed class StaticAccessor(DbContext context)
    : SSAS.BuildingBlocks.Infrastructure.Persistence.ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(context);
  }

  private sealed class TestCurrentCompany(Guid companyId) : ICurrentCompany
  {
    public Guid? CompanyId => companyId;
  }

  private sealed class TestCurrentTenantUser(long tenantUserId) : ICurrentTenantUser
  {
    public long? TenantUserId => tenantUserId;
  }

  private sealed class TestSelection(Guid? companyId) : ICompanySelection
  {
    public Result<Guid?> Requested => Result.Success(companyId);
  }

  private sealed class TestSession(Guid tenantId, long tenantUserId, long sessionId)
    : ICurrentAuthenticationSession
  {
    public CurrentAuthenticationSession? Value => new(
      1, tenantId, tenantUserId, sessionId, AuthenticationClientId.Create("web").Value, 1);
  }

  private sealed class NoOpDispatcher : IDomainEventDispatcher
  {
    public Task DispatchAsync(
      IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }
}
