using SSAS.BuildingBlocks.Tenancy.Permissions;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Permissions;

// ==================================================================================================
// THE ONE PERMISSION CATALOG THE APPLICATION RUNS ON: PLATFORM'S OWN PLUS EVERY REGISTERED MODULE'S.
// ==================================================================================================
//
// ---- WHY THIS EXISTS.
//
// A role may only be granted a permission the catalog defines. Platform owns its own definitions and may
// not reference a business module, so before this type there was nowhere for `HR.Employees.*` to live: the
// names existed as constants, no catalog knew them, and every Employee endpoint refused every caller.
//
// This composes the two sources at startup and is the only `IPermissionCatalog` the container hands out.
// Platform's built-in set is unchanged and still usable on its own — existing tests construct
// `PlatformPermissionCatalog` directly and continue to.
//
// ---- IT IS BUILT ONCE, VALIDATED WHOLE, AND IMMUTABLE AFTERWARDS.
//
// Everything happens in the constructor: a malformed name, a duplicate, or a blank description refuses the
// whole composition rather than producing a catalog that is quietly missing a permission. There is no
// registration method and no mutable state, so a contributor cannot reach back and change the catalog once
// it exists — the set a request is authorized against is the set composition approved.
//
// ---- CONTRIBUTED PERMISSIONS GET NO WEAKER RULE THAN PLATFORM'S OWN.
//
// Every contributed name goes through the same `PermissionName.Create` grammar that Platform's definitions
// go through. The scope is STAMPED `Tenant` rather than accepted: a business module's functional authority
// is tenant authority, and `PlatformSupport` is cross-tenant operator authority that no module may mint.
//
// ---- A DUPLICATE IS A FAILURE, NOT A MERGE.
//
// Two modules claiming one name, or a module shadowing a Platform permission, means two owners disagree
// about what that name grants. Last-write-wins would resolve it silently by registration order — which is
// the Host's composition order, i.e. by accident. Refusing makes the collision the composition's problem
// on the first run rather than a production authorization surprise later.
public sealed class ComposedPermissionCatalog : IPermissionCatalog
{
  private readonly Dictionary<string, PermissionDefinition> definitions;

  private readonly IReadOnlyCollection<PermissionDefinition> all;

  public ComposedPermissionCatalog(
    PlatformPermissionCatalog platformCatalog,
    IEnumerable<IPermissionCatalogContributor> contributors)
  {
    ArgumentNullException.ThrowIfNull(platformCatalog);
    ArgumentNullException.ThrowIfNull(contributors);

    // Platform's own definitions first and unmodified: they are the established set, and a module must not
    // be able to displace one.
    definitions = platformCatalog.All.ToDictionary(
      definition => definition.Name.Value, StringComparer.Ordinal);

    foreach (var contributor in contributors)
    {
      if (contributor is null)
      {
        throw new InvalidOperationException(
          "A null permission catalog contributor was registered. Module contributions are registered " +
          "explicitly by the Host, so a null entry is a composition defect rather than an empty module.");
      }

      Compose(contributor);
    }

    // ORDERED BY NAME, so `All` is stable across runs and independent of the Host's registration order.
    // A caller that renders or diffs the catalog must not see it change because a module moved in Program.cs.
    all = definitions.Values
      .OrderBy(definition => definition.Name.Value, StringComparer.Ordinal)
      .ToArray()
      .AsReadOnly();
  }

  public IReadOnlyCollection<PermissionDefinition> All => all;

  public bool TryGet(string name, out PermissionDefinition permission) =>
    definitions.TryGetValue(name, out permission!);

  private void Compose(IPermissionCatalogContributor contributor)
  {
    var contributed = contributor.Permissions;
    var contributorName = contributor.GetType().FullName ?? contributor.GetType().Name;

    if (contributed is null)
    {
      throw new InvalidOperationException(
        $"Permission catalog contributor '{contributorName}' returned a null permission set. A module that " +
        "defines no permissions must return an empty set, so the difference stays visible.");
    }

    foreach (var permission in contributed)
    {
      if (permission is null)
      {
        throw new InvalidOperationException(
          $"Permission catalog contributor '{contributorName}' contributed a null permission.");
      }

      // THE CANONICAL GRAMMAR, not a copy of it. Three ASCII-identifier segments, exactly as every
      // Platform-owned permission is validated.
      var name = PermissionName.Create(permission.Name);
      if (name.IsFailure)
      {
        throw new InvalidOperationException(
          $"Permission catalog contributor '{contributorName}' contributed the invalid permission name " +
          $"'{permission.Name}'. A permission name is three ASCII-identifier segments separated by dots.");
      }

      if (string.IsNullOrWhiteSpace(permission.Description))
      {
        throw new InvalidOperationException(
          $"Permission catalog contributor '{contributorName}' contributed '{permission.Name}' with no " +
          "description. The description is what a tenant administrator reads when granting it.");
      }

      // FAIL FAST, NEVER OVERWRITE. This catches a module shadowing a Platform permission and two modules
      // claiming the same name, which are the same defect from different directions.
      if (definitions.ContainsKey(name.Value.Value))
      {
        throw new InvalidOperationException(
          $"Permission '{name.Value.Value}' is defined more than once. Contributor '{contributorName}' " +
          "duplicates a permission another contributor or the platform catalog already defines; each " +
          "permission must have exactly one owner.");
      }

      // TENANT SCOPE IS STAMPED, NOT ACCEPTED. A business module's functional authority is tenant
      // authority; PlatformSupport is cross-tenant operator authority and is never module-contributable.
      definitions.Add(
        name.Value.Value,
        new PermissionDefinition(name.Value, PermissionScope.Tenant, permission.Description.Trim()));
    }
  }
}
