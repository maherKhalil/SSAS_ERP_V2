using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Tenant ERP persistence (ADR-017). A distinct context from PlatformDbContext, NOT a rename of it:
// PlatformDbContext keeps the platform plane (tenants, identity, membership, roles, localization,
// platform-support authority, and the tenant-storage registry itself), while this context owns tenant
// business data and is the only context whose connection is chosen dynamically per tenant.
//
// It inherits PersistenceDbContext deliberately, so the load-bearing guarantees are the SAME code rather
// than a second implementation that could drift: auditing, the ITenantOwnedEntity global query filter,
// the insert/update tenant guard, and the global DeleteBehavior.Restrict.
//
// The tenant filter is retained in EVERY placement, including a dedicated database holding one tenant.
// Physical isolation is additional protection, never a replacement for logical TenantId enforcement
// (ADR-017 "Global query filtering"): a database relying on physical separation alone could not be moved
// back onto shared storage, and would silently lose its isolation guarantee if it were.
//
// THE MODEL IS TENANT-INVARIANT. Nothing here varies by TenantId, DatabaseName, ServerKey, StorageMode,
// HostingMode or RoutingVersion. EF caches the model per options instance, so a tenant-conditional model
// would let one tenant's model serve another (ADR-017 binding lifetime rule 3). Routing changes which
// connection this context talks to; it never changes the shape of what it maps.
public sealed class TenantDbContext(
  DbContextOptions<TenantDbContext> options,
  ICurrentUser currentUser,
  ICurrentTenant currentTenant,
  IDateTimeProvider dateTimeProvider,
  // The cutover write fence (ADR-020). Optional because the maintenance builders construct this context
  // outside any tenant context for schema work, which is not an application write.
  ITenantWriteFence? writeFence = null)
  : PersistenceDbContext(options, currentUser, currentTenant, dateTimeProvider)
{
  public DbSet<Company> Companies => Set<Company>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    // Only tenant configurations are applied. The two contexts share an assembly today, so an unfiltered
    // ApplyConfigurationsFromAssembly would pull every platform entity into the tenant model (and the
    // reverse) — the configuration namespace is what keeps the two models disjoint.
    modelBuilder.ApplyConfigurationsFromAssembly(
      typeof(TenantDbContext).Assembly,
      type => type.Namespace == TenantPersistenceConstants.ConfigurationNamespace);

    base.OnModelCreating(modelBuilder);
  }

  // THE TENANT APPLICATION WRITE BOUNDARY (ADR-020 "The freeze covers every writer").
  //
  // Every application write reaches SQL Server through here — repositories track, the unit of work saves,
  // and this is the single method underneath all of it. Enforcing the freeze at this point rather than in
  // HTTP middleware is what makes it cover jobs, consumers, imports and workflows as well as requests: a
  // writer is blocked wherever it originates, because there is no second path to tenant persistence.
  //
  // The fence needs the write to be inside a transaction it can lock against, so one is opened when the
  // caller has not already opened one. When the caller HAS, the fence joins it and the caller keeps
  // ownership of the commit.
  public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    PreventCompanyDeletion();

    // CurrentTenantId comes from the base rather than capturing the parameter: the same trusted tenant the
    // global query filter uses, so the fence and the filter can never disagree about who is writing.
    if (writeFence is null || CurrentTenantId is not { } tenantId || tenantId == Guid.Empty ||
      !ChangeTracker.HasChanges())
    {
      return await base.SaveChangesAsync(cancellationToken);
    }

    if (Database.CurrentTransaction is { } ambient)
    {
      await writeFence.AdmitWriteAsync(
        tenantId, Database.GetDbConnection(), ambient.GetDbTransaction(), cancellationToken);
      return await base.SaveChangesAsync(cancellationToken);
    }

    await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
    await writeFence.AdmitWriteAsync(
      tenantId, Database.GetDbConnection(), transaction.GetDbTransaction(), cancellationToken);

    var written = await base.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return written;
  }

  // Carried over from PlatformDbContext together with Company: lifecycle is expressed by Archive, never
  // by a physical delete, so history stays reconstructable.
  private void PreventCompanyDeletion()
  {
    if (ChangeTracker.Entries<Company>().Any(entry => entry.State == EntityState.Deleted))
    {
      throw new InvalidOperationException("Company rows cannot be physically deleted; use the Archive lifecycle transition.");
    }
  }
}
