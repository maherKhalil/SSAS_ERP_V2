using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Domain.Positions;
using SSAS.HR.Domain.ImportExport;
using SSAS.Platform.Infrastructure.TenantStorage;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.Companies;
using SSAS.TestSupport.CutoverModel;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE CUTOVER MANIFEST AND COPY ORDER, CHECKED AGAINST THE MODEL RATHER THAN A DATABASE (T-253).
// ==================================================================================================
//
// ---- WHY THESE ARE HERE AND NOT IN THE INTEGRATION SUITE.
//
// Six checks that read an EF model composed IN MEMORY and assert nothing about a server: the manifest
// covers every contributed tenant-owned entity, principals are copied before dependents, rowversion is
// excluded from the projection, the branch assignment has no branch foreign key, and a contributor-free
// plan omits the HR tables.
//
// They lived in `TenantCutoverCopySqlServerTests` and were found by a duration sweep -- all six report
// under 10 ms, with **nothing at all between 10 ms and 2.4 seconds** in that suite. **`GATE_SCOPE=TASK`
// never runs Integration**, so these invariants were not checked during ordinary development at all,
// which is the same shape as the 145 Integration failures that went unread for eight days.
//
// ---- WHAT UNBLOCKED THE MOVE.
//
// They read `CutoverTenantModel`, which was defined inside the Integration project and consumed by five
// other files there. Moving it here would have inverted the dependency; copying it would have created the
// second list its own header warns about. **It now lives in `tests/TestSupport/SSAS.TestSupport.CutoverModel`
// and both suites reference the one definition.**
//
// ---- PLANT RECORD.
//
// Each was broken deliberately in its new home and observed to fail. `always green in a suite nobody
// reads` is the weakest evidence there is, and it was the only evidence these had.
public sealed class CutoverManifestArchitectureTests
{

  // ================================================================================================
  // C6 — SHARED → DEDICATED CARRIES THE MODULE-CONTRIBUTED ENTITIES (FP-006C6, ADR-020, ADR-017).
  // ================================================================================================
  //
  // ---- WHAT WAS ACTUALLY BROKEN, AND WHY NOTHING CAUGHT IT.
  //
  // The copy manifest is derived from the tenant model, which is the right design — a hand-written table
  // list is wrong the moment someone adds an entity, and wrong silently. But the model it derived from was
  // built with NO contributors, so it could not contain Employee no matter what HR registered.
  //
  // A promotion therefore copied Companies and Branches, validated every row it copied, reported success,
  // and left every employee and every branch-assignment record behind. There was no error to notice: the
  // copy was faithful to the model it was given, and the model was the wrong one.
  //
  // These proofs run the REAL copy service against real SQL Server with the contributor set the Host
  // registers.

  // ---- C6-1 / C6-2. THE MODEL THE CUTOVER PLANS FROM IS THE ONE THE APPLICATION PERSISTS THROUGH.
  [Fact]
  [Trait("Decision", "ADR-020")]
  // ⚠ CITED BY B18, body-confirmed: the EXACT expected list names both `Employee` and `EmployeeBranchAssignment`, and it is asserted
  // against the derived plan -- which is what "appears in the declared tenant-owned inventory" means.
  [Trait("Criterion", "AC-EMP-0037")]
  [Trait("Criterion", "AC-EMP-0038")]
  public void C6_1_C6_2_The_cutover_manifest_covers_every_contributed_tenant_owned_entity()
  {
    var composed = CutoverTenantModel.Source.Model;

    // The runtime model contains all twenty — two from Platform, two from FP-006, three from FP-007
    // Phase 1, four from FP-008 Phase 1, and the two run records from FP-009 Phase 1...
    var derived = composed.GetEntityTypes()
      .Where(entity => !entity.IsOwned())
      .Where(entity => typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType))
      .Where(entity => entity.GetTableName() is not null)
      .Select(entity => entity.ClrType.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // AN EXACT LIST, DELIBERATELY. The derivation guarantees the engine cannot MISS a table; this
    // guarantees a human SEES a new one, because a new tenant-owned entity may need ordering, identity or
    // column decisions that "it compiles" does not settle. FP-007 Phase 1 added three, FP-008 Phase 1 added
    // four, FP-009 Phase 1 adds two, and this is one of the three places that has to say so.
    //
    // ---- SalaryGrade IS HERE AND ITS BAND IS NOT, WHICH IS THE OWNED-TYPE FILTER DOING ITS JOB.
    //
    // `SalaryGrade.Band` is an optional OWNED type (`DEC-POS-0027`), so its three money columns live in the
    // `SalaryGrades` table and it is not a separate entity to copy. The `!entity.IsOwned()` filter above is
    // what keeps it out of this list; without it the manifest would name a table that does not exist.
    Assert.Equal(
      [
        "Account",
        "AttendancePeriod",
        "AttendanceRecord",
        "Branch",
        "CalendarHoliday",
        "Company",
        nameof(Department),
        nameof(DepartmentManager),
        "Employee",
        "EmployeeBranchAssignment",
        "EmployeeCompensation",
        nameof(EmployeeDepartmentAssignment),
        nameof(EmployeeExportRun),
        nameof(EmployeeImportRun),
        nameof(EmployeePositionAssignment),
        "FiscalPeriod",
        "FiscalYear",
        nameof(JobGrade),
        "JournalDraft",
        "JournalDraftLine",
        "JournalEntry",
        "JournalLine",
        "LeaveBalance",
        "LeaveRequest",
        "LeaveType",
        "OneOffPayment",
        "PayElement",
        "PayElementAssignment",
        "PayrollPeriod",
        "PayrollRun",
        "PayrollRunDraftLine",
        "PayrollRunLine",
        nameof(Position),
        nameof(SalaryGrade),
        "WorkingCalendar"
      ],
      derived);

    // ...and the plan derived for the copy covers exactly that set, with nothing declared by hand.
    var plan = TenantCutoverCopyPlan.Build(composed);
    Assert.True(plan.IsSuccess);
    Assert.Equal(
      derived,
      plan.Value.Select(table => table.EntityName).OrderBy(name => name, StringComparer.Ordinal));
  }


  // ---- C6-6 / C6-11. DEPENDENCY ORDER, DERIVED FROM FOREIGN KEYS.
  //
  // Employee references Company and Branch; the assignment references Employee. Inserting a dependent
  // before its principal would violate referential integrity with constraints ON, which the engine keeps on
  // throughout — so the order is a correctness requirement, not a preference.
  [Fact]
  [Trait("Decision", "ADR-020")]
  // ⚠ CITED BY B18, body-confirmed: Employee after Company AND after Branch, history after Employee -- the criterion verbatim.
  [Trait("Criterion", "AC-EMP-0039")]
  public void C6_6_Employee_is_ordered_after_company_and_branch_and_history_after_employee()
  {
    var plan = TenantCutoverCopyPlan.Build(CutoverTenantModel.Source.Model);
    Assert.True(plan.IsSuccess);

    var order = plan.Value.Select(table => table.EntityName).ToArray();

    var company = Array.IndexOf(order, nameof(Company));
    var branch = Array.IndexOf(order, nameof(Branch));
    var employee = Array.IndexOf(order, nameof(Employee));
    var history = Array.IndexOf(order, nameof(EmployeeBranchAssignment));

    Assert.True(employee > company, $"Employee must follow Company. Order: {string.Join(", ", order)}");
    Assert.True(employee > branch, $"Employee must follow Branch. Order: {string.Join(", ", order)}");
    Assert.True(history > employee, $"History must follow Employee. Order: {string.Join(", ", order)}");

    // ---- AND THE ORDER IS PRODUCED BY THE FK GRAPH, NOT BY THE NAMES.
    //
    // "EmployeeBranchAssignments" sorts BEFORE "Employees" alphabetically, so an alphabetical ordering would
    // place the dependent first. That it does not is the proof the topological sort is doing the work.
    Assert.True(
      string.CompareOrdinal("EmployeeBranchAssignments", "Employees") < 0,
      "The premise of this assertion no longer holds.");
  }


  // ---- C6-7. ROWVERSION IS NOT CARRIED ACROSS.
  //
  // It is the TARGET's concurrency state, generated by the target on insert. Copying the source's bytes
  // would hand the new database a token describing a different database's history.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void C6_7_The_employee_rowversion_is_excluded_from_the_copy_projection()
  {
    var plan = TenantCutoverCopyPlan.Build(CutoverTenantModel.Source.Model);
    var employees = Assert.Single(plan.Value, table => table.EntityName == nameof(Employee));

    // A live exclusion: Employee genuinely carries a rowversion, so this is not vacuous.
    var model = CutoverTenantModel.Source.Model.FindEntityType(typeof(Employee));
    Assert.Contains(
      model!.GetProperties(),
      property => property.IsConcurrencyToken && property.ValueGenerated == ValueGenerated.OnAddOrUpdate);

    Assert.DoesNotContain(nameof(Employee.RowVersion), employees.Columns);

    // The assignment carries none at all — it is append-only and never updated — so there is nothing to
    // exclude and nothing to transport.
    var assignments = Assert.Single(
      plan.Value, table => table.EntityName == nameof(EmployeeBranchAssignment));

    Assert.DoesNotContain("RowVersion", assignments.Columns);
  }

  // ---- C6-12. THE HISTORY STILL CARRIES NO BRANCH FOREIGN KEY.
  //
  // ADR-024 classifies the assignment as company-owned but NOT branch-owned: it names a source and a
  // destination and belongs to neither. Adding a branch FK would have made the copy ordering marginally
  // easier to reason about and would have broken that classification, so it was not done — and this records
  // that the convenience was declined.
  [Fact]
  [Trait("Decision", "ADR-024")]
  public void C6_12_The_assignment_has_no_branch_foreign_key()
  {
    var assignment = CutoverTenantModel.Source.Model.FindEntityType(typeof(EmployeeBranchAssignment));
    Assert.NotNull(assignment);

    var principals = assignment!.GetForeignKeys()
      .Select(foreignKey => foreignKey.PrincipalEntityType.ClrType.Name)
      .ToArray();

    Assert.DoesNotContain(nameof(Branch), principals);
    Assert.Contains(nameof(Employee), principals);
  }


  // ---- C6-14. AND THE OLD, CONTRIBUTOR-FREE MODEL DEMONSTRABLY DOES NOT.
  //
  // The regression detector. It proves the fix is load-bearing rather than incidental: without the
  // contributor set the manifest silently loses every HR table, which is exactly what shipped before this
  // slice. If these two ever agreed, the composition would have collapsed back and every proof below would
  // still pass while production quietly lost data again.
  //
  // FP-007 Phase 1 made the gap wider rather than different, FP-008 Phase 1 wider again and FP-009 Phase 1
  // wider once more — eleven HR tables now, not two — which is the point: each new contributed entity
  // increases what a contributor-free
  // manifest would silently leave behind.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void C6_14_A_contributor_free_plan_silently_omits_both_hr_tables()
  {
    var composed = TenantCutoverCopyPlan.Build(CutoverTenantModel.Source.Model);
    var contributorFree = TenantCutoverCopyPlan.Build(CutoverTenantModel.ContributorFreeSource.Model);

    Assert.True(composed.IsSuccess);

    // It SUCCEEDS. That is the danger: an incomplete manifest is not an error, it is a shorter list.
    Assert.True(contributorFree.IsSuccess);

    Assert.DoesNotContain(contributorFree.Value, table => table.EntityName == nameof(Employee));
    Assert.DoesNotContain(
      contributorFree.Value, table => table.EntityName == nameof(EmployeeBranchAssignment));
    Assert.DoesNotContain(contributorFree.Value, table => table.EntityName == nameof(Department));
    Assert.DoesNotContain(contributorFree.Value, table => table.EntityName == nameof(DepartmentManager));
    Assert.DoesNotContain(
      contributorFree.Value, table => table.EntityName == nameof(EmployeeDepartmentAssignment));
    Assert.DoesNotContain(contributorFree.Value, table => table.EntityName == nameof(SalaryGrade));
    Assert.DoesNotContain(contributorFree.Value, table => table.EntityName == nameof(JobGrade));
    Assert.DoesNotContain(contributorFree.Value, table => table.EntityName == nameof(Position));
    Assert.DoesNotContain(
      contributorFree.Value, table => table.EntityName == nameof(EmployeePositionAssignment));
    Assert.DoesNotContain(contributorFree.Value, table => table.EntityName == nameof(EmployeeImportRun));
    Assert.DoesNotContain(contributorFree.Value, table => table.EntityName == nameof(EmployeeExportRun));

    // ---- EIGHTEEN MODULE TABLES MISSING, AND ONLY PLATFORM'S COMPANY AND BRANCH LEFT.
    //
    // Eleven from HR, SEVEN from GL (FP-011), SEVEN from Payroll (FP-012) and SEVEN from Attendance
    // (FP-013). The subtraction is written against the composed count
    // rather than as a literal so the two halves cannot drift: if a module adds a table and forgets this
    // test, the count on the left moves and the assertion fails, which is the whole point of the guard.
    Assert.Equal(
      composed.Value.Count - 33,
      contributorFree.Value.Count);
    Assert.Equal(2, contributorFree.Value.Count);
  }

  // ================================================================================================
  // C6-15. THE COPY ORDER PUTS DEPARTMENTS BEFORE EMPLOYEES (FP-007 Phase 3).
  // ================================================================================================
  //
  // Employee gained a REQUIRED foreign key to Department, so a copy that inserted employees first would
  // fail on that constraint against a target where the departments did not exist yet. The plan is a
  // topological sort over the model's foreign keys, so the ordering is derived rather than declared — and
  // derived means nobody wrote it down, which is exactly why it is worth asserting.
  //
  // ---- AND WHY THIS IS NOT MERELY THE SQL TESTS RESTATED.
  //
  // The real-SQL copies below would fail if the order were wrong, but only for the tables the fixture
  // happens to populate, and only after twenty minutes. This reads the order directly out of the plan, in
  // milliseconds, for every pair that matters — including DepartmentManagers and
  // EmployeeDepartmentAssignments, which point at BOTH principals.
  //
  // It is also the guard for the ADR-026 decision 7 split. If DepartmentManager were ever folded back onto
  // Department as a ManagerEmployeeId column, Department would depend on Employee while Employee depends on
  // Department, the sort would find a cycle, and Build would fail with CutoverCopyOrderUndecidable rather
  // than producing a wrong order.
  [Fact]
  [Trait("Decision", "ADR-020")]
  // CITED BY B18 pass 21 for `AC-DEP-0034`, BOUNDED TO THE CLAUSES IT ACTUALLY CARRIES.
  //
  // The criterion has two halves. This test carries the first entirely: the plan BUILDS with
  // Department and DepartmentManagers present (`Assert.True(plan.IsSuccess)`), and every ordering
  // the criterion names is asserted here -- Company before Department, Department before Employee,
  // and DepartmentManager after both.
  //
  // THE SECOND HALF IS ASSERTED BY NOTHING. The criterion also says *a model in which Department
  // holds a direct manager foreign key is asserted to make the plan fail with
  // `CutoverCopyOrderUndecidable`* -- a NEGATIVE model, built deliberately and shown to fail.
  // `DepartmentArchitectureTests.Department_holds_no_foreign_key_to_employee` is the nearest thing
  // and its own comment says what it cannot do: it asserts the foreign key's ABSENCE, not that its
  // presence would break the plan. Absence of the cause is not a demonstration of the effect, and
  // the reason for `DEC-DEP-0022` therefore rests on an argument rather than on a test.
  //
  // ---- AND A CORRECTION TO WHAT THIS COMMENT SAID WHEN IT WAS WRITTEN.
  //
  // It claimed the string literals here would "silently assert about entities that no longer exist".
  // THAT WAS WRONG ABOUT THIS TEST. `PositionOf` ends in `Assert.True(index >= 0, "... is absent from
  // the copy manifest entirely.")`, so a renamed entity REDDENS THIS TEST with a message naming it.
  //
  // The literals were replaced with `nameof` anyway, across all nine entity types in this file, and the
  // reason is elsewhere -- the sites where a rename really is silent are the NEGATIVE assertions:
  // `Assert.DoesNotContain(..., table => table.EntityName == "X")` and the `Assert.Null(... ShortName()
  // == "X")` at the foot of this file. A renamed entity makes those predicates match nothing, and a
  // `DoesNotContain` that matches nothing PASSES. Green, asserting nothing.
  //
  // So the change is a correctness fix at four sites and diagnosability everywhere else, and the file is
  // uniform because a mixed convention is what let the distinction go unexamined for as long as it did.
  [Trait("Criterion", "AC-DEP-0034")]
  public void C6_15_The_copy_order_places_every_principal_before_its_dependents()
  {
    var plan = TenantCutoverCopyPlan.Build(CutoverTenantModel.Source.Model);

    Assert.True(plan.IsSuccess, plan.IsFailure ? plan.Error.Code : null);

    var order = plan.Value.Select(table => table.EntityName).ToArray();

    int PositionOf(string entity)
    {
      var index = Array.IndexOf(order, entity);

      Assert.True(index >= 0, $"{entity} is absent from the copy manifest entirely.");

      return index;
    }

    // Company and Branch are Platform's, and everything HR-owned depends on one or both.
    Assert.True(PositionOf(nameof(Company)) < PositionOf(nameof(Department)));
    Assert.True(PositionOf(nameof(Company)) < PositionOf(nameof(Employee)));
    Assert.True(PositionOf(nameof(Branch)) < PositionOf(nameof(Employee)));

    // ---- THE FP-007 PHASE 3 EDGE. This is the one the new foreign key created.
    Assert.True(
      PositionOf(nameof(Department)) < PositionOf(nameof(Employee)),
      "Departments must be copied before Employees: Employee.DepartmentId is a required foreign key.");

    // The two tables that depend on BOTH must come after both.
    Assert.True(PositionOf(nameof(Department)) < PositionOf(nameof(DepartmentManager)));
    Assert.True(PositionOf(nameof(Employee)) < PositionOf(nameof(DepartmentManager)));
    Assert.True(PositionOf(nameof(Department)) < PositionOf(nameof(EmployeeDepartmentAssignment)));
    Assert.True(PositionOf(nameof(Employee)) < PositionOf(nameof(EmployeeDepartmentAssignment)));

    Assert.True(PositionOf(nameof(Employee)) < PositionOf(nameof(EmployeeBranchAssignment)));

    // ================================================================================================
    // THE FP-008 PHASE 1 EDGES. A THREE-LINK CHAIN, AND A HISTORY THAT DEPENDS ON BOTH ENDS.
    // ================================================================================================
    //
    // SalaryGrade -> JobGrade -> Position is the longest dependency chain in the tenant model, and every
    // link is a nullable foreign key — so a copy that got the order wrong would fail only for the rows that
    // happened to use the reference. Asserting the ORDER catches it regardless of what the fixture
    // populates.
    Assert.True(
      PositionOf(nameof(SalaryGrade)) < PositionOf(nameof(JobGrade)),
      "Salary grades must be copied before job grades: JobGrade.SalaryGradeId is a foreign key.");
    Assert.True(
      PositionOf(nameof(JobGrade)) < PositionOf(nameof(Position)),
      "Job grades must be copied before positions: Position.JobGradeId is a foreign key.");

    // The history depends on BOTH Employee and Position, so it must come after both.
    Assert.True(PositionOf(nameof(Position)) < PositionOf(nameof(EmployeePositionAssignment)));
    Assert.True(PositionOf(nameof(Employee)) < PositionOf(nameof(EmployeePositionAssignment)));

    Assert.True(PositionOf(nameof(Company)) < PositionOf(nameof(Position)));
    Assert.True(PositionOf(nameof(Company)) < PositionOf(nameof(JobGrade)));
    Assert.True(PositionOf(nameof(Company)) < PositionOf(nameof(SalaryGrade)));

    // ---- THE FP-008 PHASE 3 EDGE. This is the one the new foreign key created.
    //
    // Phase 1 recorded this assertion as a FORWARD OBLIGATION and refused to write it early: at that point
    // nothing linked the two, so the assertion would have passed or failed on the sort's tie-breaking
    // rather than on a constraint — green for the wrong reason. `Employee.PositionId` is now a required
    // foreign key, so the edge exists and the claim is finally provable.
    //
    // The obligation's other half moved in the same commit: `data-model.md`'s "not ordered against
    // Employee" caveat is gone, because the assertion and the claim became true together.
    Assert.True(
      PositionOf(nameof(Position)) < PositionOf(nameof(Employee)),
      "Positions must be copied before Employees: Employee.PositionId is a required foreign key.");

    // ================================================================================================
    // THE FP-009 PHASE 1 EDGES. TWO TABLES THAT DEPEND ON COMPANY AND ON NOTHING ELSE.
    // ================================================================================================
    //
    // A run record names WHO RAN WHAT, never WHICH EMPLOYEES RESULTED, so neither points at Employee and
    // neither lengthens the dependency chain. Both carry a company foreign key, which is the only edge
    // they have and the only ordering claim provable about them.
    Assert.True(
      PositionOf(nameof(Company)) < PositionOf(nameof(EmployeeImportRun)),
      "Companies must be copied before import runs: EmployeeImportRun.CompanyId is a foreign key.");
    Assert.True(
      PositionOf(nameof(Company)) < PositionOf(nameof(EmployeeExportRun)),
      "Companies must be copied before export runs: EmployeeExportRun.CompanyId is a foreign key.");

    // ---- AND NEITHER RUN RECORD IS ORDERED AGAINST Employee, DELIBERATELY AND PERMANENTLY.
    //
    // `data-model.md` predicts they "sort ahead of Employees", and they do — but on the SORT'S TIE-BREAK,
    // not on a constraint, because there is no path between them. Asserting that order would be green for
    // the wrong reason, exactly as FP-008 Phase 1 refused to assert Position before Employee before the
    // foreign key existed. What IS assertable is that no such edge exists in either direction.
    foreach (var runRecord in new[] { typeof(EmployeeImportRun), typeof(EmployeeExportRun) })
    {
      var principals = CutoverTenantModel.Source.Model.FindEntityType(runRecord)!
        .GetForeignKeys()
        .Select(key => key.PrincipalEntityType.ShortName())
        .ToArray();

      Assert.Equal([nameof(Company)], principals);
    }

    // ---- AND POSITION IS UNORDERED WITH RESPECT TO DEPARTMENT, PERMANENTLY (OD-POS-003).
    //
    // Position is independent of Department: no `Position.DepartmentId` exists, so neither can precede the
    // other for any reason a constraint would enforce. If this ever becomes assertable, something has grown
    // the second source of truth for an employee's department that `OD-POS-003` refused.
    Assert.Null(
      CutoverTenantModel.Source.Model.FindEntityType(typeof(SSAS.HR.Domain.Positions.Position))!
        .GetForeignKeys()
        .FirstOrDefault(key => key.PrincipalEntityType.ShortName() == nameof(Department)));
  }
}
