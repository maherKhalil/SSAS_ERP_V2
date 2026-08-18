using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Branches;
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
  ITenantWriteFence? writeFence = null,
  // THE BRANCH WRITE BOUNDARY (Branch foundation B1c). Optional for the same reason the fence is:
  // maintenance builders construct this context for schema work, which writes no branch-owned data. Null
  // here makes a branch-owned write fail closed rather than pick a branch — it never permits one.
  //
  // It replaced a plain ICurrentBranch: reading the active branch is a database question (the durable
  // session) and re-authorizing it is another, so it cannot be answered by a synchronous property without
  // either blocking a request thread or trusting a value nobody re-checked.
  IBranchWriteAuthorizer? branchAuthorizer = null,
  // WHICH PHYSICAL DATABASE THIS CONTEXT IS BOUND TO (ADR-020, TS-Storage Phase E4). Captured at creation
  // from the route that chose the connection, and never re-read: a context's database is fixed for its
  // lifetime, which is exactly why a context created before a cutover flip is still pointing at the source
  // afterwards — and why the fence needs to be told, rather than asked to guess from the connection.
  long tenantDatabaseId = 0)
  : PersistenceDbContext(options, currentUser, currentTenant, dateTimeProvider)
{
  public DbSet<Company> Companies => Set<Company>();

  public DbSet<Branch> Branches => Set<Branch>();

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
  //
  // IT HOOKS `SaveChangesAsync(bool, CancellationToken)`, WHICH IS THE ONLY ASYNC PATH TO THE DATABASE.
  // EF Core's `SaveChangesAsync(ct)` calls this overload by virtual dispatch, so both async entry points
  // are fenced here and are fenced EXACTLY ONCE. Overriding the convenience overload as well would take the
  // application lock twice for one write; overriding only the convenience overload — as this type
  // previously did — left `SaveChangesAsync(true, ct)` able to commit against a frozen tenant.
  public override async Task<int> SaveChangesAsync(
    bool acceptAllChangesOnSuccess,
    CancellationToken cancellationToken = default)
  {
    PreventCompanyDeletion();

    // ---- BRANCH AUTHORIZATION, BEFORE ANYTHING ELSE TOUCHES THE DATABASE (Branch foundation B1c).
    //
    // Runs only when branch-owned entities are actually in play, so tenant-global writes — Company, Branch
    // itself — are unaffected and remain possible before any branch has been selected. When it does run it
    // is authoritative: the active branch comes from the durable session and is re-authorized against the
    // resolver on EVERY save, because access, authority and branch state can all change inside a session.
    await ApplyBranchRulesAsync(cancellationToken);

    // CurrentTenantId comes from the base rather than capturing the parameter: the same trusted tenant the
    // global query filter uses, so the fence and the filter can never disagree about who is writing.
    if (writeFence is null || CurrentTenantId is not { } tenantId || tenantId == Guid.Empty ||
      !ChangeTracker.HasChanges())
    {
      return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    if (Database.CurrentTransaction is { } ambient)
    {
      await writeFence.AdmitWriteAsync(
        tenantId, tenantDatabaseId, Database.GetDbConnection(), ambient.GetDbTransaction(), cancellationToken);
      return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
    await writeFence.AdmitWriteAsync(
      tenantId, tenantDatabaseId, Database.GetDbConnection(), transaction.GetDbTransaction(), cancellationToken);

    var written = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return written;
  }

  // SYNCHRONOUS TENANT PERSISTENCE IS REFUSED, NOT SILENTLY UNFENCED (option B).
  //
  // The fence is a real SQL round trip — `sys.sp_getapplock` on the tenant's own connection — and there is
  // no correct synchronous form of it here: blocking on the async path would be sync-over-async on a
  // request thread, and a second synchronous SQL path would be a duplicate of the one mechanism the whole
  // freeze depends on. Every tenant writer in this codebase is already async (ITenantUnitOfWork exposes
  // only SaveChangesAsync), so nothing legitimate reaches this.
  //
  // It throws rather than falling through to base, because falling through is precisely the unfenced path
  // that would let a write commit against a frozen tenant. This overload covers `SaveChanges()` too, which
  // EF Core routes here by virtual dispatch.
  public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
    throw new InvalidOperationException(
      "Synchronous SaveChanges is not supported on TenantDbContext: the cutover write fence (ADR-020) " +
      "cannot be enforced synchronously. Use SaveChangesAsync.");

  // ---- THE BRANCH OWNERSHIP BOUNDARY (Branch foundation B0/B1).
  //
  // IT TOUCHES ONLY IBranchOwnedEntity. Tenant-global data — Branch itself, Company — is unaffected, which
  // is what lets a tenant administrator create the very first branch: demanding an active branch context in
  // order to write a Branch would be unsatisfiable by construction.
  //
  // IT RUNS ALONGSIDE THE EXISTING RULES, NOT INSTEAD OF THEM. This is called from the one async SaveChanges
  // funnel, before the cutover fence and before base.SaveChangesAsync applies the tenant guard and audit
  // stamping — so branch enforcement composes with Phase E rather than bypassing it, and every hook still
  // runs exactly once.
  //
  // IT IS DELIBERATELY NOT AN AUTHORIZATION CHECK. Whether the user may enter this branch is answered by
  // ITenantBranchAccessResolver against the platform database; this is the ownership stamp, and a context
  // that reached here without a branch has no business writing branch-owned rows regardless of authority.
  private async Task ApplyBranchRulesAsync(CancellationToken cancellationToken)
  {
    var entries = ChangeTracker.Entries<IBranchOwnedEntity>()
      .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
      .ToArray();

    // Nothing branch-owned is being written, so no branch is needed. This is what keeps first-branch
    // onboarding and all tenant-global administration possible with no branch selected.
    if (entries.Length == 0)
    {
      return;
    }

    if (branchAuthorizer is null || CurrentTenantId is not { } tenantId)
    {
      throw new InvalidOperationException(
        "A trusted branch context is required to save branch-owned entities.");
    }

    // ---- ONE AUTHORITATIVE ANSWER PER SAVE, used for every entry below. Asked once rather than per entity
    // so a single save cannot straddle two answers, and so the cost is one check per write rather than one
    // per row.
    var authorized = await branchAuthorizer.AuthorizeCurrentBranchAsync(tenantId, cancellationToken);
    if (authorized.IsFailure)
    {
      throw new Persistence.TenantErp.TenantStorageUnavailableException(authorized.Error);
    }

    var branchId = authorized.Value;

    foreach (var entry in entries)
    {
      switch (entry.State)
      {
        case EntityState.Added:
          AssignBranch(entry.Entity, branchId);
          break;

        // BRANCH OWNERSHIP IS IMMUTABLE, exactly as tenant ownership is. Moving a document between
        // branches by editing the column would relocate history with no record that it moved; if that ever
        // becomes a real operation it needs to be an explicit, audited transfer rather than an update.
        case EntityState.Modified when entry.Property(nameof(IBranchOwnedEntity.BranchId)).IsModified:
          throw new InvalidOperationException(
            "Branch ownership cannot be changed after an entity is created.");

        // ---- MODIFYING OR DELETING SOMEONE ELSE'S BRANCH IS STILL A CROSS-BRANCH WRITE. Authorizing only
        // inserts would let a user in Riyadh edit or delete Jeddah's rows, which is the same breach as
        // creating one there.
        case EntityState.Modified or EntityState.Deleted when entry.Entity.BranchId != branchId:
          throw new InvalidOperationException(
            "Branch ownership must match the trusted branch context.");

        default:
          break;
      }
    }
  }

  private static void AssignBranch(IBranchOwnedEntity entity, Guid branchId)
  {
    if (entity.BranchId == Guid.Empty)
    {
      entity.BranchId = branchId;
      return;
    }

    // A CALLER-SUPPLIED BranchId IS ONLY EVER CONFIRMED, NEVER TRUSTED. An entity arriving with a branch
    // that is not the active one is a write aimed at somewhere the caller is not — refused rather than
    // quietly rewritten, because silently correcting it would hide the attempt.
    if (entity.BranchId != branchId)
    {
      throw new InvalidOperationException("Branch ownership must match the trusted branch context.");
    }
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
