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
  // ---- ⚠ CITES `AC-ATT-0036`, WHICH THIS ASSERTS EXACTLY AND WHICH NOTHING CITED.
  //
  // *"Every Attendance entity carries an explicit branch classification assertion — positive or negative,
  // never absent."* **The expected map above is the "explicit", and checking it against reflection is the
  // "never absent": an entity added without a decision fails here rather than shipping unclassified.**
  //
  // ⚠ HOW IT WAS FOUND, BECAUSE THE ROUTE IS WORTH MORE THAN THE CITATION: **this test reddened during an
  // unrelated `ITenantOwnedEntity` plant hours earlier.** I read only whether the expected test fired and
  // moved on. ***A PLANT NAMES EVERY TEST THAT WATCHES THE PROPERTY IT BREAKS, AND WE HAD BEEN READING ONE
  // ROW OF THAT LIST.*** The other rows are free, already paid for, and answer *what watches what* — which
  // is the question a citation pass exists to answer, in the one language that cannot be misread.
  [Fact]
  [Trait("Decision", "DEC-ATT-0014")]
  [Trait("Criterion", "AC-ATT-0036")]
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
  // ---- ⚠⚠⚠ THE PAYROLL-FACING SUMMARY REFUSES BY THROWING, AND NOTHING ASSERTED THAT.
  //
  // ***DELIBERATELY UNCITED.*** `AC-ATT-0028` describes this mechanism — *"throws
  // `UnauthorizedAccessException` — it does not return an empty list"* — but names a READ SCOPE as its
  // subject, and the read scope does no such thing: `AttendanceScopeResolver` returns `Result.Failure`
  // with `CompanyScopeDenied`. **One criterion, two surfaces.** Whether it is stale, misaddressed, or
  // malformed and in need of splitting is author intent, and it is with the owner. *A test that enforces
  // a misreading is worse than no test, because the gate then defends the misreading.*
  //
  // **The GAP is real under every reading, which is why this exists anyway.** `AttendanceSummaryService`
  // throws twice, is constructed by exactly ONE test file in the tree — an Integration chain — and neither
  // throw is asserted anywhere. A label can be added later; an unwritten test cannot.
  //
  // ⚠ AND THE CONSEQUENCE IS IN THE SOURCE'S OWN WORDS, which is why it is worth a guard rather than a
  // note: *"An empty summary would claim 'this employee worked no hours', a statement about the DATA:
  // payroll would calculate cleanly, pay nothing for the period, and nobody would learn that an
  // authorization check had failed."* **A silent wrong payroll, from a refusal that returned data-shaped
  // emptiness instead of refusing.**
  //
  // ⚠⚠ THE COUNTS ARE EXACT RATHER THAN FLOORS, and that is the assertion doing the work. A floor of one
  // survives somebody converting the second throw into a `Result` — which is exactly the drift this guards,
  // because a `Result` on this path is what the read scope legitimately returns one module over.
  //
  // ⚠⚠⚠ AND THE THIRD PUBLIC METHOD IS EXEMPT, SO ITS GROUNDS ARE ASSERTED RATHER THAN ASSUMED.
  // `GetWorkingDaysAsync` does not authorize. That is defensible — it answers a calendar question and
  // returns no employee data — but *"it looked fine"* is not a guard. **The exemption is pinned by its
  // reason: that method touches no entity set at all.** If it ever queries one, this fails and the
  // exemption has to be re-argued rather than inherited.
  [Fact]
  public void The_payroll_facing_summary_refuses_an_unauthorized_company_by_throwing()
  {
    var source = AttendanceCode("SSAS.Attendance.Infrastructure", "Summaries", "AttendanceSummaryService.cs");

    // MATCHER CONTROL. A source guard's failure mode is reading the wrong file and finding nothing, which
    // is indistinguishable from compliance for every assertion below.
    Assert.Contains("AttendanceSummaryService", source, StringComparison.Ordinal);

    // TWO throws: the unresolved actor, and the company the caller has no grant for.
    Assert.Equal(2, CountOccurrences(source, "throw new UnauthorizedAccessException"));

    // FAIL CLOSED. A FAILED permission lookup refuses; it does not fall through to "all companies".
    Assert.Contains("permitted.IsFailure ||", source, StringComparison.Ordinal);

    // One declaration and two call sites — the two methods that return data. An exact count is what
    // notices a call site being dropped from one of them.
    Assert.Equal(3, CountOccurrences(source, "AuthorizeCompanyAsync"));

    // ---- AND THE EXEMPTION'S GROUNDS.
    var workingDays = source[source.IndexOf(
      "public async Task<int> GetWorkingDaysAsync", StringComparison.Ordinal)..];
    workingDays = workingDays[..workingDays.IndexOf("public async Task<AttendancePeriodInspection>", StringComparison.Ordinal)];

    Assert.DoesNotContain("Set<", workingDays, StringComparison.Ordinal);
    Assert.DoesNotContain("AuthorizeCompanyAsync", workingDays, StringComparison.Ordinal);
  }

  private static int CountOccurrences(string source, string term)
  {
    var count = 0;
    for (var i = source.IndexOf(term, StringComparison.Ordinal); i >= 0;
      i = source.IndexOf(term, i + term.Length, StringComparison.Ordinal))
    {
      count++;
    }

    return count;
  }

  // Comments are stripped before matching. This tree's comments QUOTE the very phrases asserted above —
  // the source's own note explains why the refusal throws — and a raw matcher would credit the prose.
  private static string AttendanceCode(params string[] segments)
  {
    var path = Path.Combine(
      new[] { RepositoryRoot(), "src", "Modules", "Attendance" }.Concat(segments).ToArray());

    Assert.True(File.Exists(path), $"Source not found: {path}");

    return string.Join(
      Environment.NewLine,
      File.ReadAllText(path).Split('\n').Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
      }));
  }

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }

  // ---- ⚠⚠⚠ A RECORD STAYS READABLE AFTER THE EMPLOYEE LEAVES, BECAUSE NO READER CAN LEARN THEY LEFT.
  //
  // `AC-ATT-0009` — *"A record already settled by termination remains readable after termination —
  // `BR-HR-0004` bars new obligations, not the settlement of existing ones."* The write path enforces the
  // first half of that reading and `AttendanceRecordEmploymentWindowTests` drives it. **This is the second
  // half, and it is an ABSENCE: nothing on the read path may filter a record out because its employee was
  // terminated.**
  //
  // ⚠ WHY A DEPENDENCY ASSERTION RATHER THAN A BEHAVIOURAL ONE. Recording, terminating and re-reading needs
  // a database, which puts it in Integration — currently unrunnable here, and an unrun assertion is a claim
  // rather than a check. **The structural statement is available at the gate and is stronger in one
  // specific way: it forbids the filter from being WRITABLE, not merely absent today.**
  //
  // ⚠⚠ THE ENFORCEMENT SET WAS ENUMERATED RATHER THAN ASSUMED. The read path is three types, and none can
  // reach an employment date:
  //
  //   `AttendanceReadService`               a context accessor and a scope resolver
  //   `AttendanceScopeResolver`             company and branch ACCESS, plus current-actor services
  //   `AttendanceSelfServiceScopeResolver`  a user-employee link and a PLACEMENT directory
  //
  // ⚠ AND ONE OF THE THREE IS ALREADY HELD BY THE COMPILER, WHICH IS SAID HERE SO THIS GUARD IS NOT READ
  // AS THREE EQUALLY LOAD-BEARING CHECKS. **`SSAS.Attendance.Infrastructure` does not reference
  // `SSAS.HR.Contracts` at all**, so `AttendanceReadService` COULD NOT take the roster even if somebody
  // tried — that leg would fail to compile before it failed here. *The two Application-layer resolvers are
  // the ones this test actually protects*, and the plant that reddens it was applied to one of them.
  // Keeping the read service in the population costs nothing and states the boundary; it just is not where
  // the risk lives.
  //
  // ⚠⚠⚠ AND THE SECOND ASSERTION IS THE ONE THAT SURVIVES A RENAME. `EmploymentRecord`, reached through
  // `IEmployeeRoster`, is the ONLY contract type carrying employment dates — `EmployeePlacement` is two
  // `Guid`s and nothing else. **So banning the roster is only sound while placement stays dateless**, and
  // adding a `TerminationDateUtc` to it would hand the self-service read exactly the fact this criterion
  // says it must not have, without touching a constructor. *That is asserted on the contract type itself,
  // where the change would happen.*
  [Fact]
  [Trait("Criterion", "AC-ATT-0009")]
  public void No_attendance_read_path_can_learn_that_an_employee_was_terminated()
  {
    var readService = typeof(SSAS.Attendance.Application.Reads.AttendanceScopeResolver).Assembly
      .GetType("SSAS.Attendance.Application.Reads.AttendanceSelfServiceScopeResolver");
    Assert.NotNull(readService);

    Type[] readPath =
    [
      Assembly.Load("SSAS.Attendance.Infrastructure")
        .GetType("SSAS.Attendance.Infrastructure.Persistence.AttendanceReadService")!,
      typeof(SSAS.Attendance.Application.Reads.AttendanceScopeResolver),
      readService!
    ];

    // The population is named, so it cannot silently shrink — but assert it anyway, because a `null` from
    // either lookup above would otherwise reach the loop as a hole rather than as a failure.
    Assert.All(readPath, type => Assert.NotNull(type));

    // ---- NONE OF THEM CAN ASK FOR AN EMPLOYMENT WINDOW.
    foreach (var type in readPath)
    {
      var constructor = Assert.Single(type.GetConstructors());

      Assert.DoesNotContain(
        constructor.GetParameters(),
        parameter => parameter.ParameterType == typeof(SSAS.HR.Contracts.Employment.IEmployeeRoster));
    }

    // ---- AND THE ONE HR CONTRACT THE READ PATH DOES USE CARRIES NO DATE.
    //
    // Asserted as "every property is a `Guid`" rather than "no property is a date": a ban names the shapes
    // it thought of, and this criterion is broken by ANY employment fact arriving here, not only by a
    // `DateTimeOffset`.
    Assert.All(
      typeof(SSAS.HR.Contracts.Employment.EmployeePlacement).GetProperties(),
      property => Assert.Equal(typeof(Guid), property.PropertyType));
  }

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
  // ⚠ CITES `AC-ATT-0023` — *"The summary contract returns totals for one employee and one period, and
  // exposes **no punch-level, per-event or time-of-day data**."* **The second clause is what this asserts,
  // by name, over the record's own properties.** The first clause is carried by the contract's SIGNATURE —
  // `GetSummaryAsync` takes one employee and one date inside one period — which the type system enforces
  // and no test needs to restate.
  //
  // ⚠⚠ THE ASSERTION IS A NAME BAN, SO ITS REACH IS THE VOCABULARY IT LISTS. A per-event field named
  // something none of `Punch`, `ClockIn`, `ClockOut`, `TimeOfDay` or `Event` matches would pass — `Movement`
  // or `Swipe`, say. **That is the honest bound of a name-shaped guard and it is why the ban is broad
  // rather than exact.** *The structural alternative — assert the exact property set — exists next door in
  // `EmploymentTypeAssumptionTests`, which pins `AttendanceSummaryResult`'s components exactly; between the
  // two, an added field is caught there and a suspiciously-named one is caught here.*
  [Fact]
  // ⚠⚠ ALSO CITES `AC-ATT-0027` — *"The contract does not disclose leave **type** if `OD-ATT-0013`(3) rules
  // it sensitive — the contract may not be laxer than the module's own HTTP surface."* **The `LeaveType`
  // ban below is that clause**, and the comment beside it already gave the criterion's own reason before
  // any criterion was attached to it.
  //
  // ⚠⚠⚠ AND THE DISTINCTION FROM `AC-ATT-0045`, WHICH IS NOT CITED HERE AND MUST NOT BE READ AS COVERED:
  // this asserts the CONTRACT carries no leave type AT ALL. `0045` is about the module's own HTTP reads —
  // *leave type is not returned on any read a caller holds only `Attendance.Leave.View` for* — which is
  // PER-ROW REDACTION in `AttendanceReadService`, decided at runtime by a permission. **A structural absence
  // and a conditional redaction are different mechanisms; this test reaches only the first.**
  [Trait("Decision", "DEC-ATT-0002")]
  [Trait("Criterion", "AC-ATT-0023")]
  [Trait("Criterion", "AC-ATT-0027")]
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
  // ---- ⚠⚠⚠ CITES `AC-ATT-0026`, AND THIS IS THE CRITERION'S PROPER HOME — I CITED IT ELSEWHERE FIRST.
  //
  // *"Payroll consumes the contract **without an assembly reference** to any Attendance implementation
  // project — asserted by an architecture test, not by inspection."*
  //
  // **I cited that on `PayrollArchitectureTests.Payroll_assemblies_reach_other_modules_only_through_contracts`
  // before finding this, and recorded a bound there — *reads EMITTED metadata, so an unused
  // `ProjectReference` is pruned before the test sees it.* THAT BOUND IS FALSE FOR THE CRITERION, because
  // THIS test closes it: it asserts the DECLARED dependencies too, and its plant record shows an unused
  // `SSAS.Attendance.Domain` reference passing the emitted half and reddening the declared one.**
  //
  // ⚠ THE TWO ARE COMPLEMENTARY AND NEITHER SUBSUMES THE OTHER, WHICH IS WHY BOTH KEEP THE CITATION:
  // **this one is EXHAUSTIVE (`Assert.Equal(["SSAS.Attendance.Contracts"])`, not a list of assemblies
  // somebody thought of) and covers DECLARED + EMITTED, but only `SSAS.Payroll.Application`.** The other
  // covers **all four Payroll assemblies** and is emitted-only. *A reference from `SSAS.Payroll.API` would
  // be caught only there; an unused one from Application only here.*
  [Fact]
  [Trait("Decision", "DEC-ATT-0002")]
  [Trait("Criterion", "AC-ATT-0026")]
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

    // ================================================================================================
    // ⚠⚠ TWO CLAUSES, ONE INSTRUMENT, AND ONLY THE POSITIVE ONE WAS SAFE (272)
    // ================================================================================================
    //
    // The exact set above carries a POSITIVE clause — *references the contracts* — and a NEGATIVE one —
    // *and no implementation*. **The exact form protects only the first.**
    //
    // `GetReferencedAssemblies()` reads EMITTED metadata, and the compiler omits a reference no type is
    // taken from. ⚠ **PRUNING REMOVES THE FORBIDDEN REFERENCE, NOT THE SANCTIONED ONE.** Add an unused
    // `SSAS.Attendance.Domain` ProjectReference and the emitted set is still exactly
    // `["SSAS.Attendance.Contracts"]` — this passes, unchanged, over a project that can now reach the
    // implementation. The reference is merged, the friction is gone, and the guard says nothing until the
    // first use.
    //
    // I first filed this test as FAIL-SAFE on the reasoning that pruning would empty the set and redden it.
    // That was wrong in the direction that matters, and the declared reading is what closes it.
    //
    // ---- ⚠ BOTH ASSERTIONS STAY. THEY FAIL ON DIFFERENT DAYS.
    //
    // DECLARED catches the capability the moment the `.csproj` merges. EMITTED catches consumption,
    // INCLUDING through a transitive path no `.csproj` of ours names. **Neither subsumes the other**, and
    // two assertions that both mention "references" are exactly what a later tidy-up deletes one of.
    //
    // ---- ⚠⚠ AND THE ORDER OF THE TWO IS LOAD-BEARING FOR THE PLANT, WHICH IS INVISIBLE FROM HERE.
    //
    // The plant that justified this conversion added an UNUSED `SSAS.Attendance.Domain` ProjectReference to
    // `SSAS.Payroll.Application.csproj`. xUnit stops at the first failure, and because the EMITTED
    // assertion runs ABOVE this one it was observed to PASS while the declared assertion below reddened —
    // both halves demonstrated in a single run.
    //
    // **Swap the two and the coverage is identical but that demonstration is gone**: the declared failure
    // would mask the emitted one, and nobody could show the emitted check is blind to an unused reference
    // without a second run. A tidy-up that reorders these loses nothing at runtime and destroys the
    // evidence, which is why the order is written down rather than left to look arbitrary.
    var declared = DeclaredDependencies.Of(payrollApplication)
      .Where(name => name.StartsWith("SSAS.Attendance", StringComparison.Ordinal))
      .ToArray();

    // The predicate matches an implementation reference where one legitimately exists, so the exact set
    // below is not satisfied by a parse that recognises no Attendance project at all.
    Assert.Contains(
      DeclaredDependencies.Of("SSAS.Attendance.Infrastructure"),
      name => name.StartsWith("SSAS.Attendance.Domain", StringComparison.Ordinal));

    Assert.Equal(["SSAS.Attendance.Contracts"], declared);
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
  //
  // ⚠ CITES `AC-ATT-0030` — *"The read scope cannot be constructed outside its factory — private
  // constructor, internal factory, asserted by an architecture test."* **All three clauses, including the
  // last, which names the instrument rather than the property.** The ban below passes over an empty set,
  // which is the compliant state here rather than a vacuity: the walk is rooted in a NAMED TYPE, so it
  // cannot silently return nothing — delete the type and this stops compiling. The factory assertions are
  // positive and fail if it is removed or widened.
  [Fact]
  [Trait("Decision", "DEC-ATT-0008")]
  [Trait("Criterion", "AC-ATT-0030")]
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
  // THE SELF-SERVICE PERMISSIONS ARE EXACTLY TWO (AC-ATT-0032, OD-ATT-0013).
  // ================================================================================================
  //
  // ---- WHAT THIS GUARD USED TO ASSERT, AND WHY IT DID NOT SIMPLY GET DELETED (T-089).
  //
  // It asserted that NO permission name contained `Own`, `Self` or `Mine`. That absence was correct for as
  // long as self-service was deferred — first because the identity-to-employee mapping did not exist, then,
  // after T-082 built it, because FP-015's endpoint did not.
  //
  // **FP-015 has now landed, so the absence is false. The question the guard answers is not.** It was never
  // really *"is there a self permission"*; it was *"has a self-service surface appeared without a person
  // deciding its shape"*. **An absence check answers that only until the first one is added, and then it is
  // deleted and answers nothing ever again.** An exact inventory keeps answering it: the THIRD one still
  // needs a person, exactly as the first two did.
  //
  // `DEC-L-072`, applied on the day the door opened — the same move T-088 made for the placement directory's
  // injection set, and for the same reason.
  //
  // ---- AND WHY THE INVENTORY IS TWO RATHER THAN ONE.
  //
  // The administrative plane permissions records and leave separately (`Attendance.Records.View` versus
  // `Attendance.Leave.View`) because a timesheet and a leave history disclose different things. **A single
  // `Attendance.ViewOwn` would be a WIDENING wearing the costume of a simplification:** granting one's own
  // attendance would silently grant one's own leave.
  //
  // **Pinning both names is what makes a later collapse into one a visible decision** rather than a
  // simplification nobody reviewed. `TS-SS-0013` asserts the runtime half — that neither substitutes for
  // the other at an endpoint — and this asserts the catalog half.
  //
  // **The durable handle is `AC-ATT-0032`, not this method name.** `AC-` identifiers survive refactors and
  // are what the traceability matrix and specification prose cite; a test name changes whenever the thing
  // it describes does, which is exactly what happened here, twice.
  [Fact]
  [Trait("Criterion", "AC-ATT-0032")]
  [Trait("Decision", "OD-ATT-0013")]
  public void The_self_service_permissions_are_exactly_the_two_that_were_decided()
  {
    var constants = typeof(SSAS.Attendance.Application.Permissions.AttendancePermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral)
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();

    string[] expectedSelfService = ["Attendance.Leave.ViewOwn", "Attendance.Records.ViewOwn"];

    var actualSelfService = constants
      .Where(name =>
        name.Contains("Own", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Self", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Mine", StringComparison.OrdinalIgnoreCase))
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(expectedSelfService, actualSelfService);

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
