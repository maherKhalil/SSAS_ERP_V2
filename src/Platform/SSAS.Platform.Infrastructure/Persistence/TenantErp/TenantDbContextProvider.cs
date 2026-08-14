using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Scoped holder for the request's tenant ERP context (ADR-017).
//
// CONTEXT LIFETIME IS FIXED ONCE CREATED. A context keeps the physical database it was routed to for its
// whole life; a routing change mid-scope does not retarget a live context, because a unit of work that
// silently switched databases underneath an open transaction is a data-integrity problem, not a feature.
// A new assignment therefore affects the NEXT context creation, which is the behaviour cutover (ADR-020)
// is designed around.
public sealed class TenantDbContextProvider(
  ITenantDbContextFactory factory,
  ICurrentTenant currentTenant) : ITenantDbContextProvider, IAsyncDisposable
{
  private TenantDbContext? context;
  private bool disposed;

  public async Task<Result<TenantDbContext>> ResolveAsync(CancellationToken cancellationToken = default)
  {
    ObjectDisposedException.ThrowIf(disposed, this);

    if (context is not null)
    {
      return Result.Success(context);
    }

    // The trusted tenant comes from ICurrentTenant, which is populated from the validated principal —
    // never from caller input, and never inferred from which database happens to be reachable.
    if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty)
    {
      return Result.Failure<TenantDbContext>(TenantStorageErrors.TenantContextMissing);
    }

    var created = await factory.CreateAsync(tenantId, cancellationToken);
    if (created.IsFailure)
    {
      return created;
    }

    context = created.Value;
    return created;
  }

  public async Task<TenantDbContext> GetRequiredAsync(CancellationToken cancellationToken = default)
  {
    var resolved = await ResolveAsync(cancellationToken);
    return resolved.IsSuccess
      ? resolved.Value
      : throw new TenantStorageUnavailableException(resolved.Error);
  }

  public async ValueTask DisposeAsync()
  {
    if (disposed)
    {
      return;
    }

    disposed = true;
    if (context is not null)
    {
      await context.DisposeAsync();
      context = null;
    }
  }
}
