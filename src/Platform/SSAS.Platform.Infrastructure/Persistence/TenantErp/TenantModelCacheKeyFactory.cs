using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// THE TENANT MODEL VARIES BY WHICH MODULES CONTRIBUTED TO IT (FP-006C3-pre, ADR-012).
//
// EF's default cache key is the CONTEXT TYPE (plus the design-time flag). That is correct for a context
// whose model is fixed by its own OnModelCreating, and wrong for one that composes module contributions:
// a maintenance context built with no contributors and an application context built with HR's would share
// one cached model, and whichever was created first in the process would win.
//
// The failure mode is the worst kind — silent and order-dependent. A migration tool could see HR's tables
// because a request built the model first, or an application could fail to find them because a health probe
// did. Folding the contributor set into the key makes the two models genuinely different models.
//
// THE SIGNATURE IS THE ORDERED SET OF CONTRIBUTOR TYPES, not their instances: contributors are required to
// be deterministic, so two contexts with the same contributor types have the same model by construction.
internal sealed class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
  public object Create(DbContext context, bool designTime) =>
    context is TenantDbContext tenant
      ? (context.GetType(), tenant.ModelSignature, designTime)
      : (context.GetType(), string.Empty, designTime);
}
