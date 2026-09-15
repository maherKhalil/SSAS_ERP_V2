# item 170 — are any of the 61 reached without a compile-time reference?

**Measurement only. Nothing built, nothing removed.** Item 165's `[Obsolete]` probe sees only compile-time
references, and its blind spot was named as *"reflection, DI resolution and serialization"*.

## ⚠ The answer: none of the 61, and the blind spot is narrower than it was stated

**Every answer below is MEASURED.** Nothing here is inferred from reasoning about what "should" be true.

### 1. Reflection — enumerated completely, not name-matched

**`src/` and `tools/` contain exactly four dynamic member-access sites:**

| site | what it reaches |
|---|---|
| `PersistenceDbContext:19` — `GetMethod(nameof(ConfigureTenantFilter), …)` | a **private method on a concrete class** |
| `PersistenceDbContext:40` — `.Invoke(this, [modelBuilder])` | the same method |
| `LocalizationEndpointRouteBuilderExtensions:359/361` — `JsonElement.TryGetProperty`/`GetProperty` | **JSON document fields, not .NET reflection at all** |

`InvokeMember`, `CreateDelegate`, `GetMembers` and `GetInterface` appear **zero** times.

**None targets an interface member, so none of the 61 is invoked reflectively in production.** This is an
enumeration of the *mechanism*, which is complete — not a search for member names, which could not be.

### 2. ⚠ DI resolution cannot hide a member consumer — a category correction

**DI resolves TYPES; it never calls interface members.** A container constructs `Foo` for `IFoo`, and
every subsequent `foo.Bar()` is an ordinary compile-time reference that the probe sees.

**So "DI-resolved" is a blind spot for TYPES and not for MEMBERS.** The dispatch asked for validation
against *"a member KNOWN to be DI-resolved"* — **no such member exists to validate against, because the
category is empty.** That is a correction to the question rather than a missing control, and it is why the
control below is a reflection case instead.

### 3. Serialization — the string-literal search answers nothing, and does not need to

Searching every string literal and `nameof` in `src/`+`tools/` for the 61 names returns 9 members. **They
are name collisions, not references.** `TenantId` matched **2,236 string literals** — EF column names in
configurations and migrations. `CompanyId` matched 1,188, `EmployeeId` 392.

**A name search cannot distinguish `ICurrentUser.CompanyId` from a database column called `CompanyId`.**
The same failure as item 167's 26 → 8: proximity and naming are not semantics. It does not matter here,
because §1 enumerates the mechanism rather than the names.

## The known positive, and what it validates

**`ConfigureTenantFilter`**, found by the search at `PersistenceDbContext.cs:19` via `nameof`. It is
genuinely reached by reflection — `GetMethod` then `.Invoke`.

⚠ **It validates THE SEARCH, not item 165's probe.** It is a private method on a concrete class, not one
of the 61, so it cannot show what the probe would miss. What it shows is that a reflectively-invoked
member is visible to this search at all — without it, §1's zero would be indistinguishable from a search
that looks in the wrong place.

## One member IS reached reflectively — by a test, and 165 already recorded it

**`IModuleEnablementDescriptor.ModuleKey`.** Its own declaration comments say *"An architecture test
enumerates `IModuleEnablementDescriptor` implementations…"*. Item 165 recorded **6 test consumers** for it
and **0 production consumers**, so its classification is unchanged — but it is the one real instance of the
class, and the reacher is a test rather than the product.

## ⚠ And a defect in my own item 165 result file, fixed here

**`item-165-interface-members-without-consumers.md` recorded the COUNT and the GROUPS but not the
MEMBERS.** Recovering them for this item needed a probe log that survived only in `%TEMP%` — one cleanup
away from the measurement being unreproducible.

**That is the T-201 failure mode in my own artefact**, written four items after I recorded the lesson. The
61 are listed below so they stop being recoverable-only-from-a-temp-file.

## What remains outside this item

- **`tests/` was not searched for dynamic access**, only `src/` and `tools/`. A member reached
  reflectively by a test is not a production consumer, which is the population 165 measured.
- **Only two of the 61 were hand-verified in item 165**, and that is unchanged. This item establishes that
  the *mechanism* for a hidden consumer is absent; it does not re-verify each member individually.
- Source generators and compile-time weaving would be invisible to both instruments. None is configured in
  this repository, but I did not enumerate build-time tooling.

## The 61, recorded

| interface | member |
|---|---|
| `IAttendanceRecordRepository` | `GetForEmployeePeriodAsync` |
| `IBranchSessionService` | `ResolveForSessionAsync` |
| `IBranchSessionService` | `SelectActiveBranchAsync` |
| `IBranchTopologyLease` | `TenantId` |
| `ICurrentBranch` | `BranchId` |
| `ICurrentUser` | `CompanyId` |
| `ICurrentUser` | `Email` |
| `ICurrentUser` | `Roles` |
| `ICurrentUser` | `SessionId` |
| `ICurrentUser` | `TokenId` |
| `ICurrentUser` | `UserName` |
| `IDepartmentReadService` | `GetManagerEmployeeIdAsync` |
| `IDepartmentRepository` | `AppendDepartmentAssignmentAsync` |
| `IEmployeeCompensationRepository` | `GetHistoryAsync` |
| `ILeaveBalanceRepository` | `GetByIdAsync` |
| `ILocalizationTextResolver` | `ResolveAsync` |
| `ILocalizationTextResolver` | `ResolveGroupAsync` |
| `IModuleEnablementDescriptor` | `ModuleKey` |
| `IOneOffPaymentRepository` | `GetByIdAsync` |
| `IPayrollLine` | `EmployeeId` |
| `IPayrollLine` | `PayElementId` |
| `IPayrollLine` | `Sequence` |
| `IPayrollScopeResolver` | `RequirePermission` |
| `ITenantBranchService` | `CreateAsync` |
| `ITenantBranchService` | `DeactivateAsync` |
| `ITenantBranchService` | `GetAsync` |
| `ITenantBranchService` | `GetOnboardingStateAsync` |
| `ITenantBranchService` | `ListAsync` |
| `ITenantBranchService` | `UpdateAsync` |
| `ITenantCutoverCopyService` | `CopyAsync` |
| `ITenantCutoverFreezeService` | `BeginAsync` |
| `ITenantCutoverFreezeService` | `ReleaseFreezeAsync` |
| `ITenantCutoverOperationStore` | `FindActiveForTenantAsync` |
| `ITenantCutoverOrchestrator` | `ResumeAsync` |
| `ITenantCutoverOrchestrator` | `StartAsync` |
| `ITenantCutoverRoutingFlipService` | `FlipAsync` |
| `ITenantDatabaseBackupExecutor` | `ExecuteAsync` |
| `ITenantDatabaseBackupReadRepository` | `FindLatestSuccessfulRunAsync` |
| `ITenantDatabaseBackupReadRepository` | `ListRecentRunsAsync` |
| `ITenantDatabaseConnectivityHealthService` | `CheckAsync` |
| `ITenantDatabaseConnectivityHealthService` | `SweepAsync` |
| `ITenantDatabaseMigrationOrchestrator` | `MigrateAsync` |
| `ITenantDatabaseMigrationOrchestrator` | `RunAsync` |
| `ITenantDatabaseRecoveryActivationGate` | `EvaluateAsync` |
| `ITenantDatabaseRestoreVerificationRunStore` | `MarkSucceededAsync` |
| `ITenantDatabaseRestoreVerificationRunStore` | `RecordCleanupAsync` |
| `ITenantDatabaseSchemaHealthService` | `CheckAsync` |
| `ITenantDatabaseSchemaHealthService` | `SweepAsync` |
| `ITenantEntitlementCache` | `InvalidatePlan` |
| `ITenantEntitlementCache` | `InvalidateTenant` |
| `ITenantLocalizationSettingsRepository` | `GetForUpdateAsync` |
| `ITenantMigrationRunner` | `GetAppliedMigrationsAsync` |
| `ITenantMigrationRunner` | `GetPendingMigrationsAsync` |
| `ITenantMigrationRunner` | `MigrateAsync` |
| `ITenantRepository` | `GetByNormalizedCodeAsync` |
| `ITenantRoutingCache` | `Count` |
| `IUnitOfWork` | `BeginTransactionAsync` |
| `IUnitOfWork` | `SaveChangesAsync` |
| `IUserCompanyAccessRepository` | `AddAsync` |
| `IUserCompanyAccessRepository` | `GetCompanyIdsAsync` |
| `IUserCompanyAccessRepository` | `RemoveAsync` |
