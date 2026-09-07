using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Employees;
using SSAS.HR.Domain.Employees;

namespace SSAS.Architecture.Tests;

// THE EMPLOYEE BOUNDARIES (FP-006C3, ADR-014 r1.1, ADR-023, ADR-024, ADR-025).
//
// Employee is the first business record owned along all three dimensions, so most of what makes it correct
// is a CLASSIFICATION rather than a behaviour — and a classification is invisible at the call site and
// silent when it regresses. These pin the ones with a real failure mode.
public sealed class EmployeeArchitectureTests
{
  private static readonly Assembly HrDomainAssembly = typeof(Employee).Assembly;

  private static readonly Assembly HrInfrastructureAssembly =
    typeof(SSAS.HR.Infrastructure.Persistence.HrTenantModelContributor).Assembly;

  // ⚠ APPLICATION WAS MISSING FROM THE DELETE-SURFACE WALK UNTIL T-088, and it is the assembly where a
  // delete would most naturally be written: `IEmployeeRepository` itself lives here, and so would a
  // `DeleteEmployeeCommandHandler`. The walk read Domain and Infrastructure only.
  private static readonly Assembly HrApplicationAssembly = typeof(IEmployeeRepository).Assembly;

  // ---- EMPLOYEE CARRIES ALL THREE OWNERSHIP DIMENSIONS, and is the first production entity to do so.
  [Fact]
  public void Employee_is_tenant_company_and_branch_owned()
  {
    var interfaces = typeof(Employee).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(ICompanyOwnedEntity), interfaces);
    Assert.Contains(typeof(IBranchOwnedEntity), interfaces);
    Assert.Contains(typeof(IAuditableEntity), interfaces);

    Assert.Equal(typeof(AggregateRoot<Guid>), typeof(Employee).BaseType);
  }

  // ================================================================================================
  // TS-EMP-0113 — THE CLASSIFICATION THAT IS EASIEST TO GET WRONG
  // ================================================================================================
  //
  // A transfer record spans a branch boundary and belongs to neither side. If it were branch-owned it would
  // enter the branch write boundary, where the trusted context during a transfer is the SOURCE while the
  // record's subject is the DESTINATION — making transfer unrepresentable.
  [Fact]
  public void The_branch_assignment_is_tenant_and_company_owned_but_never_branch_owned()
  {
    var interfaces = typeof(EmployeeBranchAssignment).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(ICompanyOwnedEntity), interfaces);
    Assert.Contains(typeof(IAppendOnlyEntity), interfaces);

    Assert.DoesNotContain(typeof(IBranchOwnedEntity), interfaces);
  }

  // ---- AND NEITHER BRANCH COLUMN IS NAMED `BranchId`.
  //
  // The naming is defence, not style: a property called BranchId is what a future convention or interface
  // implementation would latch onto to reclassify the type as branch-owned.
  [Fact]
  public void The_branch_assignment_has_no_property_named_branch_id()
  {
    var properties = typeof(EmployeeBranchAssignment)
      .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .Select(property => property.Name)
      .ToArray();

    Assert.DoesNotContain("BranchId", properties);
    Assert.Contains(nameof(EmployeeBranchAssignment.SourceBranchId), properties);
    Assert.Contains(nameof(EmployeeBranchAssignment.DestinationBranchId), properties);
  }

  // ---- THE HISTORY HAS NO CONCURRENCY STATE, NO MODIFICATION METADATA AND NO CLOSING DATE.
  //
  // Each absence is a decision: a record that is never updated has nothing to protect, nothing to stamp, and
  // closing an interval would mean updating the previous row.
  [Fact]
  public void The_branch_assignment_has_no_rowversion_modification_or_closing_date()
  {
    var type = typeof(EmployeeBranchAssignment);

    // ⚠ A LOOKUP WHOSE MISS IS THE ASSERTED VALUE (258). `GetProperty` returns null for a name that does
    // not exist AND for a name that is misspelt, so these could not tell "the assignment has no row
    // version" from "I typed it wrong". The three with a witness are now compile-checked against it.
    Assert.Null(type.GetProperty(nameof(SSAS.HR.Domain.Employees.Employee.RowVersion)));
    Assert.Null(type.GetProperty(nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.ModifiedUtc)));
    Assert.Null(type.GetProperty(nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.ModifiedBy)));

    // ⚠⚠ `EffectiveToUtc` STAYS A STRING AND THAT IS NOT AN OVERSIGHT: closing an interval is the thing
    // this model exists to prevent, so no type in the product declares it and NO WITNESS CAN EXIST. The
    // residual is real — a wrong word here is detected by nothing.
    Assert.Null(type.GetProperty("EffectiveToUtc"));
    Assert.NotNull(type.GetProperty(nameof(EmployeeBranchAssignment.EffectiveFromUtc)));
  }

  // ================================================================================================
  // WHAT V1 DELIBERATELY DOES NOT HAVE
  // ================================================================================================

  // BR-HR-0005, BR-HR-0006 and BR-HR-0007 are retained as binding and deferred (DEC-EMP-0017/0018/0031).
  // No placeholder column stands in for them, because a placeholder is how a deferral quietly becomes a
  // design.
  //
  // ---- UPDATED BY FP-007 PHASE 1, THEN BY PHASE 3, AND ONLY WHERE THE APPROVED SCOPE CHANGED IT.
  //
  // Phase 1 superseded the clause asserting that no Department TYPE existed anywhere in HR: the aggregate
  // exists. Phase 3 supersedes the clause asserting that Employee has no DEPARTMENT PROPERTY: BR-HR-0005 is
  // no longer deferred, and `Employee.DepartmentId` is its implementation rather than a placeholder.
  //
  // ---- AND BY FP-008 PHASE 1, ON EXACTLY THE SAME TERMS AS FP-007 PHASE 1.
  //
  // The clause asserting that no Position TYPE existed anywhere in HR is superseded: the three aggregates
  // exist. It is REPLACED, not deleted — `Position` is now asserted to exist, and no position type may hold
  // an Employee reference (`DEC-POS-0002`), which is the property that clause was really protecting.
  //
  // ---- AND BY FP-008 PHASE 3, THE LAST OF THE THREE SUPERSESSIONS.
  //
  // The clause asserting that Employee has NO POSITION SURFACE is superseded: `BR-HR-0006` is no longer
  // deferred, and `Employee.PositionId` is its implementation rather than a placeholder. Phase 1 kept that
  // clause deliberately and said exactly when it would end — "arrives in Phase 3" — so this is the
  // scheduled retirement of a guard rather than the removal of an inconvenient one.
  //
  // It is REPLACED, not deleted, on the identical terms the department clause was: the position members are
  // now asserted to exist with the exact shape Phase 3 approved, so this test still fails if Employee grows
  // a position surface nobody agreed to — a `PositionCode`, a `PositionTitle`, or a navigation that would
  // let an employee read walk into a position and around its scope.
  //
  // MANAGER IS NOT SUPERSEDED AT ALL. `OD-POS-006` deferred `ReportsToPositionId`, so `BR-HR-0007`'s
  // remainder transfers onward unchanged and its clause is kept in full.
  //
  // The department clause is REPLACED rather than deleted — the property is now asserted to exist with the
  // exact shape Phase 3 approved, so this test still fails if Employee grows a department surface nobody
  // agreed to.
  [Fact]
  // ⚠ CITED BY B18, body-confirmed: no position or manager property on the aggregate -- the deferral asserted structurally.
  [Trait("Criterion", "AC-EMP-0045")]
  public void Employee_has_no_position_or_manager_and_exactly_one_department_property()
  {
    var properties = typeof(Employee)
      .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .Select(property => property.Name)
      .ToArray();

    // MANAGER IS NOT SUPERSEDED AT ALL — `OD-POS-006` deferred `ReportsToPositionId`, so `BR-HR-0007`'s
    // remainder transfers onward unchanged and its clause is kept in full and alone.
    Assert.DoesNotContain(properties, name =>
      name.Contains("Manager", StringComparison.OrdinalIgnoreCase));

    // ---- EXACTLY TWO POSITION MEMBERS, NAMED, exactly as for the department: the current position and the
    // append-only log, and nothing else.
    Assert.Equal(
      ["PositionAssignments", "PositionId"],
      properties
        .Where(name => name.Contains("Position", StringComparison.OrdinalIgnoreCase))
        .OrderBy(name => name, StringComparer.Ordinal));

    // ---- AND ITS SETTER IS PRIVATE, on the same terms as DepartmentId's (ADR-026 d.6, DEC-POS-0010).
    //
    // A public setter would be an ordinary-assignment path around `ChangePosition`, which is precisely what
    // `BRULE-POS-0017` forbids: a position changes only through the sanctioned channel that appends history.
    var positionId = typeof(Employee).GetProperty(nameof(Employee.PositionId));

    Assert.NotNull(positionId);
    Assert.Equal(typeof(Guid), positionId!.PropertyType);
    Assert.False(positionId.SetMethod!.IsPublic);

    // ---- EXACTLY TWO DEPARTMENT MEMBERS, NAMED. The current department and the append-only log, and
    // nothing else — no DepartmentCode, no DepartmentName, no Department navigation. An Employee that could
    // walk to its Department would be a read that bypasses the department's own scope.
    Assert.Equal(
      ["DepartmentAssignments", "DepartmentId"],
      properties
        .Where(name => name.Contains("Department", StringComparison.OrdinalIgnoreCase))
        .OrderBy(name => name, StringComparer.Ordinal));

    // ---- AND ITS SETTER IS PRIVATE, unlike BranchId's.
    //
    // BranchId is public-set because IBranchOwnedEntity requires it for stamping. DepartmentId has no such
    // interface, so a public setter would be an ordinary-assignment path around ChangeDepartment — which is
    // precisely what §27's protected-mutation rule forbids.
    var departmentId = typeof(Employee).GetProperty(nameof(Employee.DepartmentId));

    Assert.NotNull(departmentId);
    Assert.Equal(typeof(Guid), departmentId!.PropertyType);
    Assert.False(departmentId.SetMethod!.IsPublic);

    // ================================================================================================
    // "POSITION IS DEFERRED WHOLE" IS RETIRED. FP-008 PHASE 1 IS THE PACKAGE THAT ENDS IT.
    // ================================================================================================
    //
    // This clause read `Assert.DoesNotContain(HrDomainAssembly.GetTypes(), type => type.Name.Contains
    // ("Position", ...))` and asserted `DEC-DEP-0020`: FP-007 introduced no Position type, table, column or
    // foreign key, and `BR-HR-0006` transferred onward untouched. It did its job — it is why nobody slipped
    // a `PositionId` placeholder into Employee for the convenience of a later phase.
    //
    // FP-008 Phase 1 introduces `Position`, `JobGrade`, `SalaryGrade` and `EmployeePositionAssignment`, so
    // the assembly-wide clause is now false BY DESIGN. It is replaced rather than deleted, because what it
    // was really protecting is still worth protecting and is still true today: **Employee has no position
    // surface.**
    //
    // The first assertion in this test already states that for Employee's own properties. What follows is
    // the other half — that the position types exist as their own aggregates and reach Employee through
    // nothing.
    //
    // ---- THIS TEST CHANGES AGAIN IN PHASE 3, AND THAT IS THE SEQUENCE, NOT AN OVERSIGHT.
    //
    // Phase 3 gives Employee a `PositionId` and a `PositionAssignments` collection. At that point the
    // "no property containing Position" assertion above must become the exact-membership assertion the
    // department half already uses — two named members and nothing else — so an Employee that grew a
    // position surface nobody agreed to still fails here.
    Assert.Contains(HrDomainAssembly.GetTypes(), type => type.Name == "Position");

    // NO POSITION TYPE REFERENCES EMPLOYEE (DEC-POS-0002). `Employee.PositionId -> Position` plus any
    // `Position.* -> Employee` key is a cycle in the foreign-key graph, and `TenantCutoverCopyPlan.Order`
    // returns `CutoverCopyOrderUndecidable` on a cycle — Shared→Dedicated cutover would stop working for
    // every tenant. The history record is the one legitimate holder of an `EmployeeId`, and it points
    // outward from both principals rather than being pointed at.
    var positionTypesReferencingEmployee = HrDomainAssembly.GetTypes()
      .Where(type => type.Namespace == "SSAS.HR.Domain.Positions")
      .Where(type => type.Name != "EmployeePositionAssignment")
      .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => $"{type.Name}.{property.Name}"))
      .Where(name => name.Contains("Employee", StringComparison.OrdinalIgnoreCase))
      .ToArray();

    Assert.Empty(positionTypesReferencingEmployee);

    // AND NO EMPLOYEE REPORTING LINE. `BR-HR-0007` presumes an employee-to-manager relationship that no
    // authority defines; a department has a manager, an employee does not. `DepartmentManager` is the
    // department's, which is why it is excluded by name rather than by the loose pattern above.
    var reportingLines = HrDomainAssembly.GetTypes()
      .Where(type => type.Name.Contains("Manager", StringComparison.OrdinalIgnoreCase) &&
        type.Name != nameof(SSAS.HR.Domain.Departments.DepartmentManager))
      .Select(type => type.Name)
      .ToArray();

    Assert.True(reportingLines.Length == 0,
      $"HR domain types name a manager relationship: {string.Join(", ", reportingLines)}. A DEPARTMENT has " +
      "a manager; an employee does not, and `BR-HR-0007` presumes an employee-to-manager link that no " +
      "authority defines. If the type above belongs to the department, exclude it by name here as " +
      "`DepartmentManager` already is; if it is an employee reporting line, it needs a decision before a test.");
  }

  // Automatic per-company numbering is deferred (DEC-EMP-0011): the number is a required INPUT, so a future
  // generator is additive rather than a redesign.
  [Fact]
  // ⚠ CITED BY B18, body-confirmed: BOTH clauses: no generator type exists, AND the create parameter is a required non-optional
  // string, which is "supplied by the caller at creation".
  [Trait("Criterion", "AC-EMP-0046")]
  public void No_employee_number_generator_exists()
  {
    var types = HrDomainAssembly.GetTypes().Concat(HrInfrastructureAssembly.GetTypes())
      .Select(type => type.Name)
      .ToArray();

    // The floor and the matcher control, for the reason given on `No_rehire_operation_exists` (B23): an
    // empty `types` satisfies this ban identically to compliance, and a comparison that can never match is
    // indistinguishable from one that is satisfied.
    Assert.NotEmpty(types);
    Assert.Contains(types, name => name.Contains("Employee", StringComparison.OrdinalIgnoreCase));

    Assert.DoesNotContain(types, name =>
      name.Contains("Sequence", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("NumberGenerator", StringComparison.OrdinalIgnoreCase));

    // The create command REQUIRES the number: it is not nullable and not optional.
    var parameter = typeof(CreateEmployeeCommand).GetConstructors().Single()
      .GetParameters()
      .Single(candidate => candidate.Name == "EmployeeNumber");

    Assert.Equal(typeof(string), parameter.ParameterType);
    Assert.False(parameter.IsOptional);
  }

  // Rehire is deferred: there is no transition out of Terminated and no operation named for one.
  [Fact]
  // ⚠ CITED BY B18, body-confirmed: ⚠ PARTIAL. `AC-EMP-0047` bans route, command, handler, permission AND table for rehire, employee
  // documents, import AND export. This asserts the REHIRE clause only. The documents, import and export
  // clauses are pinned by nothing here -- recorded rather than implied (B18).
  [Trait("Criterion", "AC-EMP-0047")]
  public void No_rehire_operation_exists()
  {
    var methodNames = HrDomainAssembly.GetTypes()
      .SelectMany(type => type.GetMethods())
      .Select(method => method.Name)
      .ToArray();

    // ⚠ THE FLOOR: a ban over an EMPTY sequence passes and looks identical to a ban that found nothing.
    // `GetTypes()` returning nothing — a renamed assembly, a load failure — would satisfy the assertion
    // below in exactly the same way as compliance does (B23).
    Assert.NotEmpty(methodNames);

    // ⚠⚠ AND THE MATCHER CONTROL: `Contains(..., OrdinalIgnoreCase)` over THIS population is proven able to
    // fire, against a lifecycle method that really exists. Without it, a ban whose comparison never matches
    // anything is indistinguishable from a ban that is satisfied.
    Assert.Contains(methodNames, name => name.Contains("Terminate", StringComparison.OrdinalIgnoreCase));

    Assert.DoesNotContain(methodNames, name => name.Contains("Rehire", StringComparison.OrdinalIgnoreCase));
  }

  // ⚠⚠⚠ WHAT THE TWO CONTROLS ABOVE DO **NOT** CLOSE, SAID PLAINLY SO NOBODY CREDITS THEM WITH IT.
  //
  // They prove the population is real and the matching MECHANISM fires. **They cannot prove the WORD is
  // right.** If the concept ships as `Reinstate` or `Reactivate`, `"Rehire"` matches nothing and this test
  // is green over a feature it was written to forbid.
  //
  // **B23's preferred remedy — make a wrong name a compile error via `nameof` — IS UNAVAILABLE HERE BY
  // CONSTRUCTION: you cannot `nameof` a symbol whose absence is the claim.** That limit is inherent to
  // absence-of-concept assertions, not an omission in this one, and it is the reason the criterion also
  // needs the behavioural half (no transition out of `Terminated`) rather than resting on a name scan.
  //
  // ⚠ MEASURED, NOT ASSUMED: with BOTH controls above in place, changing the banned literal to `"Rehiree"`
  // left this test GREEN. The controls close an empty population and a dead comparison; they do not close
  // a wrong word, and the plant says so rather than leaving a reader to infer it.
  //
  // ================================================================================================
  // ⚠⚠⚠ THE B23 POPULATION, CLASSIFIED BY REFLECTION — AS AT 2026-09-02, AND HOW TO RE-DERIVE IT
  // ================================================================================================
  //
  // **246 distinct literals banned by negative assertions across `tests/`. 193 NAME A REAL SYMBOL; 53 NAME
  // NOTHING** — measured against 84,176 type, member and assembly names reflected from every non-test
  // assembly in this project's output directory.
  //
  // ⚠ A DATED AS-BUILT RECORD, ASSERTED BY NOTHING. To re-derive: extract literals from `tests/**/*.cs`
  // with `Assert\.(DoesNotContain|Empty|Null|False)\([^;]*?"([^"]+)"` — **the `[^;]` bound is load-bearing;
  // without it the match runs past the end of a method and captures the NEXT method's `[Trait]` attributes,
  // which inflated a first run from 246 to 313.** Then reflect over every `*.dll` beside the tests,
  // **excluding test assemblies BY PROPERTY (references xunit), never by a `*.Tests.dll` name pattern**,
  // collecting type names, full names, member names including non-public, and the ASSEMBLY NAME itself.
  //
  // ⚠⚠ IT ERRS TOWARDS KIND 2. Its reach is assemblies present in this project's OUTPUT, not every type
  // that exists — `IHttpContextAccessor` is a real framework type filed as *names nothing* because its
  // assembly is not there. **Over-reporting the un-closable kind is the safe direction, and a reader needs
  // to know WHICH WAY it errs, not merely that it errs.**
  //
  // ---- ⚠⚠⚠ AND *NAMES NOTHING* IS NOT ONE POPULATION. IT IS AT LEAST THREE, WANTING OPPOSITE TREATMENT.
  //
  //   ABSENCE-OF-CONCEPT BANS — `Rehire`, `Punch`, `Unfreeze`, `MapDelete`, `UpdateName`, `SetCode`,
  //   `NextDue`, `DeleteDepartmentAsync`. **The real B23 population.** `nameof` impossible; close
  //   behaviourally.
  //
  //   ⚠ DELIBERATELY-ABSENT PROBE VALUES — `hunter2`, `spoofed`, `Nothing.MapsThis`,
  //   `Platform.Unknown.Thing`, `probe.widgets.view`, `primary.example`, `secret.internal`, `1098765432`.
  //   **MATCHING NOTHING IS THE ASSERTION. These must NOT be "fixed".** And the risk runs the other way:
  //   the day `Platform.Unknown.Thing` becomes a real permission, the test does not fail — it keeps passing
  //   while the proposition silently changes from *unknown permissions are rejected* to *ungranted
  //   permissions are rejected*. **True for a different reason, and no plant can detect that.**
  //
  //   SOURCE-TEXT BANS — `Math.Clamp`, `context.Add`, `copyService.CopyAsync`, `OPENQUERY`, `NOCHECK`.
  //   The literal is a code fragment matched against file CONTENTS, not a symbol name.
  //
  // ---- WHY NO GUARD WAS LANDED FOR THIS.
  //
  // The classifier computes *does this literal name a symbol*. The guard the probe values need is *does
  // this literal STILL name nothing* — and separating those from absence-of-concept bans requires INTENT,
  // which is not derivable. **A hand-maintained list of probe values is exactly the term drift `278`
  // closed: a new probe added without a list entry would be unguarded, silently, forever.** So the
  // instrument was run, recorded here, and deleted rather than landed over a population it cannot derive.

  // ================================================================================================
  // WHAT THE CONTRACTS REFUSE TO EXPRESS
  // ================================================================================================

  // ---- THE UPDATE COMMAND CANNOT EXPRESS A RELOCATION, A COMPANY MOVE OR A LIFECYCLE CHANGE.
  //
  // Omission at the contract level is the first of two protections; the shared write boundaries are the
  // second. This pins the first, which is the one a reviewer cannot see from the boundary code.
  [Fact]
  // ⚠ CITED BY B18 pass 12, body-confirmed: clause 1 -- `tenantId`, `companyId`, `branchId`, `employeeNumber` and `status` are ABSENT from the
  // update contract. Clause 2, that updating a `Terminated` employee is refused, is asserted by
  // `A_terminated_employee_cannot_have_its_profile_updated`.
  [Trait("Criterion", "AC-EMP-0007")]
  public void The_update_command_carries_no_ownership_or_status()
  {
    var parameters = typeof(UpdateEmployeeProfileCommand).GetConstructors().Single()
      .GetParameters()
      .Select(parameter => parameter.Name!)
      .ToArray();

    Assert.DoesNotContain(parameters, name =>
      name.Contains("Tenant", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Company", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Branch", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
      // ⚠ `nameof` RATHER THAN A LITERAL, AND IT IS THE ONLY CLAUSE HERE THAT CAN BE (B23). The other four
      // are SUBSTRING bans — *no parameter MENTIONS Tenant* — and `nameof` yields an exact string, so
      // converting them would narrow each ban from *mentions* to *equals* and let `TenantId` through.
      // This clause is an exact-equality ban on a member that really exists on the create command, so the
      // reference is compile-checked and a rename cannot leave it silently matching nothing.
      name.Equals(nameof(CreateEmployeeCommand.EmployeeNumber), StringComparison.Ordinal));

    // And it carries the concurrency token, so an update cannot be applied to state the caller never saw.
    Assert.Contains("ExpectedRowVersion", parameters);
  }

  // The CREATE command carries no ownership either: tenant, company and branch all come from the trusted
  // execution context, so the question never reaches the boundary.
  [Fact]
  // ⚠ CITED BY B18 pass 12, body-confirmed: the CONTRACT clause of three criteria at once -- the create command carries no Tenant, Company or
  // Branch parameter, so none can be "accepted from the route, body, header, claim or query string".
  // ⚠ PARTIAL for `0002`: its second clause -- a post-creation `TenantId` change is rejected -- is
  // asserted for Company and TenantUser but NOT for Employee (searched S3, S5, S6).
  [Trait("Criterion", "AC-EMP-0002")]
  [Trait("Criterion", "AC-EMP-0003")]
  [Trait("Criterion", "AC-EMP-0004")]
  public void The_create_command_carries_no_ownership()
  {
    var parameters = typeof(CreateEmployeeCommand).GetConstructors().Single()
      .GetParameters()
      .Select(parameter => parameter.Name!)
      .ToArray();

    Assert.DoesNotContain(parameters, name =>
      name.Contains("Tenant", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Company", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Branch", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Status", StringComparison.OrdinalIgnoreCase));
  }

  // ---- ONLY THE TRANSFER COMMAND NAMES A BRANCH, and it names the DESTINATION only: the source is the
  // record's current branch, never something a request may assert.
  [Fact]
  // ⚠ CITED BY B18, body-confirmed: only the transfer command carries a branch and only the DESTINATION -- so the channel cannot be
  // opened from a create or update DTO, which is the criterion's subject.
  [Trait("Criterion", "AC-EMP-0043")]
  public void Only_the_transfer_command_names_a_branch_and_only_the_destination()
  {
    var parameters = typeof(TransferEmployeeCommand).GetConstructors().Single()
      .GetParameters()
      .Select(parameter => parameter.Name!)
      .ToArray();

    Assert.Contains("DestinationBranchId", parameters);

    // NO SOURCE BRANCH. `InactiveSourceRecovery` names a MODE, not a branch: the source is always the
    // record's current branch, so the DESTINATION is the only branch the command may name.
    Assert.Equal(
      ["DestinationBranchId"],
      parameters.Where(name => name.Contains("Branch", StringComparison.OrdinalIgnoreCase)).ToArray());

    // No other Employee command mentions a branch at all.
    foreach (var command in new[]
      { typeof(CreateEmployeeCommand), typeof(UpdateEmployeeProfileCommand), typeof(TerminateEmployeeCommand) })
    {
      Assert.DoesNotContain(
        command.GetConstructors().Single().GetParameters(),
        parameter => parameter.Name?.Contains("Branch", StringComparison.OrdinalIgnoreCase) == true);
    }
  }

  // ==================================================================================================
  // ⚠⚠⚠ THIS WAS ONE TEST CALLED `No_employee_delete_operation_is_exposed` AND IT CHECKED SPELLINGS (T-088)
  // ==================================================================================================
  //
  // The name claimed a MECHANISM — no operation deletes an employee. The predicate was a VOCABULARY:
  // `Contains("Delete")` over `IEmployeeRepository` alone, and an anchored `^Delete(Employee)?(Async)?$`
  // over Domain and Infrastructure. **`SoftDeleteEmployeeAsync` and `DeleteEmployeeRecordAsync` were
  // invisible to both** — not on that one interface, and not matching four literal spellings. The
  // assembly walk also omitted `SSAS.HR.Application` entirely, which is where a delete handler would live.
  //
  // ⚠ ADDING `SoftDelete` TO THE REGEX WOULD HAVE BEEN THE SAME DEFECT ONE COMMIT LATER. A wider
  // vocabulary is still a vocabulary. So the claim is split in two, and each half is named for what it
  // actually inspects: one reads the MECHANISM and is name-independent, the other reads NAMES and says so.
  //
  // ⚠⚠ THE PRODUCT WAS SEARCHED BEFORE EITHER PREDICATE WAS TOUCHED (T-088), because a widened guard that
  // reddens on real code is a finding and not a test edit. `Set<Employee>()` is used for `AddAsync` and
  // `AsNoTracking` reads only; the sole EF removal anywhere in HR targets `DepartmentManager`; and
  // `SoftDelete`, `IsDeleted`, `DeletedUtc`, `DeletedBy` and `MarkDeleted` appear NOWHERE under `src`.
  // The old guard's green was a true green — this closes a hole, it does not close a breach.
  //
  // ⚠⚠⚠ REACH PROBE, BOTH COLOURS MEASURED RATHER THAN ARGUED. `SoftDeleteEmployeeAsync` was planted in
  // `EmployeeRepository`, containing `context.Set<Employee>().Remove(employee)` — one needle for each half.
  //
  //   OLD PREDICATE, run verbatim against the plant  -> GREEN. Blind to both.
  //   The two tests below                            -> RED. Both.
  //
  // The old body was re-run as a temporary fixture rather than reasoned about, because *the old guard would
  // have missed this* is exactly the kind of claim that is easy to assert and easy to get wrong. It missed
  // the NAME because `^Delete(Employee)?(Async)?$` is anchored, and it missed the CALL because a method body
  // is invisible to a reflection walk over method names. The exact-list failure names the offender:
  // `Actual: ["DepartmentManager (DepartmentRepository.cs)", "Employee (EmployeeRepository.cs)"]`.

  // ---- THE MECHANISM. WHAT HR ACTUALLY REMOVES FROM THE DATABASE, WHATEVER THE METHOD IS CALLED.
  //
  // An EXACT LIST rather than a ban, because the expected value is non-empty and therefore cannot be
  // satisfied by a collapsed walk: a scan that matched nothing fails this, where a `DoesNotContain` would
  // have passed. The one legitimate removal is named, so the test states the house's real position —
  // *HR deletes exactly one kind of row* — instead of a prohibition with an invisible exception.
  [Fact]
  public void The_only_entity_hr_removes_from_the_database_is_the_department_manager()
  {
    var files = HrSourceFiles();

    // ⚠ 110, DERIVED FROM A NAMED COLLAPSE (T-099). Actual 134, measured 2026-09-07. The event this
    // discriminates is A PROJECT OR FOLDER DROPPING OUT OF THE HR TREE — the smallest HR project is well
    // over twenty files, so losing one lands below 110 while ordinary growth never approaches it.
    // ⚠⚠ IT WAS 40, AND 40 WAS CHOSEN BECAUSE IT PASSED. Ninety-four files could have left this walk in
    // silence, and the exact list below depends on the walk being whole rather than merely non-empty.
    Assert.True(files.Length >= 110,
      $"only {files.Length} HR source files were walked; 134 were measured at T-099, so a project or " +
      "folder has left the tree. The removal list below would report an empty set for a reason that has " +
      "nothing to do with the product.");

    var removals = new List<string>();
    var hardDeletes = new List<string>();

    foreach (var file in files)
    {
      var text = WithoutComments(File.ReadAllText(file));
      var name = Path.GetFileName(file);

      foreach (Match match in Regex.Matches(text, @"Set<(\w+)>\(\)\s*\.\s*Remove(?:Range)?\s*\("))
      {
        removals.Add($"{match.Groups[1].Value} ({name})");
      }

      // `ExecuteDelete` and a manual `Deleted` state bypass the entity API entirely, so they would not
      // appear as a `Set<T>().Remove` at all. Neither exists in HR today; if one arrives it is reported
      // separately, because it is a different mechanism and not a different spelling.
      foreach (var bypass in new[] { "ExecuteDelete", "EntityState.Deleted" })
      {
        if (text.Contains(bypass, StringComparison.Ordinal))
        {
          hardDeletes.Add($"{bypass} in {name}");
        }
      }
    }

    Assert.True(hardDeletes.Count == 0,
      $"HR bypasses the entity API to delete rows: {string.Join("; ", hardDeletes)}. These are invisible " +
      "to the removal list below because they never call Remove, and an employee deleted this way would " +
      "leave no trace in the model. Route the deletion through the entity API or do not delete.");

    Assert.Equal(
      ["DepartmentManager (DepartmentRepository.cs)"],
      removals.OrderBy(value => value, StringComparer.Ordinal).ToArray());
  }

  // ---- THE SURFACE. A NAME CHECK, AND THE NAME OF THIS TEST SAYS SO.
  //
  // ⚠ THIS CANNOT BE COMPLETE AND MUST NOT BE READ AS IF IT WERE. A method that removes an employee while
  // being called `RetireAsync` or `Finalise` passes here, and only the mechanism test above would see it.
  // What this adds is the case the mechanism test cannot reach: a delete EXPOSED on a contract but not yet
  // implemented, which is latent capability rather than behaviour — the same declared-versus-emitted split
  // recorded in `DeclaredDependencies`.
  [Fact]
  public void No_hr_type_declares_a_method_named_for_deleting_an_employee()
  {
    var types = new[] { HrDomainAssembly, HrApplicationAssembly, HrInfrastructureAssembly }
      .SelectMany(assembly => assembly.GetTypes())
      .ToArray();

    // TWO LAYERS, TWO FLOORS (T-263): a healthy type list whose method walk collapses is a different
    // failure and must say which one happened.
    // ⚠ 400, DERIVED (T-099). Actual 548, measured 2026-09-07. Discriminates ONE OF THE THREE ASSEMBLIES
    // FAILING TO LOAD OR BEING DROPPED FROM THE ARRAY — the smallest of the three contributes well over a
    // hundred types. It was 80, which is under a sixth of the real value and names no event at all.
    // ⚠⚠⚠ EACH ASSEMBLY ASSERTED SEPARATELY, BECAUSE THE FLOOR CANNOT SEE ONE LEAVE THE ARRAY (T-107).
    //
    // MEASURED 2026-09-07: Domain 76 · Application 264 · Infrastructure 208. **Deleting one line from the
    // array above takes 548 to 472, and a floor of 400 stays GREEN.** The tier most likely to go is the
    // smallest — and the smallest is `SSAS.HR.Domain`, which is where `Employee` itself lives. A guard
    // about employee deletion would then cover every assembly except the employee's own.
    //
    // This is the `size versus kind` shape: a whole tier leaves and the count stays above the floor.
    foreach (var assembly in new[] { HrDomainAssembly, HrApplicationAssembly, HrInfrastructureAssembly })
    {
      Assert.True(
        types.Any(type => type.Assembly == assembly),
        $"type count across the HR assemblies: {types.Length}, and not one from " +
        $"`{assembly.GetName().Name}`. It has been dropped from the array above — a one-line edit the " +
        "count floor below cannot see, because the two remaining assemblies clear it on their own.");
    }

    Assert.True(types.Length >= 400,
      $"type count across the three HR assemblies: {types.Length}; 548 measured at T-099, " +
      "so an assembly has failed to load or been dropped from the array above.");

    // ==================================================================================================
    // ⚠⚠⚠ `NonPublic` IS HERE BECAUSE A PRIVATE METHOD DELETES JUST AS THOROUGHLY (T-104)
    // ==================================================================================================
    //
    // This read `Public | Instance | Static | DeclaredOnly` and nothing else. **A `private async Task
    // SoftDeleteEmployeeAsync(...)` was invisible to it**, and the test's name says `exposes`, which no
    // reader decodes as *public-only*. The mechanism half of this pair reads SOURCE and ignores visibility
    // entirely, so the two halves disagreed about what counts as HR's surface — and visibility is a
    // compilation detail, not a safety property.
    //
    // ---- ⚠⚠ THE COST WAS MEASURED BEFORE THE CHANGE, NOT PREDICTED. 2026-09-07, three HR assemblies:
    //
    //     methods, PUBLIC only        2482
    //     methods, WITH NonPublic     3516      (adds 1034)
    //     of the added: COMPILER-GENERATED 842 · hand-written 192   <- both from the probe; 192 is 1034-842,
    //                                                                a JOIN, and it is the figure that later
    //                                                                produced the false 2,674 below
    //     deletion-vocabulary hits    0 public  ->  12 with NonPublic  ->  ***ALL TWELVE GENERATED***
    //
    // The twelve are `<>z__ReadOnlyArray`1.ICollection<T>.Remove`, `…IList.RemoveAt` and their siblings —
    // the compiler's own collection-expression types implementing `IList`. ***SO ADDING `NonPublic`
    // WITHOUT A FILTER WOULD HAVE PRODUCED TWELVE IMMEDIATE FALSE REDS ON THE COMPILER'S NAMING***, which
    // is the `Remove` lesson and the `$(Configuration)` lesson in one: a guard that fires on correct code
    // gets deleted rather than fixed.
    //
    // With the filter the population is **2,859** and the vocabulary hit count stays at ZERO. That is the
    // whole gain, and it is measured rather than argued.
    //
    // ⚠ I FIRST WROTE 2,674 HERE, DERIVED AS 2482 + 192 FROM THE PROBE ABOVE, AND IT WAS WRONG. The probe's
    // "generated" predicate included `[CompilerGenerated]` on the MEMBER; the filter that shipped tests the
    // declaring TYPE. Different predicates classify different members, so the arithmetic did not carry
    // across. **Every figure in this comment except that one was measured; that one was computed, and it
    // was the only one that was false.**
    //
    // ⚠ SEARCHED FIRST, AS EVERY WIDENING HERE IS: **no hand-written private or internal method in the
    // three HR assemblies carries a deletion verb today.** All twelve occupants of the blind spot were
    // the compiler's. This closes a hole; it does not close a breach.
    //
    // ---- ⚠⚠⚠ REACH PROBE WITH THE ADVERSARIAL SUBJECT, BOTH COLOURS MEASURED.
    //
    // A **private** `PurgeEmployeeRecord` was planted on `EmployeeRepository` — private being precisely the
    // case the old flags could not see:
    //
    //     OLD flags (no `NonPublic`)  -> GREEN. Blind to it, exactly as T-101 predicted.
    //     THESE flags                 -> RED: "an HR type declares a method named for deletion:
    //                                   EmployeeRepository.PurgeEmployeeRecord".
    //
    // The old colour was MEASURED by removing `NonPublic` and re-running against the same plant, not
    // reasoned about.
    var methods = types
      .SelectMany(type => type.GetMethods(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.DeclaredOnly))
      .Where(method => !IsCompilerGenerated(method))
      .ToArray();

    // ⚠⚠⚠ 1800, DERIVED (T-099). Actual 2482, measured 2026-09-07. Discriminates the collapse this
    // message has always CLAIMED to catch: a base-class, partial-class or interface-extraction refactor
    // moving a whole layer of declarations out from under `DeclaredOnly`.
    //
    // ⚠ IT WAS 200 — AGAINST 2482. **It would not have fired until 92% of HR's declared methods had
    // disappeared**, so the message said "the METHOD layer has collapsed" while the floor could only
    // notice the layer being annihilated. I wrote that floor in T-088, in the hour I was auditing other
    // guards for exactly this, which is the whole of what `documentation-is-diagnosis-not-prevention` says.
    // ⚠⚠⚠ THE POPULATION'S SHAPE CHANGED, AND A COUNT FLOOR CANNOT SEE THAT (T-106).
    //
    // T-104 added `NonPublic`, so this population now contains a tier it did not contain when the floor was
    // derived. ***THE NEW COLLAPSE IS SOMEBODY REMOVING `NonPublic` AGAIN*** — a one-word edit that returns
    // the guard to public-only and takes 2,859 back to 2,482. **A floor of 1800 does not fire on that**,
    // and neither would any floor sized for the layer-collapse this test was built for: the two events
    // differ by roughly five hundred and by over a thousand respectively — 2,859 and 2,333 are the two
    // MEASURED endpoints; the difference between them is a join and is not restated as a figure.
    //
    // So the shape is asserted by shape, not by magnitude — the same lesson as the `tests` area in
    // `ConfigurationInvarianceTests`: **a floor is a claim about SIZE and this failure is about KIND.**
    Assert.True(
      methods.Any(method => !method.IsPublic),
      $"declared method count: {methods.Length}, and not one is non-public. `NonPublic` has been removed " +
      "from the binding flags above, so this guard has silently returned to inspecting only what HR " +
      "EXPOSES — and a private delete method deletes just as thoroughly. The count floor below cannot see " +
      "this: dropping the non-public tier takes the count from 2,859 to 2,333 — both MEASURED, by planting exactly that edit — and 2,333 clears 1800 comfortably.");

    // ⚠ THE FLOOR DID NOT MOVE AND THE ACTUAL DID (T-104). 2482 public at T-099; 2859 after `NonPublic`
    // plus the compiler-generated filter. **1800 still discriminates the same collapse** — a refactor
    // moving declarations onto a base class, out from under `DeclaredOnly` — and the population grew by
    // 8%, which does not change what the number is sized against. Recorded rather than silently retained.
    Assert.True(methods.Length >= 1800,
      $"declared method count: {methods.Length}, across {types.Length} HR types; 2859 measured at T-104. " +
      "The METHOD layer has collapsed rather than the type layer — most likely a refactor moving " +
      "declarations onto a base class, where `DeclaredOnly` stops seeing them — and the vocabulary below " +
      "now reads almost nothing.");

    // The vocabulary is deliberately broad and deliberately UNANCHORED — the old `^Delete(Employee)?…$`
    // could not see `SoftDeleteEmployeeAsync`.
    //
    // ⚠⚠⚠ `Remove` WAS EXCLUDED HERE ON A FALSE PREMISE AND IS NOW INCLUDED (T-089). The comment said it
    // *"matched dozens of legitimate methods"*. **MEASURED: across all three HR assemblies, 2,482 declared
    // public methods, and ZERO contain `Remove`.** The belief came from `RemoveManagerAsync`, which is
    // `private static` in `SSAS.HR.API` — a different assembly AND a visibility this walk does not read.
    //
    // So the exclusion cost real coverage for nothing: `RemoveEmployeeAsync` passed this guard. The one
    // genuine remove in HR is `DepartmentRepository.ClearManagerAsync`, which is not named for removal at
    // all and never matched. ⚠ The `Department` exclusion below stays, because a future
    // `RemoveManagerAsync` on the repository would be legitimate — an association, not an employee — and a
    // vocabulary guard that fires on legitimate code gets deleted rather than fixed.
    var named = methods
      .Where(method => Regex.IsMatch(
        method.Name, @"(Delete|Purge|Erase|Expunge|Destroy|Remove)", RegexOptions.CultureInvariant))
      .Where(method => method.DeclaringType?.Name.Contains("Department", StringComparison.Ordinal) != true)
      .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
      .OrderBy(value => value, StringComparer.Ordinal)
      .ToArray();

    Assert.True(named.Length == 0,
      $"an HR type declares a method named for deletion: {string.Join(", ", named)}. Employees are " +
      "terminated, never deleted — see `TerminateEmployeeCommand`. If this method deletes something that " +
      "is not an employee, the exclusion belongs here by name and with its reason.");
  }

  // ⚠ THE FILTER THAT MAKES `NonPublic` SAFE (T-104). Checked on the METHOD and on its DECLARING TYPE,
  // because the twelve real occupants were named innocently — `Remove`, `RemoveAt` — on types the compiler
  // named `<>z__ReadOnlyArray\`1`. **The offending name was the type's, not the method's**, so a filter
  // reading only the method name would have let all twelve through.
  // ⚠⚠⚠ THE DECLARING TYPE ONLY, AND THE FIRST VERSION OF THIS FILTER WAS WRONG IN A WAY THE FLOOR CAUGHT.
  //
  // It also tested `method.IsDefined(CompilerGeneratedAttribute)`. ***PUBLIC AUTO-PROPERTY ACCESSORS CARRY
  // THAT ATTRIBUTE***, so the filter deleted every `get_`/`set_` in three assemblies: the population fell
  // from 3,516 to 493 and the METHOD FLOOR FIRED IMMEDIATELY. The floor was sized for a base-class refactor
  // and it caught an over-broad filter instead — a collapse it was not designed for, detected because the
  // number it guards is the number the filter changed.
  //
  // The intent was never "members the compiler marked"; it was **types the compiler synthesised** —
  // `<>z__ReadOnlyArray\`1` and its siblings. Testing the declaring type gets exactly those and leaves
  // hand-written members of every visibility in the population.
  private static bool IsCompilerGenerated(MethodInfo method) =>
    method.DeclaringType is { } declaring &&
    (declaring.Name.Contains('<', StringComparison.Ordinal) ||
      declaring.IsDefined(
        typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false));

  // HR source, excluding build output. `git ls-files` cannot see `bin`/`obj` by construction, but this
  // walk can, so the exclusion is explicit.
  private static string[] HrSourceFiles() =>
    [.. Directory
      .EnumerateFiles(
        Path.Combine(RepositoryRootDirectory(), "src", "Modules", "HR"), "*.cs", SearchOption.AllDirectories)
      .Where(path =>
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .OrderBy(path => path, StringComparer.Ordinal)];

  // Line comments only; this repository writes its prose as `//` and the block form does not appear in
  // `src`. Stripped because a comment naming `ExecuteDelete` would otherwise read as a call to it.
  private static string WithoutComments(string text) =>
    string.Join('\n', text.Split('\n').Select(line => line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : line));

  // ================================================================================================
  // LAYERING
  // ================================================================================================

  // ---- HR DOMAIN AND APPLICATION STAY FREE OF PERSISTENCE, and Platform never depends on HR.
  //
  // ⚠⚠ THIS ONE METHOD CARRIES THREE DISTINCT CLAIMS OVER TWO POPULATIONS, AND THAT IS RECORDED RATHER
  // THAN SILENTLY ACCEPTED (272). The bans are: EF Core (a PACKAGE reference) and `SSAS.Platform` (a
  // PROJECT reference) over the two HR assemblies, then `SSAS.HR` over three PLATFORM assemblies. Three
  // predicates, two element types, two loops, one test name.
  //
  // A failure therefore reports "HR layers are not clean" for any of three unrelated regressions, and the
  // message does not say which. **Splitting it was considered and ruled out inside this sweep**: a split
  // changes what a failure reports, changes test identity and names, and moves the suite count — none of
  // which this item is about. It carries no `[Trait("Criterion", …)]`, so a future split would be a plain
  // refactor rather than a decision about which half inherits a citation.
  //
  // ---- DECLARED AND EMITTED, BECAUSE THEY FAIL ON DIFFERENT DAYS (272).
  //
  // `GetReferencedAssemblies()` reads emitted metadata and the compiler omits a reference no type is taken
  // from, so any of these three could be declared in a `.csproj` and pass until the first use. Declared
  // catches the capability at merge time; emitted catches consumption, including through a transitive path
  // no `.csproj` of ours names.
  //
  // ⚠ THREE PREDICATES, THREE CONTROLS — one exercise each, because a control proves a PREDICATE can fire
  // and these three share nothing. A single control over one of them would leave the other two bans
  // holding over a parse that might recognise neither.
  [Fact]
  public void Hr_layers_stay_clean_and_platform_never_depends_on_hr()
  {
    var host = DeclaredDependencies.Of("SSAS.Host.API");

    Assert.Contains(
      DeclaredDependencies.Of("SSAS.BuildingBlocks.Infrastructure"),
      name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    Assert.Contains(host, name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));
    Assert.Contains(host, name => name.StartsWith("SSAS.HR", StringComparison.Ordinal));

    foreach (var assembly in new[] { HrDomainAssembly, typeof(IEmployeeRepository).Assembly })
    {
      var declared = DeclaredDependencies.Of(assembly);

      Assert.DoesNotContain(
        assembly.GetReferencedAssemblies(),
        reference => reference.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true);
      Assert.DoesNotContain(
        declared, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));

      // And never on Platform: HR reaches the tenant plane only through the shared contract set.
      Assert.DoesNotContain(
        assembly.GetReferencedAssemblies(),
        reference => reference.Name?.StartsWith("SSAS.Platform", StringComparison.Ordinal) == true);
      Assert.DoesNotContain(
        declared, name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));
    }

    // Platform, in both directions.
    foreach (var platform in new[]
      {
        typeof(SSAS.Platform.Domain.Companies.Company).Assembly,
        typeof(SSAS.Platform.Application.Branches.IBranchWriteAuthorizer).Assembly,
        typeof(SSAS.Platform.Infrastructure.Persistence.PlatformDbContext).Assembly
      })
    {
      Assert.DoesNotContain(
        platform.GetReferencedAssemblies(),
        reference => reference.Name?.StartsWith("SSAS.HR", StringComparison.Ordinal) == true);

      // The third claim, and the one a merged-but-unused reference falsifies first: Platform depending on
      // HR inverts the layering the whole module boundary rests on, and the `.csproj` edit is where that
      // inversion actually happens.
      Assert.DoesNotContain(
        DeclaredDependencies.Of(platform),
        name => name.StartsWith("SSAS.HR", StringComparison.Ordinal));
    }
  }

  // ---- HR MAPS ITS OWN ENTITIES, through the contract neither side owns.
  [Fact]
  public void Hr_contributes_its_entities_through_the_shared_contributor_contract()
  {
    var contributor = typeof(SSAS.HR.Infrastructure.Persistence.HrTenantModelContributor);

    Assert.Contains(
      typeof(SSAS.BuildingBlocks.Infrastructure.Persistence.ITenantModelContributor),
      contributor.GetInterfaces());

    // The configurations are HR's own, not Platform's.
    Assert.Equal(HrInfrastructureAssembly, typeof(SSAS.HR.Infrastructure.Persistence.EmployeeConfiguration).Assembly);
  }

  // ---- NO CROSS-DATABASE FOREIGN KEY, AND NO MIGRATION MAY INTRODUCE ONE.
  //
  // Employee's principals — Company and Branch — both live in the TENANT catalog, so its foreign keys are
  // intra-catalog and legal. What must never appear is a platform-stream constraint pointing at either.
  [Fact]
  public void No_platform_migration_targets_a_tenant_owned_principal()
  {
    var platformMigrations = ReadMigrations("Persistence", "Migrations");

    Assert.DoesNotContain("principalTable: \"Companies\"", platformMigrations, StringComparison.Ordinal);
    Assert.DoesNotContain("principalTable: \"Branches\"", platformMigrations, StringComparison.Ordinal);
    Assert.DoesNotContain("principalTable: \"Employees\"", platformMigrations, StringComparison.Ordinal);

    // The tenant stream DOES carry them, intra-catalog, which is the whole point of the Company move.
    var tenantMigrations = ReadMigrations("Persistence", "TenantErp", "Migrations");
    Assert.Contains("principalTable: \"Companies\"", tenantMigrations, StringComparison.Ordinal);
    Assert.Contains("principalTable: \"Branches\"", tenantMigrations, StringComparison.Ordinal);
  }

  // ---- EVERY PERSISTED EMPLOYEE STRING IS nvarchar. A varchar column would silently narrow names and
  // identifiers that the domain permits to be Unicode.
  [Fact]
  public void Every_persisted_employee_string_is_nvarchar()
  {
    // ONE FILE, NOT A CONCATENATION (TEST-001). Slicing from a marker to the end of every joined migration
    // made the examined text depend on `Directory.EnumerateFiles` order, which is alphabetical on NTFS and
    // arbitrary on ext4 — the same order dependence that broke the index test on Linux. Reading the single
    // migration that creates the table is deterministic everywhere and is what this assertion was ever
    // about.
    var migration = ReadMigrationFile("AddHrEmployee");

    var employeeSection = migration[migration.IndexOf("name: \"Employees\"", StringComparison.Ordinal)..];

    Assert.DoesNotContain("type: \"varchar", employeeSection, StringComparison.Ordinal);
    Assert.DoesNotContain("type: \"text\"", employeeSection, StringComparison.Ordinal);
    Assert.Contains("nvarchar(64)", employeeSection, StringComparison.Ordinal);
    Assert.Contains("nvarchar(200)", employeeSection, StringComparison.Ordinal);
  }

  // ---- EMPLOYEE NUMBER UNIQUENESS IS COMPANY-WIDE AND EXCLUDES THE BRANCH.
  //
  // BR-HR-0001 scopes it to the company and ADR-023 forbids BranchId participating. Getting this wrong would
  // be invisible until two branches of one company disagreed about who holds a number.
  //
  // ---- ASSERTED FROM THE MODEL, NOT FROM CONCATENATED MIGRATION SOURCE (TEST-001).
  //
  // This previously joined every migration file, found the first occurrence of the index name, and sliced
  // forward to the next `"unique: true"`. Two things made that unsafe, and Linux exposed both:
  //
  //   * The index name appears TWICE — in the migration and in the model snapshot.
  //   * `Directory.EnumerateFiles` returns alphabetical order on NTFS and DIRECTORY order on ext4, so which
  //     of the two came first depended on the filesystem.
  //
  // When the snapshot sorted first the slice began there, found no `"unique: true"` (snapshots write
  // `.IsUnique()`), and ran on through unrelated content until it hit that text in another file — sweeping
  // up a `BranchId` that had nothing to do with this index.
  //
  // The model states the same invariant exactly and cannot be reordered. It is also STRONGER: the old test
  // could only prove a substring was absent, while this pins the precise column set, so an index that
  // gained a fourth column or lost `CompanyId` now fails too.
  [Fact]
  public void The_employee_number_index_is_company_scoped_and_excludes_the_branch()
  {
    var employee = ComposedTenantModel().FindEntityType(typeof(Employee));
    Assert.NotNull(employee);

    var index = employee!.GetIndexes().SingleOrDefault(candidate =>
      candidate.GetDatabaseName() == "UX_Employees_TenantId_CompanyId_NormalizedEmployeeNumber");

    Assert.NotNull(index);

    // Uniqueness is the point of the index: without it the per-company rule is a hint, not a constraint.
    Assert.True(index!.IsUnique);

    // COMPANY-SCOPED, AND DELIBERATELY NOT BRANCH-SCOPED. BR-HR-0001 makes the number unique within the
    // COMPANY, so adding BranchId would let the same number exist twice in one company (BRULE-EMP-0009).
    Assert.Equal(
      ["TenantId", "CompanyId", "NormalizedEmployeeNumber"],
      index.Properties.Select(property => property.Name));

    Assert.DoesNotContain(index.Properties, property => property.Name == nameof(Employee.BranchId));
  }


  // The composed tenant model — Platform's own entities plus HR's contribution, exactly as the Host builds
  // it. A contributor-free model would not contain Employee at all, so the index assertion above would pass
  // by finding nothing.
  private static Microsoft.EntityFrameworkCore.Metadata.IModel ComposedTenantModel()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    using var context = new TenantDbContext(
      options,
      new ModelOnlyUser(),
      new ModelOnlyTenant(),
      new ModelOnlyClock(),
      modelContributors: [new HrTenantModelContributor()]);

    return context.Model;
  }

  private sealed class ModelOnlyUser : SSAS.BuildingBlocks.Application.Abstractions.Identity.ICurrentUser
  {
    public string? UserId => "architecture-tests";

    public string? UserName => "architecture-tests";

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelOnlyTenant : SSAS.BuildingBlocks.Application.Abstractions.Tenancy.ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class ModelOnlyClock : SSAS.BuildingBlocks.Application.Abstractions.Time.IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }

  // ---- ONE NAMED MIGRATION, DETERMINISTICALLY.
  //
  // `Directory.EnumerateFiles` does not promise an order, and the two operating systems disagree about what
  // it gives. Any assertion that slices concatenated migration text is therefore reading different input on
  // different machines. Selecting the single file by name removes the question.
  private static string ReadMigrationFile(string nameFragment)
  {
    var directory = Path.Combine(
      RepositoryRootDirectory(), "src", "Platform", "SSAS.Platform.Infrastructure",
      "Persistence", "TenantErp", "Migrations");

    var matches = Directory
      .EnumerateFiles(directory, "*.cs")
      .Where(file => Path.GetFileName(file).Contains(nameFragment, StringComparison.Ordinal) &&
        !file.EndsWith("Designer.cs", StringComparison.Ordinal))
      .ToArray();

    // Exactly one, or the fragment no longer identifies a single migration and the assertion below would be
    // reading whichever file happened to sort first — the defect this method exists to remove.
    var file = Assert.Single(matches);

    return File.ReadAllText(file);
  }

  private static string RepositoryRootDirectory()
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
  private static string ReadMigrations(params string[] segments)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);

    var path = Path.Combine(
      new[] { directory!.FullName, "src", "Platform", "SSAS.Platform.Infrastructure" }.Concat(segments).ToArray());

    Assert.True(Directory.Exists(path), $"Migration directory not found: {path}");

    return string.Join(
      Environment.NewLine,
      Directory.EnumerateFiles(path, "*.cs").Where(file => !file.EndsWith("Designer.cs", StringComparison.Ordinal))
        .Select(File.ReadAllText));
  }
}
