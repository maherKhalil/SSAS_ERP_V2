using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Domain.ImportExport;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// THE IMPORT AND EXPORT RUN RECORDS' BOUNDARIES (FP-009 Phase 1).
//
// Two entities whose interesting properties are all classifications: which ownership dimensions they take,
// which they refuse, and what they point at. Every one of them is invisible at the call site and silent when
// it regresses, which is what these are for.
public sealed class ImportExportArchitectureTests
{
  // ================================================================================================
  // THE OWNERSHIP ASYMMETRY, PROVEN AGAINST THE MECHANISM THAT MAKES IT MATTER
  // ================================================================================================
  //
  // `TenantDbContext` treats a tracked `ICompanyOwnedEntity` as a company-scoped WRITE: it requires a trusted
  // company context and calls `AuthorizeCurrentCompanyAsync`. An import genuinely is such a write. An export
  // is a READ, and routing its audit record through write authorization would make a read-only caller unable
  // to export — or, worse, make the audit silently gate the read.
  //
  // Asserted as a PAIR. Either half alone would still pass if somebody made the classification uniform,
  // which is exactly the change this exists to catch.
  [Fact]
  [Trait("Decision", "DEC-DOC-0006")]
  public void The_import_record_is_company_owned_and_the_export_record_is_not()
  {
    Assert.Contains(typeof(ICompanyOwnedEntity), typeof(EmployeeImportRun).GetInterfaces());
    Assert.DoesNotContain(typeof(ICompanyOwnedEntity), typeof(EmployeeExportRun).GetInterfaces());

    // ---- AND THE INTERFACE IS THE STAMPING CONTRACT, WHICH IS WHY REFUSING IT IS THE MECHANISM.
    //
    // `ICompanyOwnedEntity.CompanyId` is `{ get; set; }` precisely so the write boundary can assign it from
    // the trusted company context. An entity that implements the interface is therefore something the
    // boundary will stamp — and stamping is what requires the authorization an export must not need. The
    // export record's own `CompanyId` has no public setter, so nothing can stamp it and nothing can mistake
    // it for the ownership column.
    Assert.NotNull(
      typeof(ICompanyOwnedEntity).GetProperty(nameof(ICompanyOwnedEntity.CompanyId))!.SetMethod);

    Assert.False(
      typeof(EmployeeExportRun)
        .GetProperty(nameof(EmployeeExportRun.CompanyId))!.SetMethod?.IsPublic ?? false);
  }

  // ---- BOTH ARE TENANT-OWNED, WHICH IS WHAT PUTS THEM IN THE E3 MANIFEST.
  //
  // Manifest entry is by construction (`DEC-DEP-0029`): `TenantCutoverCopyPlan.Build` reflects over the
  // composed model and includes every non-owned `ITenantOwnedEntity` with a table name. Dropping either
  // interface would remove the table from cutover silently — the copy would still succeed, just shorter.
  [Theory]
  [InlineData(typeof(EmployeeImportRun))]
  [InlineData(typeof(EmployeeExportRun))]
  [Trait("Decision", "ADR-020")]
  public void Both_run_records_are_tenant_owned_and_append_only(Type type)
  {
    var interfaces = type.GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(IAppendOnlyEntity), interfaces);
    Assert.DoesNotContain(typeof(IBranchOwnedEntity), interfaces);

    Assert.Equal(typeof(Entity<Guid>), type.BaseType);
  }

  // ---- AND THE COMPOSED MODEL GIVES EACH A TABLE, which is the second half of manifest membership.
  [Theory]
  [InlineData(typeof(EmployeeImportRun), "EmployeeImportRuns")]
  [InlineData(typeof(EmployeeExportRun), "EmployeeExportRuns")]
  [Trait("Decision", "ADR-012")]
  public void The_contributor_maps_each_run_record_to_a_tenant_table(Type type, string table)
  {
    var entity = ComposedTenantModel().FindEntityType(type);

    Assert.NotNull(entity);
    Assert.False(entity!.IsOwned());
    Assert.Equal(table, entity.GetTableName());
    Assert.Equal("tenant", entity.GetSchema());
  }

  // ---- AND A CONTRIBUTOR-FREE MODEL CONTAINS NEITHER.
  //
  // The negative control. Without it the assertion above could pass against a model that included these
  // types for some other reason, and would prove the contributor did nothing.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public void A_contributor_free_model_contains_neither_run_record()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    using var context = new TenantDbContext(
      options, new ModelOnlyUser(), new ModelOnlyTenant(), new ModelOnlyClock());

    Assert.Null(context.Model.FindEntityType(typeof(EmployeeImportRun)));
    Assert.Null(context.Model.FindEntityType(typeof(EmployeeExportRun)));
  }

  // ================================================================================================
  // NEITHER RUN RECORD POINTS AT AN EMPLOYEE, AND THAT IS WHAT KEEPS THE COPY GRAPH UNCHANGED
  // ================================================================================================
  //
  // A run record names WHO RAN WHAT, never WHICH EMPLOYEES RESULTED. The consequence is structural: with an
  // edge to Employee both tables would become dependents of the longest chain in the model, and a reverse
  // edge would be a cycle — on which `TenantCutoverCopyPlan.Order` returns `CutoverCopyOrderUndecidable` and
  // Shared→Dedicated cutover stops working for every tenant.
  //
  // The exact principal set is asserted rather than only the absence, so an edge added to anything at all is
  // a decision somebody has to make here.
  [Theory]
  [InlineData(typeof(EmployeeImportRun))]
  [InlineData(typeof(EmployeeExportRun))]
  [Trait("Decision", "ADR-020")]
  public void A_run_record_references_its_company_and_nothing_else(Type type)
  {
    var entity = ComposedTenantModel().FindEntityType(type);

    Assert.NotNull(entity);

    var principals = entity!.GetForeignKeys()
      .Select(foreignKey => foreignKey.PrincipalEntityType.ShortName())
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(["Company"], principals);

    // A live exclusion rather than a vacuous one: Employee is genuinely in this model and genuinely
    // referenced by four other HR tables.
    Assert.NotNull(ComposedTenantModel().FindEntityType(typeof(Employee)));
  }

  // ---- NO CONCURRENCY TOKEN ON EITHER, IN THE MODEL AND NOT ONLY ON THE TYPE.
  //
  // A shadow rowversion introduced by a convention would be invisible on the CLR type and perfectly visible
  // here. Append-only records have no concurrency state to protect, and one that acquired a token would be
  // implying an update path that `PreventAppendOnlyMutation` refuses.
  [Theory]
  [InlineData(typeof(EmployeeImportRun))]
  [InlineData(typeof(EmployeeExportRun))]
  [Trait("Decision", "DEC-DOC-0006")]
  public void Neither_run_record_carries_a_concurrency_token(Type type)
  {
    var entity = ComposedTenantModel().FindEntityType(type);

    Assert.DoesNotContain(entity!.GetProperties(), property => property.IsConcurrencyToken);

    // The Modified pair is absent from the model too, not merely unset on the type: this record is never
    // modified, so a column for it would be permanently equal to its created counterpart.
    Assert.Null(entity.FindProperty(nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.ModifiedUtc)));
    Assert.Null(entity.FindProperty(nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.ModifiedBy)));

    // Employee carries a real rowversion, so the exclusion above is a distinction rather than a default.
    Assert.Contains(
      ComposedTenantModel().FindEntityType(typeof(Employee))!.GetProperties(),
      property => property.IsConcurrencyToken);
  }

  private static IModel ComposedTenantModel()
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
    public string? UserId => null;

    public string? UserName => null;

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
}
