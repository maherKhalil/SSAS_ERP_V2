using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.HR.Application.Employees;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.TenantStorage;
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

    var created = await graph.Create().HandleAsync(NewEmployee("EMP-V"));

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

    var created = await graph.Create().HandleAsync(NewEmployee("EMP-CA"));

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

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-R1"))).IsSuccess);

    await fixture.RevokeBranchAssignmentAsync(fixture.NormalUserId, fixture.BranchA);

    var after = await graph.Create().HandleAsync(NewEmployee("EMP-R2"));

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
    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-R3"))).IsSuccess);

    await fixture.RevokeAdministratorAuthorityAsync();

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-R4"))).IsFailure);
    Assert.Equal(1, await fixture.EmployeeCountAsync());
  }

  [Fact]
  public async Task Revoking_company_access_mid_session_refuses_the_next_employee_write()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-R5"))).IsSuccess);

    await fixture.RevokeCompanyAssignmentAsync(fixture.NormalUserId, fixture.CompanyA);

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-R6"))).IsFailure);
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

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-R7"))).IsSuccess);

    await fixture.DeactivateCompanyAsync(fixture.CompanyA);

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-R8"))).IsFailure);
    Assert.Equal(1, await fixture.EmployeeCountAsync());
  }

  [Fact]
  public async Task Revoking_the_session_refuses_the_next_employee_write()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA, asUserId: fixture.NormalUserId);

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-R9"))).IsSuccess);

    await fixture.RevokeSessionAsync(graph.SessionId);

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-R10"))).IsFailure);
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

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-100"))).IsSuccess);

    var duplicate = await graph.Create().HandleAsync(NewEmployee("EMP-100"));

    Assert.True(duplicate.IsFailure);
    Assert.Equal(EmployeeErrors.NumberConflict.Code, duplicate.Error.Code);
  }

  // ---- THE ONE THAT MAKES BR-HR-0001 CONCRETE: uniqueness spans branches of the same company, because
  // BranchId deliberately does not participate in the index.
  [Fact]
  public async Task An_employee_number_is_unique_across_branches_of_the_same_company()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    Assert.True((await fixture.Graph(fixture.BranchA).Create().HandleAsync(NewEmployee("EMP-200"))).IsSuccess);

    var otherBranch = await fixture.Graph(fixture.BranchB).Create().HandleAsync(NewEmployee("EMP-200"));

    Assert.True(otherBranch.IsFailure);
    Assert.Equal(EmployeeErrors.NumberConflict.Code, otherBranch.Error.Code);
  }

  [Fact]
  public async Task The_same_employee_number_is_free_in_a_different_company()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    Assert.True((await fixture.Graph(fixture.BranchA).Create()
      .HandleAsync(NewEmployee("EMP-300"))).IsSuccess);

    var otherCompany = await fixture.Graph(fixture.BranchA, company: fixture.CompanyB).Create()
      .HandleAsync(NewEmployee("EMP-300"));

    Assert.True(otherCompany.IsSuccess, otherCompany.IsFailure ? otherCompany.Error.Code : null);
  }

  // Two spellings that normalize alike are the same number, and the binary-collated index is what refuses
  // the second under concurrency.
  [Fact]
  public async Task Employee_numbers_that_normalize_alike_collide()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    Assert.True((await graph.Create().HandleAsync(NewEmployee(" emp-400 "))).IsSuccess);

    var equivalent = await graph.Create().HandleAsync(NewEmployee("EMP-400"));

    Assert.True(equivalent.IsFailure);
    Assert.Equal(EmployeeErrors.NumberConflict.Code, equivalent.Error.Code);
  }

  // ---- NATIONAL ID: unique where present, and many absent values remain possible.
  [Fact]
  public async Task A_national_id_is_unique_within_a_company_but_may_be_absent_many_times()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var graph = fixture.Graph(fixture.BranchA);

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-500", nationalId: "NID-1"))).IsSuccess);

    var duplicate = await graph.Create().HandleAsync(NewEmployee("EMP-501", nationalId: "nid-1"));
    Assert.True(duplicate.IsFailure);
    Assert.Equal(EmployeeErrors.NationalIdConflict.Code, duplicate.Error.Code);

    // Two employees with no national identifier at all are fine: the unique index is filtered.
    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-502"))).IsSuccess);
    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-503"))).IsSuccess);
  }

  // ================================================================================================
  // INITIAL ASSIGNMENT, APPEND-ONLY HISTORY AND PHYSICAL DELETE
  // ================================================================================================

  [Fact]
  public async Task Creating_an_employee_writes_its_initial_assignment_atomically()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();

    var created = await fixture.Graph(fixture.BranchA).Create().HandleAsync(NewEmployee("EMP-600"));
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

    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-700"))).IsSuccess);
    Assert.True((await graph.Create().HandleAsync(NewEmployee("EMP-700"))).IsFailure);

    Assert.Equal(1, await fixture.EmployeeCountAsync());
    Assert.Equal(1, await fixture.HistoryRowCountAsync());
  }

  [Fact]
  public async Task A_history_row_cannot_be_updated_or_deleted()
  {
    await using var fixture = await EmployeeFixture.CreateAsync();
    var created = await fixture.Graph(fixture.BranchA).Create().HandleAsync(NewEmployee("EMP-800"));
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
    var created = await graph.Create().HandleAsync(NewEmployee("EMP-1000"));
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
    var reuse = await graph.Create().HandleAsync(NewEmployee("EMP-1000"));
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
    var created = await graph.Create().HandleAsync(NewEmployee("EMP-T1"));
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
    var created = await graph.Create().HandleAsync(NewEmployee("EMP-T2"));
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
    var created = await graph.Create().HandleAsync(NewEmployee("EMP-T3"));
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
    var created = await graph.Create().HandleAsync(NewEmployee("EMP-T4"));
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
    var created = await graph.Create().HandleAsync(NewEmployee("EMP-T5"));
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
    var created = await graph.Create().HandleAsync(NewEmployee("EMP-T6"));
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
    var created = await graph.Create().HandleAsync(NewEmployee("EMP-T7"));
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
      .Create().HandleAsync(NewEmployee("EMP-T8"));
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
      .Create().HandleAsync(NewEmployee("EMP-T9"));
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
    var created = await fixture.Graph(fixture.BranchA).Create().HandleAsync(NewEmployee("EMP-T10"));
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
    var created = await graph.Create().HandleAsync(NewEmployee("EMP-T11"));
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

  private static CreateEmployeeCommand NewEmployee(string number, string? nationalId = null) =>
    new(number, "Layla Haddad", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), nationalId);

  // ================================================================================================
  // FIXTURE
  // ================================================================================================

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

    public static Employee NewAggregate(string number) =>
      Employee.Create(
        EmployeeNumber.Create(number).Value,
        EmployeeFullName.Create("Spoof Attempt").Value,
        null,
        new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
        Actor,
        Guid.NewGuid(),
        DateTimeOffset.UtcNow).Value;

    public async Task<Guid> SeedEmployeeAsync(string number, Guid branchId)
    {
      var created = await Graph(branchId).Create().HandleAsync(
        new CreateEmployeeCommand(number, "Seeded Person",
          new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), null));
      Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);
      return created.Value;
    }

    public async Task<Guid?> EmployeeBranchAsync(Guid employeeId) =>
      await ScalarGuidAsync("Employees", "BranchId", "EmployeeId", employeeId);

    public async Task<Guid?> EmployeeCompanyAsync(Guid employeeId) =>
      await ScalarGuidAsync("Employees", "CompanyId", "EmployeeId", employeeId);

    public async Task<int> EmployeeCountAsync() => await CountAsync("Employees");

    public async Task<int> HistoryRowCountAsync() => await CountAsync("EmployeeBranchAssignments");

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

    private async Task<int> CountAsync(string table)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = $"SELECT COUNT(*) FROM [tenant].[{table}]";
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=true;TrustServerCertificate=true";

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

    internal sealed class TestUser : ICurrentUser
    {
      public string? UserId => Actor;

      public string? UserName => Actor;

      public string? Email => null;

      public Guid? CompanyId => null;

      public string? SessionId => null;

      public string? TokenId => null;

      public IReadOnlyCollection<string> Roles => [];

      public IReadOnlyCollection<string> Permissions => [];
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
      unitOfWork = new TenantUnitOfWork(provider, new NoOpDispatcher());
    }

    public long SessionId { get; }

    public Guid Company { get; }

    public ITenantBranchAccessResolver BranchAccess { get; }

    public int BranchAuthorizerCalls => branchAuthorizer.Calls;

    public int CompanyAuthorizerCalls => companyAuthorizer.Calls;

    public async Task<TenantDbContext> ContextAsync() => await provider.GetRequiredAsync();

    public CreateEmployeeCommandHandler Create() => new(
      new EmployeeRepository(accessor), unitOfWork,
      new CurrentBranchResolverShim(branchAuthorizer, fixture.Tenant),
      new EmployeeFixture.TestTenant(fixture.Tenant),
      new TestCurrentCompany(Company),
      new EmployeeFixture.TestUser(),
      new EmployeeFixture.TestClock());

    public UpdateEmployeeProfileCommandHandler Update() => new(
      new EmployeeRepository(accessor), unitOfWork,
      new EmployeeFixture.TestUser(), new EmployeeFixture.TestClock());

    public TerminateEmployeeCommandHandler Terminate() => new(
      new EmployeeRepository(accessor), unitOfWork,
      new EmployeeFixture.TestUser(), new EmployeeFixture.TestClock());

    // The scoped context is owned by this graph, exactly as a request scope owns it in production.
    public void Dispose() => provider.DisposeAsync().AsTask().GetAwaiter().GetResult();

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
