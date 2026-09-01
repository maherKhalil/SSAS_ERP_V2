using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Tests.ImportExport;

// THE TWO RUN RECORDS AND THE IMPORT KEY (FP-009 Phase 1, DEC-DOC-0004, DEC-DOC-0006).
//
// Phase 1 delivers a schema and a domain; there is no pipeline yet, so everything here is about what the
// TYPES make possible and impossible. The ownership asymmetry is the substance — see `R4` — and it is
// asserted rather than described, because "the comment says so" is not a guarantee.
public sealed class ImportExportRunDomainTests
{
  private static readonly Guid Tenant = Guid.NewGuid();

  private static readonly Guid Company = Guid.NewGuid();

  private static readonly DateTimeOffset Executed =
    new(2026, 8, 22, 9, 15, 0, TimeSpan.FromHours(3));

  private static ImportKey Key(string value = "batch-2026-08") => ImportKey.Create(value).Value;

  // ================================================================================================
  // I1. THE OWNERSHIP ASYMMETRY, ASSERTED FROM BOTH SIDES.
  // ================================================================================================
  //
  // An import is a company-scope WRITE and its record rides the same boundary as the rows it imports. An
  // export is a READ, and `TenantDbContext` treats a tracked `ICompanyOwnedEntity` as a company-scoped write
  // — demanding a trusted company context and authorizing it. Marking the export record company-owned would
  // therefore make a read-only caller unable to export, or make the audit record a gate on the read.
  //
  // Both halves are asserted, because either one alone would pass if the classification were uniform.
  [Fact]
  [Trait("Decision", "DEC-DOC-0006")]
  public void The_import_run_is_company_owned_and_the_export_run_is_deliberately_not()
  {
    Assert.Contains(typeof(ICompanyOwnedEntity), typeof(EmployeeImportRun).GetInterfaces());
    Assert.DoesNotContain(typeof(ICompanyOwnedEntity), typeof(EmployeeExportRun).GetInterfaces());
  }

  // ---- AND THE EXPORT RECORD STILL CARRIES THE COMPANY, AS DATA.
  //
  // "Which company's employees left" is exactly what an investigator asks, so the column exists. What must
  // not exist is a SETTER reachable by the write boundary: `ICompanyOwnedEntity.CompanyId` is `{ get; set; }`
  // and the boundary stamps through it, so a private setter here is what keeps the column an attribute
  // rather than an ownership discriminator.
  [Fact]
  [Trait("Decision", "DEC-DOC-0006")]
  public void The_export_run_carries_a_company_that_no_write_boundary_can_stamp()
  {
    var companyId = typeof(EmployeeExportRun).GetProperty(nameof(EmployeeExportRun.CompanyId));

    Assert.NotNull(companyId);
    Assert.False(companyId!.SetMethod?.IsPublic ?? false);

    // The import record's is public, by contrast — the boundary stamps it.
    var importCompanyId = typeof(EmployeeImportRun).GetProperty(nameof(EmployeeImportRun.CompanyId));
    Assert.True(importCompanyId!.SetMethod!.IsPublic);
  }

  // ---- BOTH ARE TENANT-OWNED AND APPEND-ONLY, AND NEITHER IS BRANCH-OWNED.
  //
  // Tenant ownership is what puts them in the E3 cutover manifest by construction (`DEC-DEP-0029`). Neither
  // is branch-owned: an import or an export is performed within a company, and a branch is a sibling
  // dimension rather than a narrower one.
  [Theory]
  [InlineData(typeof(EmployeeImportRun))]
  [InlineData(typeof(EmployeeExportRun))]
  [Trait("Decision", "ADR-020")]
  public void Both_run_records_are_tenant_owned_append_only_and_never_branch_owned(Type type)
  {
    var interfaces = type.GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(IAppendOnlyEntity), interfaces);
    Assert.Contains(typeof(IAuditableEntity), interfaces);
    Assert.DoesNotContain(typeof(IBranchOwnedEntity), interfaces);
    Assert.Null(type.GetProperty(nameof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity.BranchId)));
  }

  // ---- NO ROWVERSION, AND NO PUBLIC MUTATOR OF ANY KIND.
  //
  // The `EmployeeBranchAssignment` convention, not the `TenantDatabaseBackupRun` one: a record written once
  // when the outcome is already known has no concurrency state to protect and no lifecycle to advance.
  // `TenantId` and `CompanyId` are excluded because their setters are the OWNERSHIP contract the write
  // boundary stamps through, not a mutation of what the record says happened.
  [Theory]
  [InlineData(typeof(EmployeeImportRun))]
  [InlineData(typeof(EmployeeExportRun))]
  [Trait("Decision", "DEC-DOC-0006")]
  public void Neither_run_record_has_a_rowversion_or_a_public_mutator(Type type)
  {
    Assert.Null(type.GetProperty(nameof(SSAS.HR.Domain.Employees.Employee.RowVersion)));

    var mutable = type.GetProperties()
      .Where(property => property.SetMethod?.IsPublic == true)
      .Select(property => property.Name)
      .Where(name => name is not (nameof(ITenantOwnedEntity.TenantId) or "CompanyId"))
      .ToArray();

    Assert.Empty(mutable);

    // No method offers one either. A record of what happened that can be edited afterwards is not one.
    Assert.Empty(type.GetMethods()
      .Where(method => method.IsPublic && !method.IsStatic && !method.IsSpecialName)
      .Where(method => method.DeclaringType == type)
      .Select(method => method.Name));
  }

  // ================================================================================================
  // I2. ALL-OR-NOTHING IS STRUCTURAL, NOT CONVENTIONAL (OD-DOC-003).
  // ================================================================================================
  //
  // `api-contracts.md` records that an `Applied` run with 998 of 1000 accepted is no longer reachable. There
  // is no factory parameter through which one could be expressed, which is stronger than a validation rule:
  // the shape does not exist rather than being rejected.
  [Fact]
  [Trait("Decision", "OD-DOC-003")]
  public void An_applied_run_accepts_every_row_it_counted()
  {
    var run = EmployeeImportRun.Applied(
      Tenant, Company, Key(), "people.csv", byteCount: 40_960, rowCount: 1_000, Executed, "importer");

    Assert.True(run.IsSuccess);
    Assert.Equal(EmployeeImportOutcome.Applied, run.Value.Outcome);
    Assert.Equal(1_000, run.Value.RowCount);
    Assert.Equal(1_000, run.Value.AcceptedCount);
    Assert.Equal(0, run.Value.RejectedCount);
  }

  [Fact]
  [Trait("Decision", "OD-DOC-003")]
  public void A_refused_run_accepts_nothing_and_still_consumed_its_key()
  {
    var run = EmployeeImportRun.Refused(
      Tenant, Company, Key("replay-me"), "people.csv",
      byteCount: 40_960, rowCount: 1_000, rejectedCount: 2, Executed, "importer");

    Assert.True(run.IsSuccess);
    Assert.Equal(EmployeeImportOutcome.Refused, run.Value.Outcome);
    Assert.Equal(0, run.Value.AcceptedCount);
    Assert.Equal(2, run.Value.RejectedCount);

    // THE KEY IS RECORDED, which is what consuming it means: the unique index is over the normalized column
    // and is filtered on nothing, so this row occupies the key exactly as an applied run would.
    Assert.Equal("REPLAY-ME", run.Value.NormalizedImportKey);
  }

  // ---- A FILE CAN BE REFUSED BEFORE ANY ROW IS READ, AND THAT IS NOT AN INVALID RUN.
  //
  // A bad header or an exceeded cap refuses the submission with nothing to count. Zero rejected rows out of
  // zero rows is the honest record of that, and the arithmetic guard must not mistake it for an error.
  [Fact]
  [Trait("Decision", "OD-DOC-003")]
  public void A_file_refused_before_its_first_row_records_no_rejected_rows()
  {
    var run = EmployeeImportRun.Refused(
      Tenant, Company, Key(), "bad-header.csv",
      byteCount: 96, rowCount: 0, rejectedCount: 0, Executed, "importer");

    Assert.True(run.IsSuccess);
    Assert.Equal(0, run.Value.RowCount);
    Assert.Equal(0, run.Value.RejectedCount);
  }

  // ---- A RUN CANNOT REJECT ROWS IT DID NOT CONTAIN.
  [Fact]
  public void A_run_rejecting_more_rows_than_it_read_is_refused()
  {
    var run = EmployeeImportRun.Refused(
      Tenant, Company, Key(), "people.csv",
      byteCount: 10, rowCount: 3, rejectedCount: 4, Executed, "importer");

    Assert.True(run.IsFailure);
    Assert.Equal(ImportExportErrors.InvalidCounts, run.Error);
  }

  [Theory]
  [InlineData(-1, 0)]
  [InlineData(0, -1)]
  public void A_run_with_a_negative_count_is_refused(int byteCount, int rowCount)
  {
    var run = EmployeeImportRun.Validated(
      Tenant, Company, Key(), "people.csv", byteCount, rowCount, Executed, "importer");

    Assert.True(run.IsFailure);
    Assert.Equal(ImportExportErrors.InvalidCounts, run.Error);
  }

  // ---- A DRY RUN WRITES NOTHING AND SAYS SO.
  [Fact]
  [Trait("Decision", "DEC-DOC-0006")]
  public void A_validated_run_is_a_dry_run_that_accepted_every_row()
  {
    var run = EmployeeImportRun.Validated(
      Tenant, Company, Key(), " people.csv ", byteCount: 512, rowCount: 12, Executed, " importer ");

    Assert.True(run.IsSuccess);
    Assert.Equal(EmployeeImportOutcome.Validated, run.Value.Outcome);
    Assert.Equal(12, run.Value.AcceptedCount);

    // Trimmed on the way in, both of them — a run report echoes these back to the operator.
    Assert.Equal("people.csv", run.Value.FileName);
    Assert.Equal("importer", run.Value.ExecutedBy);
  }

  [Fact]
  public void A_run_without_an_actor_or_a_file_name_is_refused()
  {
    Assert.Equal(
      ImportExportErrors.InvalidActor,
      EmployeeImportRun.Applied(
        Tenant, Company, Key(), "people.csv", 1, 1, Executed, "   ").Error);

    Assert.Equal(
      ImportExportErrors.InvalidFileName,
      EmployeeImportRun.Applied(
        Tenant, Company, Key(), "   ", 1, 1, Executed, "importer").Error);

    Assert.Equal(
      ImportExportErrors.InvalidFileName,
      EmployeeImportRun.Applied(
        Tenant, Company, Key(), new string('x', EmployeeImportRun.FileNameMaximumLength + 1),
        1, 1, Executed, "importer").Error);
  }

  // ================================================================================================
  // I3. THE EXPORT RECORD SAYS WHAT LEFT (SEC-DOC-0404).
  // ================================================================================================
  //
  // The column set is stored IN ORDER, because the order is part of what left. The scope lists are stored
  // SORTED, because two records describing the same scope must be textually identical or an investigator
  // comparing them is comparing enumeration orders.
  [Fact]
  [Trait("Decision", "SEC-DOC-0404")]
  public void The_export_record_preserves_column_order_and_sorts_the_scope()
  {
    var first = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    var second = Guid.Parse("00000000-0000-0000-0000-000000000001");

    var run = EmployeeExportRun.Completed(
      Tenant, Company, rowCount: 42,
      columnSet: ["employeeNumber", "fullName", "employmentDate"],
      scopeCompanyIds: [first, second],
      scopeBranchIds: [],
      Executed, "exporter");

    Assert.True(run.IsSuccess);
    Assert.Equal("employeeNumber,fullName,employmentDate", run.Value.ColumnSet);
    Assert.Equal($"{second},{first}", run.Value.ScopeCompanyIds);

    // AN EMPTY SCOPE LIST IS A REAL ANSWER, not a missing one: "the scope resolved to no branches" is a
    // fact worth recording, and an empty string is how it is recorded.
    Assert.Equal(string.Empty, run.Value.ScopeBranchIds);
  }

  [Fact]
  [Trait("Decision", "SEC-DOC-0404")]
  public void An_export_recording_no_columns_is_refused()
  {
    Assert.Equal(
      ImportExportErrors.InvalidColumnSet,
      EmployeeExportRun.Completed(
        Tenant, Company, 0, [], [], [], Executed, "exporter").Error);

    // A column name containing the separator would make the stored list unreadable, and the value it came
    // from is not a column name.
    Assert.Equal(
      ImportExportErrors.InvalidColumnSet,
      EmployeeExportRun.Completed(
        Tenant, Company, 0, ["employeeNumber,fullName"], [], [], Executed, "exporter").Error);
  }

  [Fact]
  public void An_export_of_no_rows_is_still_a_completed_export()
  {
    var run = EmployeeExportRun.Completed(
      Tenant, Company, rowCount: 0, ["employeeNumber"], [], [], Executed, "exporter");

    Assert.True(run.IsSuccess);
    Assert.Equal(0, run.Value.RowCount);
  }

  [Fact]
  public void An_export_with_a_negative_row_count_is_refused()
  {
    Assert.Equal(
      ImportExportErrors.InvalidCounts,
      EmployeeExportRun.Completed(
        Tenant, Company, -1, ["employeeNumber"], [], [], Executed, "exporter").Error);
  }

  // ================================================================================================
  // I4. THE IMPORT KEY FOLLOWS THE `EmployeeNumber` CONVENTION EXACTLY (DEC-DOC-0004).
  // ================================================================================================
  [Fact]
  [Trait("Decision", "DEC-DOC-0004")]
  public void The_import_key_trims_for_display_and_uppercases_for_comparison()
  {
    var key = ImportKey.Create("  Batch-2026-08  ");

    Assert.True(key.IsSuccess);
    Assert.Equal("Batch-2026-08", key.Value.Value);
    Assert.Equal("BATCH-2026-08", key.Value.NormalizedValue);
  }

  // TWO KEYS DIFFERING ONLY IN CASE ARE ONE KEY, which is what makes the unique index answer the operator's
  // "did my import happen?" rather than letting a re-typed key import a second time.
  [Fact]
  [Trait("Decision", "DEC-DOC-0004")]
  public void Two_import_keys_differing_only_in_case_are_equal()
  {
    Assert.Equal(ImportKey.Create("batch-1").Value, ImportKey.Create("BATCH-1").Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("with\tcontrol")]
  public void An_unusable_import_key_is_refused(string? value)
  {
    var key = ImportKey.Create(value);

    Assert.True(key.IsFailure);
    Assert.Equal(ImportExportErrors.InvalidImportKey, key.Error);
  }

  [Fact]
  public void An_import_key_longer_than_the_column_is_refused()
  {
    Assert.True(ImportKey.Create(new string('k', ImportKey.MaximumLength)).IsSuccess);
    Assert.True(ImportKey.Create(new string('k', ImportKey.MaximumLength + 1)).IsFailure);
  }

  // ---- THE OUTCOME VOCABULARY IS CLOSED, AND `InProgress` IS NOT IN IT.
  //
  // Its absence is `DEC-DOC-0007`'s synchronous execution rather than an oversight, and the day it arrives
  // it must arrive with a timeout, an owner and a reconciliation pass. Asserting the exact member set is how
  // that stays a decision rather than a drift.
  [Fact]
  [Trait("Decision", "DEC-DOC-0007")]
  public void The_outcome_vocabulary_is_exactly_three_terminal_values()
  {
    Assert.Equal(
      ["Applied", "Refused", "Validated"],
      Enum.GetNames<EmployeeImportOutcome>().OrderBy(name => name, StringComparer.Ordinal));
  }
}
