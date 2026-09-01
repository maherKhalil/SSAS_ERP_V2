using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Permissions;
using SSAS.GL.Application.Permissions;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Journals;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// GL'S STRUCTURAL GUARDS (FP-011).
//
// These assert properties that no unit test can reach and that a reviewer would have to remember: module
// isolation, the unforgeable read scope, the permission catalog's completeness, the absence of a DELETE
// verb, and the shape the ownership rulings gave the schema.
//
// Each one exists because the property it pins is invisible at the point where it would be broken.
public sealed class GlArchitectureTests
{
  // ================================================================================================
  // MODULE ISOLATION (ADR-012)
  // ================================================================================================

  [Theory]
  [Trait("Decision", "ADR-012")]
  [InlineData("SSAS.GL.Domain")]
  [InlineData("SSAS.GL.Application")]
  [InlineData("SSAS.GL.Infrastructure")]
  [InlineData("SSAS.GL.API")]
  public void Gl_assemblies_reference_no_other_module_and_no_platform_assembly(string assemblyName)
  {
    var assembly = Assembly.Load(assemblyName);

    var forbidden = assembly.GetReferencedAssemblies()
      .Select(reference => reference.Name ?? string.Empty)
      .Where(name =>
        name.StartsWith("SSAS.HR", StringComparison.Ordinal) ||
        name.StartsWith("SSAS.Platform", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(forbidden);
  }

  // ================================================================================================
  // THE CONTRACTS ASSEMBLY RETURNED, ON THE CONDITION ITS OWN GUARD NAMED (FP-012, OD-PAY-0013).
  // ================================================================================================
  //
  // The guard replaced here asserted that `SSAS.GL.Contracts` did NOT exist, and its comment named the exact
  // circumstance under which that would stop being right:
  //
  //   > This guard is what stops it being recreated as scaffolding rather than because a consumer exists —
  //   > when Payroll needs one, it returns SHAPED BY ITS CONSUMER, and this test is the deliberate speed
  //   > bump.
  //
  // FP-012 met that condition. **The guard was REPLACED rather than deleted**, because the thing worth
  // protecting did not go away — it changed from "this must not exist" to "if it exists, it must still be a
  // contract rather than a window into the ledger".
  //
  // ---- AND THE OLD GUARD WAS VACUOUS, WHICH IS WORTH RECORDING.
  //
  // It asked `AppDomain.CurrentDomain.GetAssemblies()` whether `SSAS.GL.Contracts` was LOADED. Architecture
  // .Tests never referenced it, so the assembly was never loaded and the assertion passed **by not
  // looking** — it would have passed just as happily on the day the project was recreated. A guard that
  // cannot fail is not protecting anything, and this one would have reported green through the very change
  // it was written to catch.
  [Fact]
  [Trait("Decision", "OD-PAY-0013")]
  public void The_contracts_assembly_exists_because_a_consumer_needs_it()
  {
    // Loaded by REFERENCE, not by hoping something else loaded it — the failure mode of the guard this
    // replaces.
    var contracts = typeof(SSAS.GL.Contracts.Posting.IJournalPoster).Assembly;

    Assert.Equal("SSAS.GL.Contracts", contracts.GetName().Name);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0013")]
  public void The_contracts_assembly_references_nothing_so_it_cannot_leak_the_ledger()
  {
    // A contract that referenced GL's domain would re-create the coupling `ADR-012` forbids by another
    // route: the consumer would transitively see the ledger's internals. Everything crossing this boundary
    // is a primitive or a type declared in the contract itself.
    var referenced = typeof(SSAS.GL.Contracts.Posting.IJournalPoster).Assembly
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name)
      .Where(name => name is not null && name.StartsWith("SSAS.", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(referenced);
  }

  // ================================================================================================
  // THE UNFORGEABLE READ SCOPE (DEC-GL-0004)
  // ================================================================================================

  [Fact]
  [Trait("Decision", "DEC-GL-0004")]
  public void Every_read_service_method_requires_a_scope()
  {
    // ---- THE SCOPE-BYPASS PROHIBITION.
    //
    // A read that omitted its scope predicate must not be EXPRESSIBLE, not merely reviewable. That holds
    // only while every method on the port demands one — an overload without it would be the crack, and it
    // would look entirely innocent in a diff.
    var methods = typeof(IGlReadService).GetMethods();

    Assert.NotEmpty(methods);
    Assert.All(methods, method =>
      Assert.Equal(typeof(GlReadScope), method.GetParameters()[0].ParameterType));
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0004")]
  public void A_read_scope_cannot_be_constructed_from_outside_its_assembly()
  {
    // Private constructor, internal factory. If either became public, holding a scope would stop being
    // proof that the resolver ran — and every read's authorization would silently become advisory.
    Assert.Empty(typeof(GlReadScope).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    var factory = typeof(GlReadScope).GetMethod(
      "Create", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

    Assert.NotNull(factory);
    Assert.False(factory!.IsPublic);
  }

  [Fact]
  [Trait("Decision", "AC-GL-0014")]
  public void An_empty_company_set_cannot_produce_a_scope()
  {
    // The "empty means everything" bug is unrepresentable rather than guarded against: no scope with an
    // empty set exists, so `WHERE CompanyId IN ()` never reaches SQL.
    var factory = typeof(GlReadScope).GetMethod(
      "Create", BindingFlags.Static | BindingFlags.NonPublic)!;

    var result = factory.Invoke(null, [Guid.NewGuid(), Array.Empty<Guid>()]);

    Assert.Null(result);
  }

  // ================================================================================================
  // PERMISSIONS (DEC-GL-0003, FP-006P)
  // ================================================================================================

  [Fact]
  [Trait("Decision", "DEC-GL-0003")]
  public void Every_named_permission_is_defined_by_the_catalog_contributor()
  {
    // ---- NAMING IS NOT REGISTERING, AND THIS IS THE TEST THAT SAYS SO.
    //
    // FP-006P: HR's constants existed, no catalog defined them, no role could hold one, and every endpoint
    // refused every caller. The failure is total and silent. A constant added here without a definition
    // there produces a permission that authorizes nothing.
    var named = typeof(GlPermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral && field.FieldType == typeof(string))
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();

    var defined = new GlPermissionCatalogContributor().Permissions
      .Select(permission => permission.Name)
      .ToArray();

    Assert.NotEmpty(named);
    Assert.Equal(named.OrderBy(name => name, StringComparer.Ordinal), defined.OrderBy(name => name, StringComparer.Ordinal));
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0003")]
  public void Every_permission_name_has_exactly_three_segments_and_the_gl_plane()
  {
    var named = typeof(GlPermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral)
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();

    Assert.All(named, name =>
    {
      var segments = name.Split('.');

      Assert.Equal(3, segments.Length);
      Assert.Equal("GL", segments[0]);
      Assert.All(segments, segment => Assert.NotEmpty(segment));
    });
  }

  [Fact]
  public void Every_permission_definition_carries_a_description_written_for_the_grantor()
  {
    // The description is read by the person granting the permission, not by a developer. An empty one makes
    // the grant screen a list of identifiers.
    var definitions = new GlPermissionCatalogContributor().Permissions;

    Assert.All(definitions, definition => Assert.False(string.IsNullOrWhiteSpace(definition.Description)));
  }

  // ================================================================================================
  // THE OWNERSHIP RULINGS, AS SCHEMA (OD-GL-0003, OD-GL-0004, OD-GL-0005)
  // ================================================================================================

  [Fact]
  [Trait("Decision", "OD-GL-0003")]
  public void The_account_table_has_no_company_column_in_the_composed_model()
  {
    // The ruling made visible where it would be broken. A convention or shadow property that added a
    // CompanyId would turn account maintenance into a company-scoped write without anyone editing a handler.
    var entity = ComposedModel().FindEntityType(typeof(Account));

    Assert.NotNull(entity);
    Assert.Null(entity!.FindProperty(nameof(SSAS.BuildingBlocks.Domain.ICompanyOwnedEntity.CompanyId)));
    Assert.NotNull(entity.FindProperty(nameof(ITenantOwnedEntity.TenantId)));
  }

  [Fact]
  [Trait("Decision", "OD-GL-0005")]
  public void No_gl_table_carries_a_branch_column()
  {
    // `OD-GL-0005` declined the branch dimension for V1. Company and branch remain siblings; GL simply does
    // not use the second.
    var model = ComposedModel();

    foreach (var type in GlEntityTypes)
    {
      var entity = model.FindEntityType(type);

      Assert.NotNull(entity);
      Assert.Null(entity!.FindProperty(nameof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity.BranchId)));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0001")]
  public void Every_monetary_column_is_decimal_19_4()
  {
    // `ADR-027` named General Ledger in its deferred obligations: adopt decision 1 or amend the ADR.
    // `DEC-GL-0001` adopted it, and this is where the adoption is checked rather than assumed.
    var model = ComposedModel();

    foreach (var type in new[] { typeof(JournalLine), typeof(JournalDraftLine) })
    {
      var entity = model.FindEntityType(type)!;

      foreach (var name in new[] { "Debit", "Credit" })
      {
        var property = entity.FindProperty(name);

        Assert.NotNull(property);
        Assert.Equal(19, property!.GetPrecision());
        Assert.Equal(4, property.GetScale());
      }
    }
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0006")]
  public void Every_gl_string_column_is_unicode()
  {
    // `Constraints.md` requires Arabic and English. A single non-unicode column is a data-loss defect that
    // surfaces only for the users who need it most.
    var model = ComposedModel();

    foreach (var type in GlEntityTypes)
    {
      var entity = model.FindEntityType(type)!;

      foreach (var property in entity.GetProperties().Where(candidate => candidate.ClrType == typeof(string)))
      {
        Assert.True(
          property.IsUnicode() ?? true,
          $"{entity.ClrType.Name}.{property.Name} is not unicode");
      }
    }
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0007")]
  public void Only_mutable_gl_aggregates_carry_a_row_version()
  {
    var model = ComposedModel();

    foreach (var type in new[] { typeof(Account), typeof(SSAS.GL.Domain.Calendar.FiscalYear),
      typeof(SSAS.GL.Domain.Calendar.FiscalPeriod), typeof(JournalDraft) })
    {
      Assert.NotNull(model.FindEntityType(type)!.FindProperty(nameof(Account.RowVersion)));
    }

    // An append-only type carrying one would advertise a mutation that cannot happen.
    foreach (var type in new[] { typeof(JournalEntry), typeof(JournalLine) })
    {
      // ⚠⚠ BOTH HALVES BOUND, NOT JUST THIS ONE (258). The positive above and this negative shared a bare
      // string: a RENAME broke the positive loudly, but A TYPO AT THIS SITE ALONE left the positive green
      // and this one passing over a lookup that could never hit. A companion elsewhere does not protect the
      // individual site.
      Assert.Null(model.FindEntityType(type)!.FindProperty(nameof(Account.RowVersion)));
    }
  }

  // ================================================================================================
  // THE E3 MANIFEST (DEC-GL-0010)
  // ================================================================================================

  [Fact]
  [Trait("Decision", "DEC-GL-0010")]
  public void Every_gl_entity_is_tenant_owned_and_therefore_enters_the_cutover_manifest()
  {
    // Derived from the interface rather than from a list, because that is exactly how
    // `TenantCutoverCopyPlan` derives the manifest. A type that fails this is a type whose rows disappear
    // at cutover, silently.
    Assert.All(GlEntityTypes, type =>
      Assert.Contains(typeof(ITenantOwnedEntity), type.GetInterfaces()));
  }

  // ================================================================================================
  // SHARED
  // ================================================================================================

  private static readonly Type[] GlEntityTypes =
  [
    typeof(Account),
    typeof(SSAS.GL.Domain.Calendar.FiscalYear),
    typeof(SSAS.GL.Domain.Calendar.FiscalPeriod),
    typeof(JournalDraft),
    typeof(JournalDraftLine),
    typeof(JournalEntry),
    typeof(JournalLine)
  ];

  private static IModel ComposedModel()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    using var context = new TenantDbContext(
      options,
      new ModelOnlyUser(),
      new ModelOnlyTenant(),
      new ModelOnlyClock(),
      modelContributors: [new GlTenantModelContributor()]);

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
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}
