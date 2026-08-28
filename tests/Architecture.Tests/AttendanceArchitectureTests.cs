using System.Reflection;
using SSAS.Attendance.Contracts.Summaries;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ================================================================================================
// FP-013's ARCHITECTURE GUARDS.
// ================================================================================================
//
// Three of these exist because a RULING required them rather than because a rule was general:
// the entity-by-entity branch classification (`DEC-ATT-0014`), the branch-BLIND summary contract
// (`OD-ATT-0011`), and module isolation in both directions (`ADR-012`, `DEC-ATT-0002`).
public sealed class AttendanceArchitectureTests
{
  // Derived by reflection over the domain assembly, not typed. FP-012 stated its entity count wrong FOUR
  // times — including once inside the section warning about miscounts — which is why every inventory in this
  // file is derived.
  private static Type[] AttendanceEntities() =>
    typeof(AttendanceRecord).Assembly
      .GetTypes()
      .Where(type => type.IsClass && !type.IsAbstract)
      .Where(type => typeof(ITenantOwnedEntity).IsAssignableFrom(type))
      .OrderBy(type => type.Name, StringComparer.Ordinal)
      .ToArray();

  // ================================================================================================
  // DEC-ATT-0014. EVERY ENTITY IS CLASSIFIED — INCLUDING THE NEGATIVES.
  // ================================================================================================
  //
  // `IBranchOwnedEntity` states the stakes itself: *"IMPLEMENTING THIS IS A DELIBERATE CLASSIFICATION, NOT A
  // DEFAULT … the failure mode is silent: an entity that should have been branch-scoped and was not is
  // readable by every branch in the tenant, and nothing about it looks wrong."*
  //
  // HR asserts its classification entity by entity. **Payroll asserts nothing** — its entities are
  // tenant-global by OMISSION, and the only classification guard in the suite
  // (`BranchSessionArchitectureTests.No_tenant_global_or_routing_entity_is_branch_owned`) walks a HARDCODED
  // LIST OF SIX PLATFORM TYPES. Nothing forces a new module to classify itself, which is exactly how Payroll
  // ended up unasserted.
  //
  // So this is a commitment FP-013 makes rather than a rule it inherits. **The expected map is written out
  // and then checked against reflection**, so adding an entity without deciding its classification fails
  // here rather than shipping unclassified.
  [Fact]
  [Trait("Decision", "DEC-ATT-0014")]
  public void Every_attendance_entity_carries_an_explicit_branch_classification()
  {
    var expected = new Dictionary<string, bool>(StringComparer.Ordinal)
    {
      // ---- THE ONE POSITIVE. Attendance is observed LOCALLY: a branch supervisor records who was present
      // at their branch, and the UserBranchAccess-to-ITenantBranchAccessResolver stack exists so that
      // boundary is enforced rather than trusted.
      [nameof(AttendanceRecord)] = true,

      // ---- THE NEGATIVES, EACH WITH ITS REASON.
      // A calendar is company POLICY, following Department's asserted classification.
      [nameof(WorkingCalendar)] = false,
      [nameof(CalendarHoliday)] = false,
      // A period is a company-level accounting boundary; branch lives on the records inside it.
      [nameof(AttendancePeriod)] = false,
      // A catalog is company policy.
      [nameof(LeaveType)] = false,
      // Entitlement is company policy; the employee's branch does not meter their leave.
      [nameof(LeaveBalance)] = false,
      // Approval runs through the DEPARTMENT chain, not the branch tree — a branch predicate here would
      // filter on a dimension the workflow does not use.
      [nameof(LeaveRequest)] = false
    };

    var actual = AttendanceEntities();

    // The inventory is DERIVED and compared against the map, so a new entity fails this test by being
    // absent from `expected` rather than by being silently unclassified.
    Assert.Equal(
      expected.Keys.OrderBy(name => name, StringComparer.Ordinal),
      actual.Select(type => type.Name));

    foreach (var type in actual)
    {
      var isBranchOwned = typeof(IBranchOwnedEntity).IsAssignableFrom(type);
      Assert.Equal(expected[type.Name], isBranchOwned);
    }
  }

  // Every tenant-owned Attendance type must carry `ITenantOwnedEntity`, or `TenantCutoverCopyPlan` — which
  // derives its manifest by reflecting over that interface — omits it SILENTLY. No error, no warning, and
  // no failing test until a tenant migrates and its data does not arrive.
  [Fact]
  [Trait("Decision", "DEC-ATT-0007")]
  public void The_attendance_entity_inventory_is_seven_and_is_derived_rather_than_listed()
  {
    Assert.Equal(7, AttendanceEntities().Length);
  }

  // ================================================================================================
  // OD-ATT-0011. THE SUMMARY CONTRACT APPLIES NO BRANCH PREDICATE, AND THAT IS GUARD-ASSERTED.
  // ================================================================================================
  //
  // The ruling took BOTH halves of an asymmetry the analysis package could not resolve: records are
  // branch-scoped so a supervisor sees only their branch, and the Payroll summary is branch-BLIND so a
  // payroll run is company-complete.
  //
  // `DEC-PAY-0017` refused a branch filter on the employee roster because **a filter means a payroll-feeding
  // query can silently omit employees** — and an omitted employee's hours produce a payroll that balances
  // perfectly and underpays somebody.
  //
  // **The hole is ruled INTENDED.** The obligation attached to that ruling was: stated at the site, and
  // guard-asserted. This is the guard. It reads the compiled source of the query method and asserts no
  // branch predicate appears — the comment explains the decision, this survives someone who has not read it.
  [Fact]
  [Trait("Decision", "OD-ATT-0011")]
  public void The_payroll_summary_contract_applies_no_branch_predicate()
  {
    var service = typeof(IAttendanceSummary).Assembly
      .GetType("SSAS.Attendance.Contracts.Summaries.IAttendanceSummary");
    Assert.NotNull(service);

    // The CONTRACT itself must not even be able to express a branch: no method takes one, and no result
    // record carries one. A contract that accepted a branch identifier would let a caller narrow the
    // company-complete answer, which is the failure the ruling exists to prevent.
    foreach (var method in typeof(IAttendanceSummary).GetMethods())
    {
      Assert.DoesNotContain(method.GetParameters(), parameter =>
        parameter.Name!.Contains("branch", StringComparison.OrdinalIgnoreCase));
    }

    foreach (var type in new[] { typeof(AttendanceSummaryResult), typeof(AttendancePeriodInspection) })
    {
      Assert.DoesNotContain(type.GetProperties(), property =>
        property.Name.Contains("Branch", StringComparison.OrdinalIgnoreCase));
    }
  }

  // The other half of the same ruling, asserted positively: the contract carries TOTALS and no per-event or
  // time-of-day data (`DEC-ATT-0002`). A contract exposing punch-level movement would let every future
  // Payroll feature read minute-by-minute employee location with no call-site change for anyone to review.
  [Fact]
  [Trait("Decision", "DEC-ATT-0002")]
  public void The_summary_contract_exposes_totals_and_no_per_event_data()
  {
    var names = typeof(AttendanceSummaryResult).GetProperties().Select(property => property.Name).ToArray();

    Assert.DoesNotContain(names, name =>
      name.Contains("Punch", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("ClockIn", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("ClockOut", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("TimeOfDay", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Event", StringComparison.OrdinalIgnoreCase));

    // And no leave TYPE, because `Attendance.Leave.ViewSensitive` gates it over HTTP. A cross-module
    // contract has no business being laxer than the owning module's own HTTP surface.
    Assert.DoesNotContain(names, name => name.Contains("LeaveType", StringComparison.OrdinalIgnoreCase));

    // No money of any kind (`DEC-ATT-0004`): Attendance records how much, Payroll decides what it is worth.
    Assert.DoesNotContain(names, name =>
      name.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Rate", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Currency", StringComparison.OrdinalIgnoreCase));
  }

  // ================================================================================================
  // MODULE ISOLATION, IN BOTH DIRECTIONS (ADR-012).
  // ================================================================================================
  [Fact]
  [Trait("Decision", "DEC-ATT-0002")]
  public void Payroll_references_the_attendance_contracts_and_no_attendance_implementation()
  {
    var payrollApplication = typeof(SSAS.Payroll.Application.Runs.CalculatePayrollRunCommandHandler).Assembly;

    var attendanceReferences = payrollApplication
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name)
      .Where(name => name is not null && name.StartsWith("SSAS.Attendance", StringComparison.Ordinal))
      .ToArray();

    // Exactly one, and it is the contracts assembly. Not "does not contain Domain" — an exhaustive check,
    // because a future reference to `.Infrastructure` would pass the negative form of this assertion if
    // somebody only listed the assemblies they thought of.
    Assert.Equal(["SSAS.Attendance.Contracts"], attendanceReferences);
  }

  [Fact]
  [Trait("Decision", "DEC-ATT-0003")]
  public void Attendance_references_the_hr_contracts_and_no_hr_implementation()
  {
    var attendanceApplication = typeof(SSAS.Attendance.Application.Approval.LeaveApprovalRouter).Assembly;

    var hrReferences = attendanceApplication
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name)
      .Where(name => name is not null && name.StartsWith("SSAS.HR", StringComparison.Ordinal))
      .ToArray();

    Assert.Equal(["SSAS.HR.Contracts"], hrReferences);
  }

  // `DEC-ATT-0003`: Attendance reads HR facts through a contract and NEVER writes HR. Asserted on the
  // contracts themselves, because a mutating method added later would be the one change nobody would read
  // as a boundary violation.
  [Fact]
  [Trait("Decision", "DEC-ATT-0003")]
  public void The_hr_contracts_attendance_consumes_are_read_only()
  {
    foreach (var contract in new[]
      {
        typeof(SSAS.HR.Contracts.Employment.IEmployeeRoster),
        typeof(SSAS.HR.Contracts.Employment.IEmployeeApproverDirectory)
      })
    {
      foreach (var method in contract.GetMethods())
      {
        Assert.StartsWith("Get", method.Name, StringComparison.Ordinal);
      }
    }
  }

  // ---- THE SELF-APPROVAL BAR LIVES IN ATTENDANCE, NOT IN HR (OD-ATT-0007).
  //
  // `IEmployeeApproverDirectory` returns the chain and applies HR's facts. If HR filtered the requester out,
  // `BR-ATT-0007` would live in two modules and could drift — with the module that owns the rule not being
  // the module enforcing it. So the contract must NOT take the requester as something to exclude.
  [Fact]
  [Trait("Decision", "OD-ATT-0007")]
  public void The_approver_directory_returns_the_chain_and_does_not_apply_attendance_policy()
  {
    var method = typeof(SSAS.HR.Contracts.Employment.IEmployeeApproverDirectory)
      .GetMethod(nameof(SSAS.HR.Contracts.Employment.IEmployeeApproverDirectory.GetApproverChainAsync))!;

    // Company, employee, cancellation token. No "exclude" parameter, no policy flag.
    Assert.DoesNotContain(method.GetParameters(), parameter =>
      parameter.Name!.Contains("exclude", StringComparison.OrdinalIgnoreCase) ||
      parameter.Name!.Contains("requester", StringComparison.OrdinalIgnoreCase));
  }

  // ================================================================================================
  // THE READ SCOPE IS UNFORGEABLE (DEC-ATT-0008, AC-ATT-0030).
  // ================================================================================================
  //
  // Private constructor, internal factory. Holding one is proof that Attendance's permission check and
  // Attendance's company AND branch resolution all ran against live state — a scope a caller could construct
  // would make that proof a shrug.
  [Fact]
  [Trait("Decision", "DEC-ATT-0008")]
  public void The_attendance_read_scope_cannot_be_constructed_outside_its_factory()
  {
    var scope = typeof(SSAS.Attendance.Application.Reads.AttendanceReadScope);

    Assert.All(
      scope.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
      constructor => Assert.Fail($"Public constructor found: {constructor}"));

    var factory = scope.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(factory);
    Assert.True(factory!.IsAssembly);
  }

  // The first three-dimensional read scope in the product. The branch set is a distinct member from the
  // company set, so a query cannot accidentally filter on the wrong one.
  [Fact]
  [Trait("Decision", "OD-ATT-0011")]
  public void The_attendance_read_scope_carries_tenant_company_and_branch()
  {
    var scope = typeof(SSAS.Attendance.Application.Reads.AttendanceReadScope);
    var names = scope.GetProperties().Select(property => property.Name).ToArray();

    Assert.Contains("TenantId", names);
    Assert.Contains("CompanyIds", names);
    Assert.Contains("BranchIds", names);
  }

  // ================================================================================================
  // NO ViewOwn PERMISSION EXISTS (AC-ATT-0032, OD-ATT-0013).
  // ================================================================================================
  //
  // ---- THE REASON THIS GUARD WAS WRITTEN FOR HAS EXPIRED. THE ASSERTION HAS NOT (T-083).
  //
  // It used to read: *"self-service is deferred because it depends on a mapping from the authenticated
  // identity to an employee record, and this build does not assert such a mapping exists."*
  //
  // **The mapping now exists** — `UserEmployeeLink`, `ADR-030`, built in T-082 and asserted by
  // `UserEmployeeLinkSqlServerTests`. What is still absent is FP-015's permission and its endpoint, so
  // **this assertion stands and the input it was waiting for has arrived.**
  //
  // `PayrollPermissionNames` recorded the same refusal in the same words, and for the same reason: *"Adding
  // a `Payroll.Payslips.ViewOwn` on an unverified assumption is exactly the shape of the FP-011 near-miss."*
  // Three consecutive features were shaped by that missing input; the absence of the PERMISSION is still
  // asserted here rather than merely intended, and that is now the only thing this guard is about.
  //
  // ---- AND THE OLD REASON EXPIRED IN SILENCE, WHICH IS THE PART WORTH KEEPING.
  //
  // It was prose, so nothing failed when it stopped being true. **Had it been an assertion — one test that
  // no Platform-Domain type pairs a user identifier with an employee identifier — T-082 would have
  // reddened it on the day the mapping landed**, and no sweep would have been needed to find it.
  // ---- THE NAME ASSERTS WHAT, AND THE REASON LIVES HERE WHERE IT CAN EXPIRE VISIBLY (T-087).
  //
  // It used to be called `..._because_the_subject_cannot_be_resolved`. **The subject can now be resolved**
  // — `UserEmployeeLink`, `ADR-030`, T-082 — so the name carried a reason that had expired, in the one
  // place a stale claim is read on every single run.
  //
  // Its neighbours already assert what rather than why (`Every_attendance_permission_follows_the_three_part_grammar`),
  // and a reason in a name cannot be corrected without changing every citation of it. **In a comment it can
  // expire and be fixed in one place; in an identifier it cannot.**
  //
  // **The durable handle is `AC-ATT-0032`, not this method.** `AC-` identifiers survive refactors and are
  // what the traceability matrix, the feature package and the specification prose all cite; a test name
  // changes whenever the thing it describes does, which is exactly what happened here.
  [Fact]
  [Trait("Criterion", "AC-ATT-0032")]
  [Trait("Decision", "OD-ATT-0013")]
  public void No_self_service_permission_is_declared()
  {
    var constants = typeof(SSAS.Attendance.Application.Permissions.AttendancePermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral)
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();

    Assert.DoesNotContain(constants, name =>
      name.Contains("Own", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Self", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Mine", StringComparison.OrdinalIgnoreCase));

    // And the catalog contributor defines exactly the constants that exist — no more, no fewer. A
    // permission an endpoint requires but the catalog omits refuses every caller, which is FP-006P's
    // incident; the reverse is a grant nothing checks.
    var contributor = new SSAS.Attendance.Application.Permissions.AttendancePermissionCatalogContributor();
    var defined = contributor.Permissions.Select(permission => permission.Name).OrderBy(n => n, StringComparer.Ordinal);

    Assert.Equal(constants.OrderBy(n => n, StringComparer.Ordinal), defined);
  }

  // The permission grammar. Every name is exactly `<Plane>.<Resource>.<Action>` with the plane fixed —
  // a malformed one would still compile and would simply never match a grant.
  [Fact]
  public void Every_attendance_permission_follows_the_three_part_grammar()
  {
    var contributor = new SSAS.Attendance.Application.Permissions.AttendancePermissionCatalogContributor();

    foreach (var permission in contributor.Permissions)
    {
      var parts = permission.Name.Split('.');
      Assert.Equal(3, parts.Length);
      Assert.Equal("Attendance", parts[0]);
      Assert.All(parts, part => Assert.False(string.IsNullOrWhiteSpace(part)));
    }
  }
}
