using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// ==================================================================================================
// THE TENANT MODEL AS THE APPLICATION ACTUALLY MAPS IT — CONTRIBUTORS INCLUDED (FP-006C6, ADR-020).
// ==================================================================================================
//
// ---- THE DEFECT THIS EXISTS TO CLOSE.
//
// The cutover copy engine derives its table manifest from the tenant model, which is exactly right: a
// hand-written table list is wrong the moment someone adds an entity, and wrong SILENTLY.
//
// But it read that model from a STATIC built with no contributors. So the derivation was faithful to a model
// that structurally could not contain Employee — and a Shared to Dedicated promotion would have copied
// Company and Branch, validated perfectly against the tables it knew about, reported success, and left every
// employee behind. Nothing in the copy would have looked wrong, because nothing in the copy WAS wrong; the
// model it was asked about was.
//
// ---- WHY A SERVICE RATHER THAN A STATIC.
//
// The contributor set is a COMPOSITION FACT. It is decided by the Host when it registers modules, and a
// static initialiser cannot ask the container what was registered — which is precisely why the old one
// silently answered for a model nobody runs. Resolving the same IEnumerable<ITenantModelContributor> the
// runtime context factory resolves means the copy engine and the application cannot be looking at two
// different tenant models: there is one registration, and both read it.
//
// ---- SINGLETON, AND SAFE TO BE ONE.
//
// The contributor set is fixed for the process's lifetime, and building an EF model is expensive. The model
// is built once, lazily, and EF's own model cache would deduplicate it anyway — TenantModelCacheKeyFactory
// folds the contributor signature into the cache key, so this instance and a request-time context with the
// same contributors genuinely share one model rather than coincidentally agreeing.
//
// ---- IT NEVER OPENS A CONNECTION.
//
// EF builds a model from metadata alone. The placeholder connection string is never dialled, which is why a
// component that only needs to reason about what a tenant's data IS can hold this without any routing.
internal interface ITenantModelSource
{
  IModel Model { get; }
}

internal sealed class ComposedTenantModelSource : ITenantModelSource
{
  private readonly Lazy<IModel> model;

  public ComposedTenantModelSource(IEnumerable<ITenantModelContributor> contributors)
  {
    ArgumentNullException.ThrowIfNull(contributors);

    // Materialised at construction: the registered set must not be able to change between the moment this
    // is built and the moment a cutover asks for the model.
    var registered = contributors.ToArray();

    model = new Lazy<IModel>(() => BuildModel(registered));
  }

  public IModel Model => model.Value;

  private static IModel BuildModel(IReadOnlyCollection<ITenantModelContributor> contributors)
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    // The MAINTENANCE identity, and a null tenant on purpose: this context is never queried, and a null
    // tenant makes an accidental entity query fail closed against the global filter rather than return
    // another tenant's rows.
    using var context = new TenantDbContext(
      options,
      MaintenanceIdentity.User,
      MaintenanceIdentity.Tenant,
      MaintenanceIdentity.Clock,
      modelContributors: contributors);

    return context.Model;
  }
}
