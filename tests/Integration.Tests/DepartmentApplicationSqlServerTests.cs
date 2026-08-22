using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Departments;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// THE DEPARTMENT APPLICATION AGAINST REAL SQL SERVER (FP-007 Phase 2, ADR-026).
//
// ================================================================================================
// WHAT ONLY A REAL SERVER CAN PROVE, AND WHAT IS PROVEN ELSEWHERE.
// ================================================================================================
//
// Here: the acyclicity invariant under CONCURRENCY, which depends on `sp_getapplock` actually serialising
// two transactions on two connections; the ancestry walk over persisted rows; the unique index; and the
// optimistic concurrency token. None of those exist in an in-memory provider, and a test that asserted them
// there would be asserting the provider.
//
// Elsewhere: WHO may do these things. The authorization decisions — permission before scope, tenant
// administration granting no HR operation, an empty company set refusing rather than widening — are proven
// against the real `DepartmentScopeResolver` in `HR.Tests`, with the Platform authorities stubbed so the
// tests can state exactly what a user may reach. Repeating them here would prove the same thing twice and
// more slowly.
//
// So the scope resolver is PERMISSIVE in this file, deliberately and visibly. It is not the thing under
// test; the hierarchy is.
[Trait("Category", "SqlServer")]
public sealed class DepartmentApplicationSqlServerTests(Xunit.Abstractions.ITestOutputHelper output)
{
  // ================================================================================================
  // THE CONCURRENT CYCLE — THE PROOF THIS PHASE EXISTS FOR
  // ================================================================================================
  //
  // A and B are both roots. Two sessions, on two real connections, in two real transactions:
  //
  //   Tx1 moves A under B.
  //   Tx2 moves B under A.
  //
  // Each is individually legal — each walks up from its proposed parent, reaches a root, and finds nothing
  // wrong. Committed together they produce a cycle that neither transaction could have detected, and that
  // row-level optimistic concurrency cannot see because the two rows are different rows.
  //
  // WITHOUT THE COMPANY HIERARCHY LOCK THIS TEST FAILS. That is the point of it.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Two_concurrent_moves_cannot_jointly_create_a_cycle()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();

    var a = await fixture.CreateDepartmentAsync("A", "Alpha");
    var b = await fixture.CreateDepartmentAsync("B", "Bravo");

    // Two INDEPENDENT graphs: separate contexts, separate connections, separate transactions. Sharing
    // either would serialise them in the client and prove nothing about the database.
    await using var first = fixture.Graph();
    await using var second = fixture.Graph();

    var firstVersion = await fixture.RowVersionAsync(a);
    var secondVersion = await fixture.RowVersionAsync(b);

    // Started together so they genuinely contend. One will sit inside sp_getapplock until the other
    // commits or rolls back.
    var moveAUnderB = first.ChangeParent().HandleAsync(
      new ChangeDepartmentParentCommand(a, b, firstVersion));
    var moveBUnderA = second.ChangeParent().HandleAsync(
      new ChangeDepartmentParentCommand(b, a, secondVersion));

    var results = await Task.WhenAll(moveAUnderB, moveBUnderA);

    // EXACTLY ONE SUCCEEDS. Both succeeding is the cycle; both failing would mean the lock is deadlocking
    // rather than serialising, which is also a defect.
    Assert.Equal(1, results.Count(result => result.IsSuccess));

    var loser = results.Single(result => result.IsFailure);

    // The loser's refusal is a NAMED business refusal, not a raw SQL error surfacing through the boundary.
    //
    // FOUR sanctioned routes, because the loser can be stopped at four different depths and the test cannot
    // control which one wins the race:
    //
    //   * HierarchyCycle           — the ancestry walk saw the move would close a loop;
    //   * HierarchyMutationBusy    — the per-company app lock was already held;
    //   * Department.ConcurrencyConflict — the HANDLER's own rowversion pre-check refused a stale token;
    //   * Persistence.ConcurrencyConflict — the pre-check passed and the DATABASE refused at SaveChanges,
    //     which is the unit of work translating DbUpdateConcurrencyException exactly as TenantUnitOfWork
    //     does in production.
    //
    // The last one is listed with production's code rather than the department-local one because that is
    // what a handler actually receives from TenantUnitOfWork; the fixture double mirrors it deliberately.
    string[] sanctioned =
    [
      DepartmentErrors.HierarchyCycle.Code,
      DepartmentErrors.HierarchyMutationBusy.Code,
      DepartmentErrors.ConcurrencyConflict.Code,
      SSAS.Platform.Domain.IdentityAccessErrors.ConcurrencyConflict.Code
    ];

    Assert.Contains(loser.Error.Code, sanctioned);

    // ---- AND THE PERSISTED HIERARCHY IS ACYCLIC, verified by walking it rather than by inference.
    await fixture.AssertAcyclicAsync();
  }

  // ---- THE SAME CONTENTION, BUT BOTH MOVES LEGAL.
  //
  // Two different departments moving under two different parents. The lock serialises them, and BOTH must
  // still succeed — a lock that refused legitimate concurrent work would be a correctness fix that broke
  // the feature.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Two_concurrent_legal_moves_both_succeed()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();

    var parentOne = await fixture.CreateDepartmentAsync("P1", "Parent One");
    var parentTwo = await fixture.CreateDepartmentAsync("P2", "Parent Two");
    var childOne = await fixture.CreateDepartmentAsync("C1", "Child One");
    var childTwo = await fixture.CreateDepartmentAsync("C2", "Child Two");

    await using var first = fixture.Graph();
    await using var second = fixture.Graph();

    var results = await Task.WhenAll(
      first.ChangeParent().HandleAsync(
        new ChangeDepartmentParentCommand(childOne, parentOne, await fixture.RowVersionAsync(childOne))),
      second.ChangeParent().HandleAsync(
        new ChangeDepartmentParentCommand(childTwo, parentTwo, await fixture.RowVersionAsync(childTwo))));

    Assert.All(results, result => Assert.True(result.IsSuccess, result.Error.Code));
    await fixture.AssertAcyclicAsync();
  }

  // ================================================================================================
  // THE DEEP CYCLE — DEPTH THREE, NOT DEPTH TWO
  // ================================================================================================
  //
  // A -> B -> C, then attempt to put A beneath C. A one-level check would pass this: C's immediate parent
  // is B, not A. Only walking the whole chain upward finds A.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_department_cannot_be_moved_beneath_its_own_grandchild()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("A", "Alpha");
    var b = await fixture.CreateDepartmentAsync("B", "Bravo", a);
    var c = await fixture.CreateDepartmentAsync("C", "Charlie", b);

    var moved = await graph.ChangeParent().HandleAsync(
      new ChangeDepartmentParentCommand(a, c, await fixture.RowVersionAsync(a)));

    Assert.True(moved.IsFailure);
    Assert.Equal(DepartmentErrors.HierarchyCycle, moved.Error);
  }

  // ---- AND DEEPER STILL. Five levels, moving the root beneath the deepest leaf.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task The_cycle_check_walks_an_arbitrarily_deep_chain()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var root = await fixture.CreateDepartmentAsync("L0", "Level 0");
    var current = root;
    for (var level = 1; level <= 4; level++)
    {
      current = await fixture.CreateDepartmentAsync($"L{level}", $"Level {level}", current);
    }

    var moved = await graph.ChangeParent().HandleAsync(
      new ChangeDepartmentParentCommand(root, current, await fixture.RowVersionAsync(root)));

    Assert.True(moved.IsFailure);
    Assert.Equal(DepartmentErrors.HierarchyCycle, moved.Error);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_department_cannot_become_its_own_parent()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("A", "Alpha");

    var moved = await graph.ChangeParent().HandleAsync(
      new ChangeDepartmentParentCommand(a, a, await fixture.RowVersionAsync(a)));

    Assert.True(moved.IsFailure);
    Assert.Equal(DepartmentErrors.ParentIsSelf, moved.Error);
  }

  // A legal move carries the whole subtree with it. Descendants are never detached.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Moving_a_department_carries_its_subtree()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("A", "Alpha");
    var b = await fixture.CreateDepartmentAsync("B", "Bravo", a);
    var c = await fixture.CreateDepartmentAsync("C", "Charlie", b);
    var d = await fixture.CreateDepartmentAsync("D", "Delta");

    var moved = await graph.ChangeParent().HandleAsync(
      new ChangeDepartmentParentCommand(b, d, await fixture.RowVersionAsync(b)));

    Assert.True(moved.IsSuccess, moved.Error.Code);
    Assert.Equal(d, await fixture.ParentOfAsync(b));

    // C's own parent is untouched — it moved because B did, not because anything rewrote it.
    Assert.Equal(b, await fixture.ParentOfAsync(c));
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_department_can_be_moved_to_the_root()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("A", "Alpha");
    var b = await fixture.CreateDepartmentAsync("B", "Bravo", a);

    var moved = await graph.MoveToRoot().HandleAsync(
      new MoveDepartmentToRootCommand(b, await fixture.RowVersionAsync(b)));

    Assert.True(moved.IsSuccess, moved.Error.Code);
    Assert.Null(await fixture.ParentOfAsync(b));
  }

  // ---- CROSS-COMPANY AND INACTIVE PARENTS, over real rows.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_parent_from_another_company_is_refused()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var mine = await fixture.CreateDepartmentAsync("A", "Alpha");
    var theirs = await fixture.CreateDepartmentAsync("B", "Bravo", company: fixture.CompanyB);

    var moved = await graph.ChangeParent().HandleAsync(
      new ChangeDepartmentParentCommand(mine, theirs, await fixture.RowVersionAsync(mine)));

    Assert.True(moved.IsFailure);
    Assert.Equal(DepartmentErrors.ParentInDifferentCompany, moved.Error);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task An_inactive_parent_is_refused()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("A", "Alpha");
    var parent = await fixture.CreateDepartmentAsync("P", "Parent");

    Assert.True((await graph.Deactivate().HandleAsync(
      new DeactivateDepartmentCommand(parent, await fixture.RowVersionAsync(parent)))).IsSuccess);

    var moved = await graph.ChangeParent().HandleAsync(
      new ChangeDepartmentParentCommand(a, parent, await fixture.RowVersionAsync(a)));

    Assert.True(moved.IsFailure);
    Assert.Equal(DepartmentErrors.ParentInactive, moved.Error);
  }

  // ---- A STALE TOKEN IS REFUSED, and the hierarchy is left alone.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_stale_row_version_refuses_a_move()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("A", "Alpha");
    var b = await fixture.CreateDepartmentAsync("B", "Bravo");

    var stale = await fixture.RowVersionAsync(a);
    Assert.True((await graph.Update().HandleAsync(
      new UpdateDepartmentCommand(a, "A", "Alpha Renamed", stale))).IsSuccess);

    var moved = await graph.ChangeParent().HandleAsync(
      new ChangeDepartmentParentCommand(a, b, stale));

    Assert.True(moved.IsFailure);
    Assert.Equal(DepartmentErrors.ConcurrencyConflict, moved.Error);
    Assert.Null(await fixture.ParentOfAsync(a));
  }

  // ================================================================================================
  // CREATE AND UPDATE
  // ================================================================================================

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_duplicate_normalized_code_is_refused_within_the_company()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    Assert.True((await graph.Create().HandleAsync(
      new CreateDepartmentCommand(fixture.CompanyA, "sales", "Sales", null))).IsSuccess);

    // Differs only by case, and the domain normalizes before the index sees it.
    var duplicate = await graph.Create().HandleAsync(
      new CreateDepartmentCommand(fixture.CompanyA, "SALES", "Sales Again", null));

    Assert.True(duplicate.IsFailure);
    Assert.Equal(DepartmentErrors.CodeConflict, duplicate.Error);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task The_same_code_is_free_in_another_company()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();

    // TWO GRAPHS, because each is scoped to one company. A single graph creating in both would mean the
    // company boundary was not being enforced — the CompanyA graph is genuinely unable to write a CompanyB
    // row, which the next assertion records rather than working around.
    await using var inA = fixture.Graph(fixture.CompanyA);
    await using var inB = fixture.Graph(fixture.CompanyB);

    Assert.True((await inA.Create().HandleAsync(
      new CreateDepartmentCommand(fixture.CompanyA, "SALES", "Sales", null))).IsSuccess);
    Assert.True((await inB.Create().HandleAsync(
      new CreateDepartmentCommand(fixture.CompanyB, "SALES", "Sales", null))).IsSuccess);

    // ---- AND THE COMPANY BOUNDARY IS WHAT KEPT THEM APART.
    //
    // The CompanyA graph is refused when it names CompanyB, so the two `SALES` rows above exist because the
    // code is unique PER COMPANY — not because one caller was free to write anywhere.
    var crossCompany = await inA.Create().HandleAsync(
      new CreateDepartmentCommand(fixture.CompanyB, "OPS", "Operations", null));

    Assert.True(crossCompany.IsFailure);
    Assert.Equal(DepartmentErrors.CompanyScopeDenied, crossCompany.Error);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_department_may_keep_its_own_code_when_renamed()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("SALES", "Sales");

    // Renaming without changing the code must not collide with the department's own row.
    var updated = await graph.Update().HandleAsync(
      new UpdateDepartmentCommand(a, "SALES", "Sales Team", await fixture.RowVersionAsync(a)));

    Assert.True(updated.IsSuccess, updated.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Renaming_onto_another_departments_code_is_refused()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("SALES", "Sales");
    await fixture.CreateDepartmentAsync("OPS", "Operations");

    var updated = await graph.Update().HandleAsync(
      new UpdateDepartmentCommand(a, "OPS", "Sales", await fixture.RowVersionAsync(a)));

    Assert.True(updated.IsFailure);
    Assert.Equal(DepartmentErrors.CodeConflict, updated.Error);
  }

  // ---- THE ORDINARY UPDATE CANNOT REACH PARENT OR STATUS, and the proof is the type itself.
  //
  // There is no field to set, so this is a compile-time guarantee rather than a runtime refusal. Asserting
  // it here records that the absence is load-bearing rather than incidental.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_update_command_carries_no_parent_status_or_manager()
  {
    var properties = typeof(UpdateDepartmentCommand)
      .GetProperties()
      .Select(property => property.Name)
      .ToArray();

    Assert.Equal(["DepartmentId", "Code", "Name", "RowVersion"], properties);
  }

  // ================================================================================================
  // LIFECYCLE
  // ================================================================================================

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Deactivation_is_refused_while_an_active_child_remains()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var parent = await fixture.CreateDepartmentAsync("P", "Parent");
    var child = await fixture.CreateDepartmentAsync("C", "Child", parent);

    var refused = await graph.Deactivate().HandleAsync(
      new DeactivateDepartmentCommand(parent, await fixture.RowVersionAsync(parent)));

    Assert.True(refused.IsFailure);
    Assert.Equal(DepartmentErrors.HasActiveChildren, refused.Error);

    // Deactivate the child, and the parent becomes deactivatable. No cascade did it — the operator did.
    Assert.True((await graph.Deactivate().HandleAsync(
      new DeactivateDepartmentCommand(child, await fixture.RowVersionAsync(child)))).IsSuccess);

    var allowed = await graph.Deactivate().HandleAsync(
      new DeactivateDepartmentCommand(parent, await fixture.RowVersionAsync(parent)));

    Assert.True(allowed.IsSuccess, allowed.Error.Code);

    // ---- AND THE CHILD WAS NOT CASCADED. It is inactive because it was deactivated explicitly.
    Assert.Equal(DepartmentStatus.Inactive, await fixture.StatusAsync(child));
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Reactivation_beneath_an_inactive_parent_is_refused()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var parent = await fixture.CreateDepartmentAsync("P", "Parent");
    var child = await fixture.CreateDepartmentAsync("C", "Child", parent);

    Assert.True((await graph.Deactivate().HandleAsync(
      new DeactivateDepartmentCommand(child, await fixture.RowVersionAsync(child)))).IsSuccess);
    Assert.True((await graph.Deactivate().HandleAsync(
      new DeactivateDepartmentCommand(parent, await fixture.RowVersionAsync(parent)))).IsSuccess);

    // The only path to an active child beneath an inactive parent, and it is closed.
    var refused = await graph.Reactivate().HandleAsync(
      new ReactivateDepartmentCommand(child, await fixture.RowVersionAsync(child)));

    Assert.True(refused.IsFailure);
    Assert.Equal(DepartmentErrors.ParentInactive, refused.Error);

    // Reactivate the parent first and the child follows — again, by an explicit operation.
    Assert.True((await graph.Reactivate().HandleAsync(
      new ReactivateDepartmentCommand(parent, await fixture.RowVersionAsync(parent)))).IsSuccess);
    Assert.True((await graph.Reactivate().HandleAsync(
      new ReactivateDepartmentCommand(child, await fixture.RowVersionAsync(child)))).IsSuccess);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Deactivating_twice_is_refused_as_an_invalid_transition()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("A", "Alpha");

    Assert.True((await graph.Deactivate().HandleAsync(
      new DeactivateDepartmentCommand(a, await fixture.RowVersionAsync(a)))).IsSuccess);

    var again = await graph.Deactivate().HandleAsync(
      new DeactivateDepartmentCommand(a, await fixture.RowVersionAsync(a)));

    Assert.True(again.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidTransition, again.Error);
  }

  // ================================================================================================
  // MANAGER
  // ================================================================================================

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_manager_can_be_assigned_replaced_and_cleared()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var department = await fixture.CreateDepartmentAsync("A", "Alpha");
    var first = await fixture.InsertEmployeeAsync("E-0001");
    var second = await fixture.InsertEmployeeAsync("E-0002");

    Assert.True((await graph.AssignManager().HandleAsync(
      new AssignDepartmentManagerCommand(
        department, first, await fixture.RowVersionAsync(department)))).IsSuccess);
    Assert.Equal(first, await fixture.ManagerOfAsync(department));

    // REPLACEMENT MUTATES THE EXISTING ROW. One row before, one row after — never two, and never zero in
    // between.
    Assert.True((await graph.AssignManager().HandleAsync(
      new AssignDepartmentManagerCommand(
        department, second, await fixture.RowVersionAsync(department)))).IsSuccess);
    Assert.Equal(second, await fixture.ManagerOfAsync(department));
    Assert.Equal(1, await fixture.ManagerRowCountAsync(department));

    Assert.True((await graph.ClearManager().HandleAsync(
      new ClearDepartmentManagerCommand(
        department, await fixture.RowVersionAsync(department)))).IsSuccess);
    Assert.Null(await fixture.ManagerOfAsync(department));
  }

  // ---- CONCURRENT REPLACEMENT CANNOT PRODUCE TWO ROWS.
  //
  // The primary key on DepartmentId makes a second row unrepresentable, and the association's own
  // RowVersion means two callers replacing from the same read cannot both succeed.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Concurrent_manager_assignment_cannot_produce_two_rows()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();

    var department = await fixture.CreateDepartmentAsync("A", "Alpha");
    var first = await fixture.InsertEmployeeAsync("E-0001");
    var second = await fixture.InsertEmployeeAsync("E-0002");

    await using var one = fixture.Graph();
    await using var two = fixture.Graph();

    var version = await fixture.RowVersionAsync(department);

    var results = await Task.WhenAll(
      one.AssignManager().HandleAsync(new AssignDepartmentManagerCommand(department, first, version)),
      two.AssignManager().HandleAsync(new AssignDepartmentManagerCommand(department, second, version)));

    // At most one row, whatever happened above.
    Assert.Equal(1, await fixture.ManagerRowCountAsync(department));
    Assert.Contains(results, result => result.IsSuccess);

    // ================================================================================================
    // TWO LEGITIMATE OUTCOMES, AND INVARIANTS RATHER THAN A SCRIPTED PATH.
    // ================================================================================================
    //
    // (i)  BOTH SUCCEED. Assignment is an upsert: the handler reads first and REASSIGNS when a row
    //      already exists. Assigning a manager does not touch the Departments row, so the second
    //      caller's rowversion token is still fresh — it sees the committed association and replaces
    //      it. One row, two successes, and nothing wrong.
    //
    // (ii) ONE SUCCEEDS AND THE LOSER FAILS GRACEFULLY, when both read "no manager" before either
    //      committed and both attempted the INSERT. The loser is then stopped either by the department's
    //      rowversion check or by PK_DepartmentManagers — whichever fires first, which is itself a race.
    //
    // Asserting "exactly one loser" would be wrong: it would fail on outcome (i), which is correct
    // behaviour. THE REAL INVARIANT IS THAT NO EXCEPTION ESCAPES AND NO SECOND ROW APPEARS — before
    // FP-007 Phase 3 the PK route threw DbUpdateException out of Task.WhenAll, not because production
    // lacked the translation but because this fixture's unit-of-work double did.
    string[] sanctioned =
    [
      SSAS.Platform.Domain.IdentityAccessErrors.ConcurrencyConflict.Code,
      SSAS.Platform.Domain.IdentityAccessErrors.UniqueConstraintViolation.Code
    ];

    foreach (var result in results)
    {
      if (result.IsFailure)
      {
        Assert.Contains(result.Error.Code, sanctioned);
      }
    }

    // The surviving manager is one of the two candidates — not a third value, and not null.
    var manager = await fixture.ManagerOfAsync(department);

    Assert.True(
      manager == first || manager == second,
      $"The surviving manager {manager} is neither candidate.");

    // ---- WHICH OUTCOME OCCURRED, RECORDED RATHER THAN ASSERTED.
    //
    // The test passes either way, so without this the two branches are indistinguishable from outside and
    // the graceful-loser path could stop being exercised for a long time before anyone noticed. Repeating
    // this test and reading the line below is how the race is confirmed to still be a race.
    var losers = results.Where(result => result.IsFailure).ToArray();

    output.WriteLine(losers.Length == 0
      ? "OUTCOME: both-success (the second call saw the committed row and reassigned)"
      : $"OUTCOME: graceful-loser ({string.Join(", ", losers.Select(result => result.Error.Code))})");
  }

  // ================================================================================================
  // FIXTURE ↔ PRODUCTION TRANSLATION PARITY. THIS TEST EXISTS BECAUSE THE DOUBLE DRIFTED ONCE.
  // ================================================================================================
  //
  // `DepartmentGraph.SingleContextUnitOfWork` stands in for `TenantUnitOfWork`, and its whole
  // justification is behaving like it. It caught `DbUpdateConcurrencyException` alone while production
  // ALSO translates SQL 2601/2627 (TenantUnitOfWork.cs:36-47), so a losing INSERT surfaced here as an
  // escaping `DbUpdateException` and in the Host as an ordinary `Result` failure. Tests above it were
  // asserting against behaviour the application does not have.
  //
  // ---- WHY THIS GOES THROUGH THE UNIT OF WORK RATHER THAN THROUGH AssignManager.
  //
  // The handler READS BEFORE IT WRITES and reassigns when a row already exists, so no arrangement of
  // handler calls can be made to attempt a duplicate insert on demand — pre-inserting the row simply
  // sends the handler down the reassign branch and it succeeds. The primary-key branch is reachable
  // only through a genuine interleave, which a deterministic test cannot schedule.
  //
  // So this proves the translation for what it actually is: a property of the unit of work. The
  // end-to-end race is covered by the concurrent test above; this covers the branch that was broken.
  //
  // KEEP IN LOCKSTEP WITH PRODUCTION'S CATCH SET. If TenantUnitOfWork gains or changes a translation,
  // the double and this test change with it.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task The_fixture_unit_of_work_translates_unique_key_violations_like_production()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();

    var department = await fixture.CreateDepartmentAsync("A", "Alpha");
    var sitting = await fixture.InsertEmployeeAsync("E-0001");
    var contender = await fixture.InsertEmployeeAsync("E-0002");

    // TWO GRAPHS, because a single context would refuse the duplicate in its own change tracker and
    // never reach SQL Server — and it is SQL Server's refusal that has to be translated.
    await using var first = fixture.Graph();
    await using var second = fixture.Graph();

    var seated = DepartmentManager.Assign(
      department, fixture.Tenant, fixture.CompanyA, sitting, "parity-test", DateTimeOffset.UtcNow);
    Assert.True(seated.IsSuccess);
    Assert.True((await first.SaveManagerDirectlyAsync(seated.Value)).IsSuccess);

    // The same PRIMARY KEY — DepartmentId identifies the association — with a different employee.
    var duplicate = DepartmentManager.Assign(
      department, fixture.Tenant, fixture.CompanyA, contender, "parity-test", DateTimeOffset.UtcNow);
    Assert.True(duplicate.IsSuccess);

    var refused = await second.SaveManagerDirectlyAsync(duplicate.Value);

    // A RESULT, NOT AN EXCEPTION — and production's own error, not a department-local equivalent.
    Assert.True(refused.IsFailure);
    Assert.Equal(
      SSAS.Platform.Domain.IdentityAccessErrors.UniqueConstraintViolation.Code, refused.Error.Code);

    // And the invariant the primary key exists to hold: one row, still the first manager.
    Assert.Equal(1, await fixture.ManagerRowCountAsync(department));
    Assert.Equal(sitting, await fixture.ManagerOfAsync(department));
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_manager_from_another_company_is_refused()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var department = await fixture.CreateDepartmentAsync("A", "Alpha");
    var outsider = await fixture.InsertEmployeeAsync("E-0009", company: fixture.CompanyB);

    var assigned = await graph.AssignManager().HandleAsync(
      new AssignDepartmentManagerCommand(department, outsider, await fixture.RowVersionAsync(department)));

    Assert.True(assigned.IsFailure);
    Assert.Equal(DepartmentErrors.ManagerInDifferentCompany, assigned.Error);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_terminated_employee_is_refused_as_a_manager()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var department = await fixture.CreateDepartmentAsync("A", "Alpha");
    var employee = await fixture.InsertEmployeeAsync("E-0001", terminated: true);

    var assigned = await graph.AssignManager().HandleAsync(
      new AssignDepartmentManagerCommand(department, employee, await fixture.RowVersionAsync(department)));

    Assert.True(assigned.IsFailure);
    Assert.Equal(DepartmentErrors.ManagerTerminated, assigned.Error);
  }

  // ---- AN EMPLOYEE FROM ANOTHER BRANCH OF THE SAME COMPANY IS ELIGIBLE.
  //
  // Branch is not consulted at all. A department spans the branches of its company, so requiring the
  // manager to work at one of them would name one arbitrarily.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task An_employee_from_another_branch_of_the_same_company_may_manage()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var department = await fixture.CreateDepartmentAsync("A", "Alpha");
    var employee = await fixture.InsertEmployeeAsync("E-0001", branch: fixture.BranchB);

    var assigned = await graph.AssignManager().HandleAsync(
      new AssignDepartmentManagerCommand(department, employee, await fixture.RowVersionAsync(department)));

    Assert.True(assigned.IsSuccess, assigned.Error.Code);
  }

  // ---- TERMINATION AFTER ASSIGNMENT DOES NOT CLEAR THE ASSOCIATION, AND THE READ SAYS SO.
  //
  // The row stands — clearing it would destroy the record that there had been a manager — and the read
  // model reports the manager as no longer active rather than presenting a terminated person as the
  // current head of a department.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_terminated_sitting_manager_is_retained_but_never_reported_as_active()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var department = await fixture.CreateDepartmentAsync("A", "Alpha");
    var employee = await fixture.InsertEmployeeAsync("E-0001");

    Assert.True((await graph.AssignManager().HandleAsync(
      new AssignDepartmentManagerCommand(
        department, employee, await fixture.RowVersionAsync(department)))).IsSuccess);

    await fixture.TerminateEmployeeAsync(employee);

    Assert.Equal(1, await fixture.ManagerRowCountAsync(department));

    var read = await graph.Get().HandleAsync(new GetDepartmentQuery(department));

    Assert.True(read.IsSuccess, read.Error.Code);
    Assert.NotNull(read.Value.Manager);
    Assert.True(read.Value.Manager!.IsAssigned);
    Assert.Equal(employee, read.Value.Manager.EmployeeId);
    Assert.False(read.Value.Manager.IsActive);
  }

  // ================================================================================================
  // THE MANAGER'S IDENTITY IS BRANCH-SCOPED EVEN THOUGH THE DEPARTMENT IS NOT
  // ================================================================================================
  //
  // The leak this design exists to prevent: a caller authorized for Riyadh reads a company-wide department
  // whose manager works in Jeddah. The DEPARTMENT is visible — that is the approved company-scoped
  // visibility — but the manager's name and number are employee data the caller has no scope for.
  //
  // A join inside the department read would have disclosed them on the strength of department visibility
  // alone. Instead the caller is told that a manager IS assigned and nothing more.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_manager_outside_the_callers_branch_scope_is_assigned_but_undisclosed()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();

    // The assigning caller can see both branches.
    await using var assigner = fixture.Graph(visibleBranches: [fixture.BranchA, fixture.BranchB]);

    var department = await fixture.CreateDepartmentAsync("FIN", "Finance");
    var manager = await fixture.InsertEmployeeAsync("E-0001", branch: fixture.BranchB);

    Assert.True((await assigner.AssignManager().HandleAsync(
      new AssignDepartmentManagerCommand(
        department, manager, await fixture.RowVersionAsync(department)))).IsSuccess);

    // ---- A NARROWER READER: branch A only, and the manager works in branch B.
    await using var narrow = fixture.Graph(visibleBranches: [fixture.BranchA]);

    var read = await narrow.Get().HandleAsync(new GetDepartmentQuery(department));

    Assert.True(read.IsSuccess, read.Error.Code);

    // The DEPARTMENT is visible — company-scoped visibility is the approved rule.
    Assert.Equal("FIN", read.Value.Code);

    // The MANAGER is not. Assigned, yes; who they are, no.
    Assert.NotNull(read.Value.Manager);
    Assert.True(read.Value.Manager!.IsAssigned);
    Assert.Null(read.Value.Manager.EmployeeId);
    Assert.Null(read.Value.Manager.FullName);
    Assert.Null(read.Value.Manager.EmployeeNumber);

    // ---- AND THE WIDER READER SEES THEM, so the redaction above is scope working rather than the feature
    // being broken.
    var wide = await assigner.Get().HandleAsync(new GetDepartmentQuery(department));

    Assert.True(wide.IsSuccess, wide.Error.Code);
    Assert.Equal(manager, wide.Value.Manager!.EmployeeId);
    Assert.Equal("Person E-0001", wide.Value.Manager.FullName);
  }

  // ---- THE OTHER REASON A MANAGER IS UNDISCLOSED, PROVEN SEPARATELY.
  //
  // The test above redacts because the manager works in a branch the caller cannot see. This one redacts
  // because the caller holds no `HR.Employees.View` at all — same visible answer, entirely different cause.
  //
  // They are split apart deliberately. While the fixture granted only the four department permissions,
  // EVERY caller in this file hit this path, so the branch-scope test's redaction assertions passed without
  // branch scope ever being consulted — the proof was vacuous and looked identical to a real one. Keeping a
  // caller that genuinely lacks the permission is what stops the two collapsing back together.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_caller_without_the_employee_permission_learns_only_that_a_manager_exists()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var assigner = fixture.Graph();

    var department = await fixture.CreateDepartmentAsync("FIN", "Finance");

    // Same branch as the reader below, so BRANCH SCOPE CANNOT BE THE CAUSE of what follows.
    var manager = await fixture.InsertEmployeeAsync("E-0001", branch: fixture.BranchA);

    Assert.True((await assigner.AssignManager().HandleAsync(
      new AssignDepartmentManagerCommand(
        department, manager, await fixture.RowVersionAsync(department)))).IsSuccess);

    await using var unpermitted = fixture.Graph(
      visibleBranches: [fixture.BranchA], canViewEmployees: false);

    var read = await unpermitted.Get().HandleAsync(new GetDepartmentQuery(department));

    // The DEPARTMENT read still succeeds. Lacking the employee permission is not an error here — the
    // department is company-visible and the caller holds ViewDepartments.
    Assert.True(read.IsSuccess, read.Error.Code);
    Assert.Equal("FIN", read.Value.Code);

    Assert.NotNull(read.Value.Manager);
    Assert.True(read.Value.Manager!.IsAssigned);
    Assert.Null(read.Value.Manager.EmployeeId);
    Assert.Null(read.Value.Manager.FullName);
    Assert.Null(read.Value.Manager.EmployeeNumber);

    // AND THE SAME BRANCH, WITH THE PERMISSION, DISCLOSES THEM — so the redaction above is the permission
    // gate working rather than the manager being unreachable for some other reason.
    await using var permitted = fixture.Graph(visibleBranches: [fixture.BranchA]);

    var disclosed = await permitted.Get().HandleAsync(new GetDepartmentQuery(department));

    Assert.True(disclosed.IsSuccess, disclosed.Error.Code);
    Assert.Equal(manager, disclosed.Value.Manager!.EmployeeId);
    Assert.Equal("Person E-0001", disclosed.Value.Manager.FullName);
  }

  // No association at all is a DIFFERENT answer from "assigned but undisclosed", and the two must never be
  // confused: one means the department needs a manager, the other means you may not know who it has.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_department_with_no_manager_reports_no_manager_at_all()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var department = await fixture.CreateDepartmentAsync("FIN", "Finance");

    var read = await graph.Get().HandleAsync(new GetDepartmentQuery(department));

    Assert.True(read.IsSuccess, read.Error.Code);
    Assert.Null(read.Value.Manager);
  }

  // ================================================================================================
  // READS
  // ================================================================================================

  // Company scope restricts what the query returns — proven against the database rather than against the
  // resolver, which is where the DECISION is proven.
  [Fact]
  [Trait("Decision", "ADR-025")]
  public async Task A_department_in_an_unauthorized_company_is_not_found()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var theirs = await fixture.CreateDepartmentAsync("B", "Bravo", company: fixture.CompanyB);

    // The graph's scope covers CompanyA only.
    var read = await graph.Get().HandleAsync(new GetDepartmentQuery(theirs));

    Assert.True(read.IsFailure);
    Assert.Equal(DepartmentErrors.NotFound, read.Error);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Search_is_ordered_deterministically_and_pages_stably()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    foreach (var code in new[] { "D", "B", "E", "A", "C" })
    {
      await fixture.CreateDepartmentAsync(code, $"Department {code}");
    }

    var first = await graph.Search().HandleAsync(new SearchDepartmentsQuery(Page: 1, PageSize: 2));
    var second = await graph.Search().HandleAsync(new SearchDepartmentsQuery(Page: 2, PageSize: 2));

    Assert.True(first.IsSuccess, first.Error.Code);
    Assert.Equal(5, first.Value.TotalCount);
    Assert.Equal(["A", "B"], first.Value.Items.Select(item => item.Code));
    Assert.Equal(["C", "D"], second.Value.Items.Select(item => item.Code));
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Search_refuses_an_out_of_range_page_size_rather_than_clamping()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var refused = await graph.Search().HandleAsync(
      new SearchDepartmentsQuery(PageSize: DepartmentSearchCriteria.MaxPageSize + 1));

    Assert.True(refused.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidPagination, refused.Error);
  }

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Children_are_returned_for_one_level_only()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    var a = await fixture.CreateDepartmentAsync("A", "Alpha");
    var b = await fixture.CreateDepartmentAsync("B", "Bravo", a);
    await fixture.CreateDepartmentAsync("C", "Charlie", b);

    var children = await graph.Children().HandleAsync(new GetDepartmentChildrenQuery(a));

    Assert.True(children.IsSuccess, children.Error.Code);

    // B only. C is a grandchild, and this is an adjacency read rather than a tree walk.
    Assert.Equal([b], children.Value.Select(child => child.DepartmentId));
  }

  // ================================================================================================
  // SEARCH BY TEXT — THE PATH THAT THREW FROM FP-007 UNTIL FP-008 PHASE 2 (DEC-POS-0030)
  // ================================================================================================
  //
  // These tests exist because their absence is what let the defect ship. `SearchAsync` filtered on
  // `Name.Value.Contains(text)`, EF Core cannot translate a member access through a value converter inside a
  // predicate, and every search carrying a `searchText` threw `InvalidOperationException` rather than
  // returning rows. Every other department test passed throughout, because none of them passed a search
  // text — the pagination test builds a `SearchDepartmentsQuery` with no filter at all.
  //
  // The fix is the ruled pattern: a domain-maintained `NormalizedName` column, searched with `LIKE` and an
  // explicit `ESCAPE`.
  [Fact]
  [Trait("Decision", "DEC-POS-0030")]
  public async Task A_search_matches_the_department_name_and_the_code()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    await fixture.CreateDepartmentAsync("FIN", "Finance");
    await fixture.CreateDepartmentAsync("OPS", "Operations");
    await fixture.CreateDepartmentAsync("HR", "People and Culture");

    // BY NAME. This is the query shape that used to throw.
    var byName = await graph.Search().HandleAsync(new SearchDepartmentsQuery(SearchText: "Finance"));
    Assert.True(byName.IsSuccess, byName.IsFailure ? byName.Error.Code : null);
    Assert.Equal("FIN", byName.Value.Items.Single().Code);

    // BY CODE, which is a PREFIX match rather than a contains — the two halves have different shapes
    // because a code is typed from the start and a name is remembered in part.
    var byCode = await graph.Search().HandleAsync(new SearchDepartmentsQuery(SearchText: "OPS"));
    Assert.True(byCode.IsSuccess, byCode.IsFailure ? byCode.Error.Code : null);
    Assert.Equal("OPS", byCode.Value.Items.Single().Code);

    // A MID-WORD FRAGMENT of a name matches; the same fragment matches no code prefix.
    var fragment = await graph.Search().HandleAsync(new SearchDepartmentsQuery(SearchText: "ultur"));
    Assert.True(fragment.IsSuccess, fragment.IsFailure ? fragment.Error.Code : null);
    Assert.Equal("HR", fragment.Value.Items.Single().Code);
  }

  // ---- NOT FOUND IS AN EMPTY PAGE, NOT A FAILURE.
  //
  // Worth asserting alongside the found case: a search that threw would also "not return the row", and only
  // checking `IsSuccess` on the negative case distinguishes the two.
  [Fact]
  [Trait("Decision", "DEC-POS-0030")]
  public async Task A_search_matching_nothing_succeeds_with_an_empty_page()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    await fixture.CreateDepartmentAsync("FIN", "Finance");

    var found = await graph.Search().HandleAsync(new SearchDepartmentsQuery(SearchText: "Marketing"));

    Assert.True(found.IsSuccess, found.IsFailure ? found.Error.Code : null);
    Assert.Empty(found.Value.Items);
    Assert.Equal(0, found.Value.TotalCount);
  }

  // ---- CASE-INSENSITIVE, OVER A BINARY-COLLATED COLUMN.
  //
  // Both sides are upper-invariant — the stored value by the domain, the pattern by the query — which is
  // what makes an ordinal column searchable without a case-insensitive collation.
  [Theory]
  [InlineData("finance")]
  [InlineData("FINANCE")]
  [InlineData("FiNaNcE")]
  [InlineData("  finance  ")]
  [Trait("Decision", "DEC-POS-0030")]
  public async Task A_search_is_case_insensitive_and_trimmed(string searchText)
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    await fixture.CreateDepartmentAsync("FIN", "Finance");

    var found = await graph.Search().HandleAsync(new SearchDepartmentsQuery(SearchText: searchText));

    Assert.True(found.IsSuccess, found.IsFailure ? found.Error.Code : null);
    Assert.Equal("FIN", found.Value.Items.Single().Code);
  }

  // ---- A WILDCARD IN THE SEARCH TEXT IS A LITERAL CHARACTER.
  //
  // Unescaped, `%` would make the predicate match every department in scope — a search that silently returns
  // everything rather than failing, which is the harder failure to notice.
  [Theory]
  [InlineData("%")]
  [InlineData("_")]
  [InlineData("[")]
  [Trait("Decision", "DEC-POS-0030")]
  public async Task A_wildcard_in_the_department_search_text_matches_only_itself(string wildcard)
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph();

    await fixture.CreateDepartmentAsync("ODD", $"Fifty {wildcard} Owned");
    await fixture.CreateDepartmentAsync("FIN", "Finance");
    await fixture.CreateDepartmentAsync("OPS", "Operations");

    var found = await graph.Search().HandleAsync(new SearchDepartmentsQuery(SearchText: wildcard));

    Assert.True(found.IsSuccess, found.IsFailure ? found.Error.Code : null);
    Assert.Equal("ODD", found.Value.Items.Single().Code);
  }

  // ---- AND THE SEARCH STAYS INSIDE THE COMPANY SCOPE.
  //
  // A text filter narrows what the scope already allows; it never widens it. Asserted because a filter
  // rewritten onto a different column is exactly the change that could be applied to an unscoped query by
  // mistake.
  [Fact]
  [Trait("Decision", "ADR-025")]
  public async Task A_text_search_never_reaches_outside_the_company_scope()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var graph = fixture.Graph(fixture.CompanyA);

    await fixture.CreateDepartmentAsync("FIN", "Finance", company: fixture.CompanyA);
    await fixture.CreateDepartmentAsync("FIN", "Finance", company: fixture.CompanyB);

    var found = await graph.Search().HandleAsync(new SearchDepartmentsQuery(SearchText: "Finance"));

    Assert.True(found.IsSuccess, found.IsFailure ? found.Error.Code : null);
    Assert.Equal(fixture.CompanyA, found.Value.Items.Single().CompanyId);
  }

  // ================================================================================================
  // employeeCount — SCOPE CONTAINMENT, WHICH ONLY A REAL DATABASE CAN PROVE
  // ================================================================================================
  //
  // `api-contracts.md` specifies the count "within the caller's employee read scope", and a stub cannot
  // demonstrate that: it returns whatever it was seeded with regardless of the predicate. These run the
  // shipped composer over real rows, so the filtering is SQL Server's and the assertion is about the query.
  //
  // The scope-visible reading is the shipped POSITION behaviour, mirrored deliberately: the count answers
  // "how many members may this caller see", not "how many members exist". A company-wide count would leak
  // the size of branches the caller cannot read, which is the one thing `OD-DEP-005` forbids.

  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task A_department_member_count_includes_only_employees_inside_the_callers_scope()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();

    var department = await fixture.CreateDepartmentAsync("FIN", "Finance");

    // Two members in BranchA, one in BranchB, and one in the SAME BRANCH but another company.
    await fixture.InsertEmployeeAsync("E-0001", branch: fixture.BranchA, department: department);
    await fixture.InsertEmployeeAsync("E-0002", branch: fixture.BranchA, department: department);
    await fixture.InsertEmployeeAsync("E-0003", branch: fixture.BranchB, department: department);

    // A caller confined to BranchA sees two of the three.
    await using var narrow = fixture.Graph(visibleBranches: [fixture.BranchA]);

    Assert.Equal(2, await narrow.EmployeeCounts().CountEmployeesAsync(department, default));

    // The SAME department read by a caller authorized for both branches sees all three — so the number
    // above is the scope narrowing it, not the department having only two members.
    await using var wide = fixture.Graph(visibleBranches: [fixture.BranchA, fixture.BranchB]);

    Assert.Equal(3, await wide.EmployeeCounts().CountEmployeesAsync(department, default));
  }

  // ---- A DEPARTMENT WITH NO VISIBLE MEMBERS IS ZERO, AND ZERO IS NOT NULL.
  //
  // The distinction the wire contract turns on, proven at the source rather than only at the transport: a
  // caller who CAN read employees and sees none gets the number 0, while a caller who cannot read employees
  // at all gets null from the same call. Asserting both here means the two can never quietly converge.
  [Fact]
  [Trait("Decision", "DEC-POS-0034")]
  public async Task An_empty_department_counts_zero_while_an_unscoped_caller_counts_null()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();

    var department = await fixture.CreateDepartmentAsync("FIN", "Finance");

    // A member the PERMITTED caller can see, so the null below cannot be mistaken for an empty department.
    await fixture.InsertEmployeeAsync("E-0001", branch: fixture.BranchA, department: department);

    var empty = await fixture.CreateDepartmentAsync("OPS", "Operations");

    await using var permitted = fixture.Graph(visibleBranches: [fixture.BranchA]);

    Assert.Equal(1, await permitted.EmployeeCounts().CountEmployeesAsync(department, default));
    Assert.Equal(0, await permitted.EmployeeCounts().CountEmployeesAsync(empty, default));

    await using var unpermitted = fixture.Graph(
      visibleBranches: [fixture.BranchA], canViewEmployees: false);

    Assert.Null(await unpermitted.EmployeeCounts().CountEmployeesAsync(department, default));
  }

  // ---- ANOTHER COMPANY'S EMPLOYEES ARE NEVER COUNTED.
  //
  // Department identifiers are unique across the tenant, so a count keyed on one cannot pick up another
  // company's rows by identifier alone — but the company predicate is what makes that true rather than
  // incidental, and a count written without it would still pass every single-company test above.
  [Fact]
  [Trait("Decision", "ADR-025")]
  public async Task A_member_count_never_reaches_outside_the_company_scope()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();

    var department = await fixture.CreateDepartmentAsync("FIN", "Finance", company: fixture.CompanyA);

    await fixture.InsertEmployeeAsync(
      "E-0001", company: fixture.CompanyA, branch: fixture.BranchA, department: department);

    await using var otherCompany = fixture.Graph(fixture.CompanyB, [fixture.BranchA, fixture.BranchB]);

    Assert.Equal(0, await otherCompany.EmployeeCounts().CountEmployeesAsync(department, default));
  }
}
