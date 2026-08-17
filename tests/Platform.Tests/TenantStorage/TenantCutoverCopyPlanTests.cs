using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// WHAT THE COPY ENGINE BELIEVES A TENANT'S DATA IS (ADR-020, TS-Storage Phase E3).
//
// The manifest is derived from TenantDbContext's model rather than hand-written, so the engine cannot MISS
// a table. These tests pin the other half: that the derivation is right, and that a human is forced to look
// when the model gains an entity.
public sealed class TenantCutoverCopyPlanTests
{
  // THE DECLARED INVENTORY. Adding a tenant-owned entity must fail this test.
  //
  // The engine would already copy a new entity — the manifest is model-derived — so this is not protecting
  // against a missed table. It is protecting against an UNCONSIDERED one: a new entity may need a copy
  // order, may carry an identity key, may have a computed column, and "the copy compiled" settles none of
  // that. Failing here is the prompt to decide those things deliberately.
  private static readonly string[] DeclaredTenantOwnedEntities = ["Company"];

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_manifest_covers_every_tenant_owned_entity_in_the_tenant_model()
  {
    var modelEntities = TenantDbContextBuilder.TenantModel.GetEntityTypes()
      .Where(entity => !entity.IsOwned())
      .Where(entity => typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType))
      .Where(entity => entity.GetTableName() is not null)
      .Select(entity => entity.ClrType.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // The model and the declared inventory agree...
    Assert.Equal(DeclaredTenantOwnedEntities.OrderBy(name => name, StringComparer.Ordinal), modelEntities);

    // ...and the derived plan covers exactly that set, so nothing the application persists for a tenant is
    // left behind by a cutover.
    var plan = TenantCutoverCopyPlan.Build(TenantDbContextBuilder.TenantModel);
    Assert.True(plan.IsSuccess);
    Assert.Equal(
      modelEntities,
      plan.Value.Select(table => table.EntityName).OrderBy(name => name, StringComparer.Ordinal));
  }

  // ROWVERSION IS NOT A COPYABLE VALUE. It is the target's own concurrency state; carrying the source's
  // bytes over would hand the new database a token describing a different database's history.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Rowversion_columns_are_excluded_from_the_copy_mapping()
  {
    var plan = TenantCutoverCopyPlan.Build(TenantDbContextBuilder.TenantModel);
    Assert.True(plan.IsSuccess);

    var companies = Assert.Single(plan.Value, table => table.EntityName == nameof(Company));

    // The Company model does carry a rowversion, so this is a live exclusion rather than a vacuous one.
    var model = TenantDbContextBuilder.TenantModel.FindEntityType(typeof(Company));
    Assert.NotNull(model);
    Assert.Contains(
      model!.GetProperties(),
      property => property.IsConcurrencyToken && property.ValueGenerated == ValueGenerated.OnAddOrUpdate);

    Assert.DoesNotContain(nameof(Company.RowVersion), companies.Columns);
  }

  // Everything that must survive the move verbatim is in the projection: keys, tenancy, business data,
  // audit provenance and lifecycle state.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_copy_mapping_preserves_keys_tenancy_audit_and_business_columns()
  {
    var plan = TenantCutoverCopyPlan.Build(TenantDbContextBuilder.TenantModel);
    var companies = Assert.Single(plan.Value, table => table.EntityName == nameof(Company));

    Assert.Equal("tenant", companies.Schema);
    Assert.Equal("Companies", companies.TableName);
    Assert.Equal(["CompanyId"], companies.PrimaryKeyColumns);
    Assert.Equal(nameof(ITenantOwnedEntity.TenantId), companies.TenantIdColumn);

    foreach (var required in new[]
    {
      "CompanyId", "TenantId", "CompanyCode", "NormalizedCompanyCode", "CompanyName", "BaseCurrencyCode",
      "Status", "StatusChangeReasonCode", "StatusChangedUtc", "StatusChangedBy",
      "CreatedUtc", "CreatedBy", "ModifiedUtc", "ModifiedBy"
    })
    {
      Assert.Contains(required, companies.Columns);
    }
  }

  // The source read and the validation read share ONE projection, so a column cannot be skipped by the copy
  // and then silently demanded by validation, or the reverse.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_source_projection_is_tenant_filtered_and_primary_key_ordered()
  {
    var plan = TenantCutoverCopyPlan.Build(TenantDbContextBuilder.TenantModel);
    var companies = Assert.Single(plan.Value, table => table.EntityName == nameof(Company));

    Assert.Equal("[CompanyId]", companies.OrderByPrimaryKey);
    Assert.Contains("[TenantId]", companies.ColumnList, StringComparison.Ordinal);
    Assert.Equal("[tenant].[Companies]", companies.QualifiedName);
  }

  // No tenant entity currently uses a database-generated identity key, so KeepIdentity is correctly off for
  // the current model. The mechanism itself is proven against a real identity table in the SQL Server tests.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void No_current_tenant_entity_requires_identity_preservation()
  {
    var plan = TenantCutoverCopyPlan.Build(TenantDbContextBuilder.TenantModel);

    Assert.All(plan.Value, table => Assert.False(table.HasIdentityColumn));
  }

  // FK ORDER, NOT ALPHABETICAL ORDER. With one table this is trivially satisfied; the ordering itself is
  // exercised by the parent/child real-SQL test, and a cycle is refused rather than worked around.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_plan_is_deterministic()
  {
    var first = TenantCutoverCopyPlan.Build(TenantDbContextBuilder.TenantModel);
    var second = TenantCutoverCopyPlan.Build(TenantDbContextBuilder.TenantModel);

    Assert.True(first.IsSuccess);
    Assert.True(second.IsSuccess);
    Assert.Equal(
      first.Value.Select(table => table.TableName),
      second.Value.Select(table => table.TableName));
  }

  // SYNCHRONOUS TENANT PERSISTENCE IS REFUSED, NOT SILENTLY UNFENCED (LOW-1, option B).
  //
  // Both synchronous entry points reach the same override by EF Core's own dispatch, so neither can commit
  // a write that skipped the cutover fence. Throwing needs no database, which is why it is asserted here.
  [Theory]
  [Trait("Decision", "ADR-020")]
  [InlineData(true)]
  [InlineData(false)]
  public void Synchronous_save_changes_cannot_bypass_the_write_fence(bool viaAcceptAllOverload)
  {
    using var context = UnroutedTenantContext();
    context.Companies.Add(NewCompany());

    var refused = viaAcceptAllOverload
      ? Assert.Throws<InvalidOperationException>(() => context.SaveChanges(acceptAllChangesOnSuccess: true))
      : Assert.Throws<InvalidOperationException>(() => context.SaveChanges());

    Assert.Contains("Synchronous SaveChanges is not supported", refused.Message, StringComparison.Ordinal);
  }

  private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

  private static Company NewCompany() =>
    Company.Create(
      TenantId,
      SSAS.Platform.Domain.ValueObjects.CompanyCode.Create("SYNC").Value,
      SSAS.Platform.Domain.ValueObjects.CompanyName.Create("Sync Guard").Value,
      SSAS.Platform.Domain.ValueObjects.BaseCurrencyCode.Create("USD").Value,
      "copy-plan-tests",
      Guid.NewGuid(),
      new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero)).Value;

  // Never opened: the synchronous override throws before any connection is used.
  private static TenantDbContext UnroutedTenantContext()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=never-opened;Database=never-opened;Integrated Security=True")
      .Options;
    return new TenantDbContext(
      options, StubUser.Instance, new StubTenant(TenantId), StubClock.Instance, UnusedFence.Instance);
  }

  private sealed class UnusedFence : SSAS.Platform.Infrastructure.TenantStorage.ITenantWriteFence
  {
    public static readonly UnusedFence Instance = new();

    public Task AdmitWriteAsync(
      Guid tenantId,
      long tenantDatabaseId,
      System.Data.Common.DbConnection connection,
      System.Data.Common.DbTransaction transaction,
      CancellationToken cancellationToken = default) =>
      throw new InvalidOperationException("The synchronous path must never reach the fence.");
  }

  private sealed class StubUser : SSAS.BuildingBlocks.Application.Abstractions.Identity.ICurrentUser
  {
    public static readonly StubUser Instance = new();
    public string? UserId => "copy-plan-tests";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class StubTenant(Guid? tenantId)
    : SSAS.BuildingBlocks.Application.Abstractions.Tenancy.ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class StubClock : SSAS.BuildingBlocks.Application.Abstractions.Time.IDateTimeProvider
  {
    public static readonly StubClock Instance = new();
    public DateTimeOffset UtcNow => new(2026, 8, 17, 10, 0, 0, TimeSpan.Zero);
  }
}
