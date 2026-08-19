using SSAS.BuildingBlocks.Tenancy.Permissions;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Tests.IdentityAccess;

// ==================================================================================================
// COMPOSING PLATFORM'S CATALOG WITH EVERY REGISTERED MODULE'S (FP-006P, ADR-012 r1.2).
// ==================================================================================================
//
// ---- THE DEFECT THIS CLOSES.
//
// A role may only be granted a permission the catalog defines. Platform may not reference a business
// module, so HR's five names belonged to no catalog: nothing could grant them, and every Employee endpoint
// refused every caller. The composed catalog is where a module's definitions finally become grantable.
//
// ---- SYNTHETIC CONTRIBUTORS, NOT HR.
//
// Platform.Tests cannot reference HR, which is the module rule working. These tests therefore exercise the
// MECHANISM rather than one consumer of it, so they keep protecting whichever module contributes next. The
// real HR composition is proven in Architecture.Tests, API.Tests and Integration.Tests, where HR is
// reachable.
public sealed class ComposedPermissionCatalogTests
{
  // ---- WITH NO CONTRIBUTORS IT IS EXACTLY PLATFORM'S CATALOG.
  //
  // The regression guard for every existing permission: composing must ADD, never disturb what Platform
  // already owned, or this slice would have silently changed the platform authorization surface.
  [Fact]
  public void With_no_contributors_the_composed_catalog_equals_the_platform_catalog()
  {
    var platform = new PlatformPermissionCatalog();
    var composed = new ComposedPermissionCatalog(platform, []);

    Assert.Equal(
      platform.All.Select(definition => definition.Name.Value).OrderBy(name => name, StringComparer.Ordinal),
      composed.All.Select(definition => definition.Name.Value).OrderBy(name => name, StringComparer.Ordinal));

    // Scope and description carried over untouched, not re-derived.
    foreach (var expected in platform.All)
    {
      Assert.True(composed.TryGet(expected.Name.Value, out var actual));
      Assert.Equal(expected.Scope, actual.Scope);
      Assert.Equal(expected.Description, actual.Description);
    }
  }

  // ---- A CONTRIBUTED PERMISSION BECOMES A REAL, TENANT-SCOPED DEFINITION.
  [Fact]
  public void A_contributed_permission_is_defined_at_tenant_scope()
  {
    var composed = Compose(new ProbeContributor(new ModulePermissionDefinition("Probe.Widgets.View", "View widgets")));

    Assert.True(composed.TryGet("Probe.Widgets.View", out var definition));
    Assert.Equal("Probe.Widgets.View", definition.Name.Value);
    Assert.Equal("View widgets", definition.Description);

    // STAMPED, NOT ACCEPTED. The contract carries no scope, so a module cannot mint cross-tenant
    // PlatformSupport authority however it is written.
    Assert.Equal(PermissionScope.Tenant, definition.Scope);
  }

  // ---- SEVERAL MODULES COMPOSE, AND PLATFORM'S SET SURVIVES ALL OF THEM.
  [Fact]
  public void Several_contributors_compose_alongside_the_platform_set()
  {
    var composed = Compose(
      new ProbeContributor(new ModulePermissionDefinition("Alpha.Things.View", "View things")),
      new ProbeContributor(new ModulePermissionDefinition("Beta.Things.Manage", "Manage things")));

    Assert.True(composed.TryGet("Alpha.Things.View", out _));
    Assert.True(composed.TryGet("Beta.Things.Manage", out _));
    Assert.True(composed.TryGet(PlatformPermissionNames.ViewUsers, out _));
    Assert.True(composed.TryGet(PlatformPermissionNames.AdministerPlatformSupport, out _));
  }

  // ---- ORDERING IS STABLE, AND INDEPENDENT OF REGISTRATION ORDER.
  //
  // `All` is what an administrator's permission list is rendered from. If it changed because a module moved
  // in Program.cs, a diff of that list would report changes nobody made.
  [Fact]
  public void The_composed_catalog_is_ordered_deterministically()
  {
    var first = new ProbeContributor(new ModulePermissionDefinition("Alpha.Things.View", "View things"));
    var second = new ProbeContributor(new ModulePermissionDefinition("Beta.Things.Manage", "Manage things"));

    var forwards = Compose(first, second).All.Select(definition => definition.Name.Value).ToArray();
    var backwards = Compose(second, first).All.Select(definition => definition.Name.Value).ToArray();

    Assert.Equal(forwards, backwards);
    Assert.Equal(forwards.OrderBy(name => name, StringComparer.Ordinal), forwards);
  }

  // ---- TWO CONTRIBUTORS CLAIMING ONE NAME IS A FAILURE, NOT A MERGE.
  //
  // Last-write-wins would resolve the collision by the Host's registration order, i.e. by accident, and the
  // losing module's description would silently become the granted one.
  [Fact]
  public void A_duplicate_contributed_permission_refuses_the_composition()
  {
    var failure = Assert.Throws<InvalidOperationException>(() => Compose(
      new ProbeContributor(new ModulePermissionDefinition("Probe.Widgets.View", "First owner")),
      new ProbeContributor(new ModulePermissionDefinition("Probe.Widgets.View", "Second owner"))));

    Assert.Contains("Probe.Widgets.View", failure.Message, StringComparison.Ordinal);
    Assert.Contains("more than once", failure.Message, StringComparison.Ordinal);
  }

  // ---- INCLUDING A DUPLICATE INSIDE ONE CONTRIBUTOR.
  [Fact]
  public void A_contributor_repeating_its_own_permission_refuses_the_composition()
  {
    Assert.Throws<InvalidOperationException>(() => Compose(new ProbeContributor(
      new ModulePermissionDefinition("Probe.Widgets.View", "View widgets"),
      new ModulePermissionDefinition("Probe.Widgets.View", "View widgets again"))));
  }

  // ---- AND A MODULE CANNOT SHADOW A PLATFORM PERMISSION.
  //
  // The same defect from the other direction: a module redefining Platform.Users.View would change what an
  // established permission grants without touching Platform.
  [Fact]
  public void A_contributor_cannot_shadow_a_platform_permission()
  {
    var failure = Assert.Throws<InvalidOperationException>(() => Compose(new ProbeContributor(
      new ModulePermissionDefinition(PlatformPermissionNames.ViewUsers, "Hijacked"))));

    Assert.Contains(PlatformPermissionNames.ViewUsers, failure.Message, StringComparison.Ordinal);

    // The platform definition is not merely preserved on collision; the whole composition is refused, so
    // there is no catalog carrying a half-applied module.
    Assert.Contains("exactly one owner", failure.Message, StringComparison.Ordinal);
  }

  // ---- CONTRIBUTED NAMES FACE THE CANONICAL GRAMMAR, NOT A LAXER ONE.
  //
  // Three ASCII-identifier segments, exactly as every Platform-owned permission. A module cannot introduce
  // a name the rest of the authorization stack cannot represent.
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("TooFew.Segments")]
  [InlineData("Far.Too.Many.Segments")]
  [InlineData("Probe.Widgets.View ")]
  [InlineData("probe.widgets.9View")]
  [InlineData("Probe.Wid gets.View")]
  [InlineData("Probe..View")]
  public void A_malformed_contributed_permission_name_refuses_the_composition(string name)
  {
    var failure = Assert.Throws<InvalidOperationException>(
      () => Compose(new ProbeContributor(new ModulePermissionDefinition(name, "Probe"))));

    Assert.Contains("invalid permission name", failure.Message, StringComparison.Ordinal);
  }

  // ---- A DESCRIPTION IS REQUIRED, because it is what an administrator reads when granting the permission.
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void A_contributed_permission_without_a_description_refuses_the_composition(string description)
  {
    Assert.Throws<InvalidOperationException>(
      () => Compose(new ProbeContributor(new ModulePermissionDefinition("Probe.Widgets.View", description))));
  }

  // ---- A NULL CONTRIBUTOR OR A NULL SET IS A COMPOSITION DEFECT, NOT AN EMPTY MODULE.
  //
  // Registration is explicit, so a null entry means the Host registered something wrong. Treating it as
  // "no permissions" would hide it and the module's endpoints would refuse everyone with no explanation.
  [Fact]
  public void A_null_contributor_or_permission_set_refuses_the_composition()
  {
    Assert.Throws<InvalidOperationException>(
      () => new ComposedPermissionCatalog(new PlatformPermissionCatalog(), [null!]));

    Assert.Throws<InvalidOperationException>(() => Compose(new NullSetContributor()));

    Assert.Throws<InvalidOperationException>(
      () => Compose(new ProbeContributor(new ModulePermissionDefinition[] { null! })));
  }

  // ---- LOOKUP STAYS EXACT AND ORDINAL for contributed names too, so a case-folded or padded spelling can
  // never satisfy a permission requirement.
  [Fact]
  public void Contributed_lookup_is_exact_and_ordinal()
  {
    var composed = Compose(new ProbeContributor(new ModulePermissionDefinition("Probe.Widgets.View", "View widgets")));

    Assert.True(composed.TryGet("Probe.Widgets.View", out _));
    Assert.False(composed.TryGet("probe.widgets.view", out _));
    Assert.False(composed.TryGet("PROBE.WIDGETS.VIEW", out _));
    Assert.False(composed.TryGet(" Probe.Widgets.View", out _));
  }

  // ---- THE CONTRIBUTOR SET IS READ ONCE, AT COMPOSITION.
  //
  // A contributor that changed its answer afterwards must not change the catalog: the set a request is
  // authorized against has to be the set composition validated.
  [Fact]
  public void The_catalog_does_not_re_read_contributors_after_construction()
  {
    var mutable = new MutableContributor(new ModulePermissionDefinition("Probe.Widgets.View", "View widgets"));
    var composed = Compose(mutable);

    mutable.Replace(new ModulePermissionDefinition("Probe.Widgets.Manage", "Manage widgets"));

    Assert.True(composed.TryGet("Probe.Widgets.View", out _));
    Assert.False(composed.TryGet("Probe.Widgets.Manage", out _));
  }

  private static ComposedPermissionCatalog Compose(params IPermissionCatalogContributor[] contributors) =>
    new(new PlatformPermissionCatalog(), contributors);

  private sealed class ProbeContributor(params ModulePermissionDefinition[] permissions)
    : IPermissionCatalogContributor
  {
    public IReadOnlyCollection<ModulePermissionDefinition> Permissions => permissions;
  }

  private sealed class NullSetContributor : IPermissionCatalogContributor
  {
    public IReadOnlyCollection<ModulePermissionDefinition> Permissions => null!;
  }

  private sealed class MutableContributor(ModulePermissionDefinition initial) : IPermissionCatalogContributor
  {
    private ModulePermissionDefinition current = initial;

    public IReadOnlyCollection<ModulePermissionDefinition> Permissions => [current];

    public void Replace(ModulePermissionDefinition replacement) => current = replacement;
  }
}
