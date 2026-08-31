using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Permissions;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// PAYROLL'S STRUCTURAL GUARDS (FP-012).
//
// Each pins a property that is invisible at the point where it would be broken. Two are unique to this
// module and carry more weight than their GL equivalents:
//
//   * **Isolation is asserted in BOTH directions.** Payroll reaches GL only through `SSAS.GL.Contracts` and
//     HR only through `SSAS.HR.Contracts` — and neither GL nor HR may learn about Payroll, because the
//     "one-directional coupling" argument in `DEC-PAY-0018` stops being true the moment they do.
//   * **The scope guards protect personal data.** Elsewhere a forgeable scope is an authorization defect;
//     for compensation it is a personal-data breach.
public sealed class PayrollArchitectureTests
{
  // ================================================================================================
  // MODULE ISOLATION, BOTH DIRECTIONS (ADR-012, DEC-PAY-0017, DEC-PAY-0018)
  // ================================================================================================

  [Theory]
  [Trait("Decision", "ADR-012")]
  [InlineData("SSAS.Payroll.Domain")]
  [InlineData("SSAS.Payroll.Application")]
  [InlineData("SSAS.Payroll.Infrastructure")]
  [InlineData("SSAS.Payroll.API")]
  public void Payroll_assemblies_reach_other_modules_only_through_contracts(string assemblyName)
  {
    var assembly = Assembly.Load(assemblyName);

    var forbidden = assembly.GetReferencedAssemblies()
      .Select(reference => reference.Name)
      .Where(name => name is not null)
      .Where(name =>
        // Another module's IMPLEMENTATION assemblies are out of reach. The two `.Contracts` assemblies are
        // the sanctioned doors and are deliberately not in this list.
        name!.StartsWith("SSAS.GL.", StringComparison.Ordinal) && name != "SSAS.GL.Contracts" ||
        name.StartsWith("SSAS.HR.", StringComparison.Ordinal) && name != "SSAS.HR.Contracts")
      .ToArray();

    Assert.Empty(forbidden);
  }

  [Theory]
  [Trait("Decision", "ADR-012")]
  [InlineData("SSAS.GL.Domain")]
  [InlineData("SSAS.GL.Application")]
  [InlineData("SSAS.GL.Infrastructure")]
  [InlineData("SSAS.HR.Domain")]
  [InlineData("SSAS.HR.Application")]
  [InlineData("SSAS.HR.Infrastructure")]
  // ⚠ CITED BY B18, body-confirmed: ⚠ SUPERSET. The criterion names Payroll and GL specifically; this asserts NO module references
  // Payroll at all.
  [Trait("Criterion", "AC-PAY-0025")]
  // ⚠ CITED BY B18 pass 15: ⚠ PARTLY PINNED, clause 1 only, and by SUPERSET.
  //
  // `AC-PAY-0003` clause 1 is *no EMPLOYEE compensation value is readable through any HR endpoint*. The
  // three HR assemblies are among the six this theory walks, and an assembly that cannot reference
  // `SSAS.Payroll.*` cannot expose a compensation type through any endpoint at all.
  //
  // ⚠ Clause 2 -- *or stored on any HR table* -- is NOT this test. It is an assembly-reference ban, and a
  // pay column on an HR table would need no reference to Payroll whatever. See
  // `No_employee_compensation_value_is_declared_in_hr` for that half.
  [Trait("Criterion", "AC-PAY-0003")]
  public void No_other_module_learns_about_payroll(string assemblyName)
  {
    // ---- THE OTHER DIRECTION, AND IT IS NOT SYMMETRY FOR ITS OWN SAKE.
    //
    // `DEC-PAY-0018` permits the ledger poster to skip a GL permission check partly because the coupling is
    // ONE-DIRECTIONAL: Payroll depends on GL, and GL knows nothing of payroll. If GL ever referenced
    // Payroll, that argument would silently stop holding while the decision still read as ratified.
    var referenced = Assembly.Load(assemblyName)
      .GetReferencedAssemblies()
      .Select(reference => reference.Name)
      .Where(name => name is not null && name.StartsWith("SSAS.Payroll", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(referenced);
  }

  [Fact]
  [Trait("Decision", "ADR-012")]
  public void The_payroll_api_layer_references_no_platform_assembly()
  {
    var referenced = Assembly.Load("SSAS.Payroll.API")
      .GetReferencedAssemblies()
      .Select(reference => reference.Name)
      .Where(name => name is not null && name.StartsWith("SSAS.Platform", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(referenced);
  }

  // ================================================================================================
  // THE UNFORGEABLE READ SCOPE — AND HERE IT GUARDS PERSONAL DATA
  // ================================================================================================

  [Fact]
  public void Every_read_service_method_requires_a_scope()
  {
    // A read that omitted its scope predicate is not something a reviewer has to catch, because it is not
    // something a caller can express. There is no overload without one, and no default.
    var withoutScope = typeof(IPayrollReadService)
      .GetMethods()
      .Where(method => !method.GetParameters().Any(p => p.ParameterType == typeof(PayrollReadScope)))
      .Select(method => method.Name)
      .ToArray();

    Assert.Empty(withoutScope);
  }

  [Fact]
  // ⚠ CITED BY B18, body-confirmed: ⚠ PARTIAL. `AC-PAY-0028` has two halves: *a read scope cannot be SUPPLIED BY THE CALLER*, and *a
  // request attempting to WIDEN ITS OWN SCOPE is refused*. This asserts the first structurally -- no
  // public constructors, no factories outside the assembly. The runtime refusal half is pinned by
  // nothing here, and is recorded rather than implied (B18).
  [Trait("Criterion", "AC-PAY-0028")]
  public void A_read_scope_cannot_be_constructed_from_outside_its_assembly()
  {
    // The credential stays per-module (`ADR-027` d4 promotion boundary): the VALUE moved to
    // `AuthorizedCompanySet`, the proof did not. Holding a `PayrollReadScope` means Payroll's resolver ran.
    Assert.Empty(typeof(PayrollReadScope).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    var factories = typeof(PayrollReadScope)
      .GetMethods(BindingFlags.Public | BindingFlags.Static)
      .Where(method => method.ReturnType == typeof(PayrollReadScope) ||
        method.ReturnType == typeof(PayrollReadScope).MakeByRefType())
      .ToArray();

    Assert.Empty(factories);
  }

  [Fact]
  public void An_empty_company_set_cannot_produce_a_scope()
  {
    // An empty authorized set REFUSES the read rather than returning an empty page. Enforcing it at
    // construction makes `WHERE CompanyId IN ()` unrepresentable rather than merely guarded against.
    Assert.Null(SSAS.BuildingBlocks.Application.Authorization.AuthorizedCompanySet.Create([]));
    Assert.Null(SSAS.BuildingBlocks.Application.Authorization.AuthorizedCompanySet.Create(null));
  }

  [Fact]
  public void The_scope_carries_materialized_identifiers_and_no_mode_flag()
  {
    // "All companies" is a LIST, never the absence of a condition. A boolean or enum here would let a query
    // branch on intent instead of filtering on values — predicate omission wearing a scope's clothes.
    foreach (var property in typeof(PayrollReadScope).GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      Assert.False(
        property.PropertyType == typeof(bool) || property.PropertyType.IsEnum,
        $"PayrollReadScope.{property.Name} would let a query branch on intent.");
    }
  }

  // ================================================================================================
  // THE PERMISSION CATALOG (FP-006P)
  // ================================================================================================

  [Fact]
  public void Every_named_permission_is_defined_by_the_catalog_contributor()
  {
    // FP-006P's incident: HR's constants existed, no catalog defined them, no role could hold one, and every
    // Employee endpoint refused every caller. Naming a permission is not registering it.
    var named = typeof(PayrollPermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral)
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();

    var defined = new PayrollPermissionCatalogContributor().Permissions
      .Select(permission => permission.Name)
      .ToArray();

    Assert.Equal(named.OrderBy(n => n, StringComparer.Ordinal), defined.OrderBy(n => n, StringComparer.Ordinal));
  }

  [Fact]
  public void Every_permission_name_has_three_segments_and_the_payroll_plane()
  {
    foreach (var definition in new PayrollPermissionCatalogContributor().Permissions)
    {
      var segments = definition.Name.Split('.');

      Assert.Equal(3, segments.Length);
      Assert.Equal("Payroll", segments[0]);
    }
  }

  [Fact]
  public void Every_permission_definition_carries_a_description_written_for_the_grantor()
  {
    // On this surface the description matters more than anywhere else: someone granting "view runs" needs to
    // know it does not hand over everyone's pay.
    foreach (var definition in new PayrollPermissionCatalogContributor().Permissions)
    {
      Assert.False(string.IsNullOrWhiteSpace(definition.Description));
      Assert.True(definition.Description.Length > 20, $"{definition.Name} has a placeholder description.");
    }
  }

  [Fact]
  [Trait("Decision", "BR-PAY-0010")]
  public void Compensation_and_payslips_are_separate_permissions_from_every_element_permission()
  {
    // `DEC-POS-0018` separated a permission for STRUCTURAL pay bands; individual compensation is personal
    // data, so the split applies with more force. This asserts the two families are distinct names — a
    // future merge would have to delete this test.
    var personal = new[] { PayrollPermissionNames.ViewCompensation, PayrollPermissionNames.ViewPayslips };
    var structural = new[] { PayrollPermissionNames.ViewElements, PayrollPermissionNames.ViewRuns };

    Assert.Empty(personal.Intersect(structural, StringComparer.Ordinal));
  }

  // ================================================================================================
  // THE SCHEMA THE RULINGS GAVE IT
  // ================================================================================================

  [Fact]
  [Trait("Decision", "DEC-PAY-0004")]
  // ⚠ CITED BY B18, body-confirmed: the DECIMAL(19,4) half. A floor of 4 columns is its control.
  [Trait("Criterion", "AC-PAY-0030")]
  public void Every_monetary_column_is_decimal_19_4()
  {
    var columns = ModelWalk.FlooredProperties(
      ModelWalk.FlooredEntities(PayrollEntities(), "Payroll domain", 6), "Payroll domain", 70);

    // ⚠ THE CONTROL ON THIS BAN'S OWN FILTER. The floors above prove the model was read; they cannot
    // prove `ClrType == decimal` still selects anything. Payroll is made of money -- a decimal filter that
    // finds nothing here has broken, and without this the ban is green over an empty set.
    var monetary = columns
      .Where(pair => pair.Property.ClrType == typeof(decimal) || pair.Property.ClrType == typeof(decimal?))
      .ToArray();

    Assert.True(monetary.Length >= 4,
      $"only {monetary.Length} decimal columns were found across {columns.Length} payroll properties; the " +
      "type filter has stopped matching and 'every monetary column is 19,4' would be a claim about nothing.");

    var offenders = monetary
      .Where(pair => pair.Property.GetPrecision() != 19 || pair.Property.GetScale() != 4)
      .Select(pair => $"{pair.Entity.ShortName()}.{pair.Property.Name}")
      .ToArray();

    Assert.Empty(offenders);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0007")]
  // ⚠ CITED BY B18, body-confirmed: the NVARCHAR half; the DECIMAL half is the test below. A floor of 12 columns is its control.
  [Trait("Criterion", "AC-PAY-0030")]
  public void Every_payroll_string_column_is_unicode()
  {
    // `Constraints.md` requires Arabic and English. A pay element's name is exactly the field a user writes
    // in their own language.
    var columns = ModelWalk.FlooredProperties(
      ModelWalk.FlooredEntities(PayrollEntities(), "Payroll domain", 6), "Payroll domain", 70);

    // ⚠ THE CONTROL ON THIS BAN'S OWN FILTER, and it is a DIFFERENT filter from the monetary one above
    // even though both walk the same properties. That is precisely why the floor cannot be shared with it.
    var strings = columns.Where(pair => pair.Property.ClrType == typeof(string)).ToArray();

    Assert.True(strings.Length >= 12,
      $"only {strings.Length} string columns were found across {columns.Length} payroll properties; the " +
      "type filter has stopped matching and the unicode ban would inspect nothing.");

    var offenders = strings
      .Where(pair => pair.Property.IsUnicode() == false)
      .Select(pair => $"{pair.Entity.ShortName()}.{pair.Property.Name}")
      .ToArray();

    Assert.Empty(offenders);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0010")]
  // ⚠ CITED BY B18, body-confirmed: ⚠ AND THE CRITERION'S COUNT IS STALE. It says "all FIVE payroll tables"; the manifest's exact list
  // carries SEVEN -- EmployeeCompensation, PayElement, PayElementAssignment, PayrollPeriod, PayrollRun,
  // PayrollRunDraftLine, PayrollRunLine -- and CutoverManifestArchitectureTests says so in its own
  // comment. The PROPERTY holds; the NUMBER in the criterion does not.
  [Trait("Criterion", "AC-PAY-0029")]
  public void Every_payroll_entity_is_tenant_owned_and_therefore_enters_the_cutover_manifest()
  {
    // ---- THE SILENT FAILURE THIS PREVENTS.
    //
    // `TenantCutoverCopyPlan.Build` derives its manifest by REFLECTING over `ITenantOwnedEntity`. A type
    // without the interface is absent from cutover and nothing says so — FP-011 shipped two such types
    // before catching them. Being an owned child is a DOMAIN fact; being copied is a REFLECTION fact.
    var entities = ModelWalk.FlooredEntities(PayrollEntities(), "Payroll domain", 6);

    // ⚠ The floor proves entities were found. The assignability test is the matcher, and the control
    // for it is the POSITIVE case: payroll entities are tenant-owned, so the same test must select them.
    Assert.Contains(entities, entity => typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType));

    var notTenantOwned = entities
      .Where(entity => !typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType))
      .Select(entity => entity.ShortName())
      .ToArray();

    Assert.Empty(notTenantOwned);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0009")]
  public void Only_mutable_payroll_aggregates_carry_a_row_version()
  {
    // A history row is never updated, so `EmployeeCompensation` deliberately has none — advertising one
    // would suggest an update path that does not exist. The append-only line has none for the same reason.
    var withRowVersion = PayrollEntities()
      .Where(entity => entity.GetProperties().Any(property => property.Name == "RowVersion"))
      .Select(entity => entity.ShortName())
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(["PayrollPeriod", "PayrollRun"], withRowVersion);
  }

  [Fact]
  // ⚠ CITED BY B18, body-confirmed: ⚠ SUPERSET. The criterion bans a foreign key to a PLATFORM-DATABASE table; this bans one to ANY
  // other module's table, which is strictly wider. Cited as the superset rather than shadowed by a
  // narrower Platform-only copy that would assert less.
  [Trait("Criterion", "AC-PAY-0031")]
  public void No_payroll_foreign_key_points_at_another_modules_table()
  {
    // A database-level FK across a module boundary would couple the two migration streams and make the
    // boundary a fiction at the schema layer even while `ADR-012` held at the assembly layer.
    var entities = ModelWalk.FlooredEntities(PayrollEntities(), "Payroll domain", 6);

    // ⚠ THE FOREIGN-KEY LAYER IS ITS OWN WALK AND GETS ITS OWN FLOOR. A healthy entity list whose
    // `GetForeignKeys()` returns nothing is a different failure from an empty model, and the ban below
    // cannot tell the difference on its own.
    var keys = entities.SelectMany(entity => entity.GetForeignKeys()).ToArray();

    Assert.True(keys.Length >= 8,
      $"{entities.Length} payroll entities declared only {keys.Length} foreign keys; the relationship walk " +
      "has collapsed and 'no key crosses a module' would be a claim about nothing.");

    var crossing = keys
      .Where(key =>
      {
        var principal = key.PrincipalEntityType.ClrType.FullName ?? string.Empty;
        return principal.StartsWith("SSAS.HR.", StringComparison.Ordinal) ||
          principal.StartsWith("SSAS.GL.", StringComparison.Ordinal);
      })
      .Select(key => $"{key.DeclaringEntityType.ShortName()} -> {key.PrincipalEntityType.ShortName()}")
      .ToArray();

    Assert.Empty(crossing);
  }

  private static IEnumerable<IEntityType> PayrollEntities() =>
    ComposedModel().GetEntityTypes()
      .Where(entity => (entity.ClrType.FullName ?? string.Empty)
        .StartsWith("SSAS.Payroll.Domain", StringComparison.Ordinal));

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
      modelContributors: [new PayrollTenantModelContributor()]);

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
