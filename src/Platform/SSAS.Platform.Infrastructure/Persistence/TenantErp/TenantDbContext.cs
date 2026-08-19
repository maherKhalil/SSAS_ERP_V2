using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.BuildingBlocks.Tenancy.Branches;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Companies;
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
  // THE COMPANY WRITE BOUNDARY (FP-006C1, ADR-025 decision 9). Optional for exactly the reasons the branch
  // authorizer is: maintenance builders construct this context for schema work, which writes no
  // company-owned data. Null here makes a company-owned write fail closed rather than pick a company — it
  // never permits one.
  //
  // A SEPARATE AUTHORIZER FROM THE BRANCH ONE, deliberately. Company and Branch are independent dimensions,
  // and one authorizer answering both would make it possible for a change in either to widen the other.
  ICompanyWriteAuthorizer? companyAuthorizer = null,
  // THE SANCTIONED BRANCH-TRANSFER CHANNEL (FP-006C2, ADR-024 decision 3). Optional for the same reason the
  // other two are: maintenance builders write no branch-owned data.
  //
  // NULL MEANS NO TRANSFER IS EVER AUTHORIZED, never that one is assumed. A context built without it keeps
  // the original invariant in full — ordinary BranchId mutation is refused, with no exception available.
  IBranchTransferAuthorizer? branchTransferAuthorizer = null,
  // THE BUSINESS MODULES' CONTRIBUTIONS TO THE TENANT MODEL (FP-006C3-pre, ADR-012).
  //
  // Tenant business data lives in ONE context and ONE migration stream (ADR-017), but Platform may not
  // reference HR or GL to map their entities. Each module supplies its own mapping through
  // ITenantModelContributor, which neither side owns, and the Host registers the set.
  //
  // EMPTY IS THE CORRECT DEFAULT for maintenance and schema tooling, which reason about Platform's own
  // tenant entities only. It is not a degraded mode — it is a genuinely different model, and
  // TenantModelCacheKeyFactory is what keeps EF from confusing the two.
  IEnumerable<ITenantModelContributor>? modelContributors = null,
  // WHICH PHYSICAL DATABASE THIS CONTEXT IS BOUND TO (ADR-020, TS-Storage Phase E4). Captured at creation
  // from the route that chose the connection, and never re-read: a context's database is fixed for its
  // lifetime, which is exactly why a context created before a cutover flip is still pointing at the source
  // afterwards — and why the fence needs to be told, rather than asked to guess from the connection.
  long tenantDatabaseId = 0)
  : PersistenceDbContext(options, currentUser, currentTenant, dateTimeProvider)
{
  public DbSet<Company> Companies => Set<Company>();

  public DbSet<Branch> Branches => Set<Branch>();

  // THE CONTRIBUTOR SET THIS CONTEXT'S MODEL WAS BUILT FROM (FP-006C3-pre).
  //
  // Ordered and de-duplicated so registration order cannot produce two cache entries for the same model, and
  // exposed for TenantModelCacheKeyFactory alone — it is model metadata, not a capability.
  internal string ModelSignature { get; } = modelContributors is null
    ? string.Empty
    : string.Join(
      "|",
      modelContributors
        .Where(contributor => contributor is not null)
        .Select(contributor => contributor.GetType().FullName ?? contributor.GetType().Name)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal));

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    ArgumentNullException.ThrowIfNull(optionsBuilder);

    // INSTALLED HERE RATHER THAN AT EVERY OPTION-BUILDING SITE, deliberately. Options for this context are
    // built by the routed factory, by the maintenance builders, and by tests; a replacement any one of them
    // forgot would silently reintroduce the shared-model bug this factory exists to prevent.
    optionsBuilder.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();

    base.OnConfiguring(optionsBuilder);
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    // Only tenant configurations are applied. The two contexts share an assembly today, so an unfiltered
    // ApplyConfigurationsFromAssembly would pull every platform entity into the tenant model (and the
    // reverse) — the configuration namespace is what keeps the two models disjoint.
    modelBuilder.ApplyConfigurationsFromAssembly(
      typeof(TenantDbContext).Assembly,
      type => type.Namespace == TenantPersistenceConstants.ConfigurationNamespace);

    // ---- THE BUSINESS MODULES MAP THEIR OWN ENTITIES (ADR-012).
    //
    // Applied BEFORE base.OnModelCreating so contributed entities are visible to the shared conventions
    // that run there — the global tenant query filter and the restricted delete behaviour. A module entity
    // added afterwards would be unfiltered, which is exactly the silent cross-tenant leak the filter exists
    // to prevent.
    foreach (var contributor in modelContributors ?? [])
    {
      contributor.Configure(modelBuilder);
    }

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
    PreventAppendOnlyMutation();

    // ---- COMPANY AUTHORIZATION, BEFORE BRANCH AND BEFORE ANYTHING TOUCHES THE DATABASE (FP-006C1).
    //
    // ORDER: tenant -> company -> branch -> persistence. Company runs BEFORE branch because the two
    // dimensions must be settled outside-in and independently: a save that is refused on company grounds
    // must never have had its branch authorized first, or a log would record branch admission for a write
    // that was never permitted to exist. Neither call can widen the other — each resolves its own dimension
    // from its own authority and returns only its own identifier — and running company first means the
    // cheaper, request-scoped refusal happens before the durable-session read that branch requires.
    //
    // The TENANT boundary bookends both: CurrentTenantId is the trusted tenant both rules verify against
    // here, and the tenant STAMP plus the post-creation immutability guard run in base.SaveChangesAsync
    // below. An Added entity therefore still carries an unstamped TenantId at this point, which is why
    // neither rule reads it — both authorize against the ambient trusted tenant instead, and AssignTenant
    // independently refuses a mismatched one afterwards.
    await ApplyCompanyRulesAsync(cancellationToken);

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

    // ---- THE SANCTIONED TRANSFER, RE-VALIDATED NOW (FP-006C2, ADR-024 decision 3).
    //
    // Asked before the execution branch because the answer changes how the declared SOURCE is judged below,
    // and because a declaration that is no longer valid should refuse the save on its own terms rather than
    // surface later as a confusing ordinary refusal.
    //
    // A null result means no transfer is open, which is not a failure — the ordinary rules below then refuse
    // every BranchId modification exactly as before. A FAILURE means a declaration is open and is no longer
    // valid, and that refuses the save rather than quietly falling through to the ordinary refusal.
    BranchTransferDeclaration? transfer = null;
    if (branchTransferAuthorizer is not null)
    {
      var openTransfer = await branchTransferAuthorizer.AuthorizeOpenTransferAsync(tenantId, cancellationToken);
      if (openTransfer.IsFailure)
      {
        throw new Persistence.TenantErp.TenantStorageUnavailableException(openTransfer.Error);
      }

      transfer = openTransfer.Value;
    }

    // ---- ONE AUTHORITATIVE ANSWER PER SAVE, used for every entry below. Asked once rather than per entity
    // so a single save cannot straddle two answers, and so the cost is one check per write rather than one
    // per row.
    //
    // ASKED EVEN FOR AN INACTIVE-SOURCE RECOVERY, and that is deliberate. This call is also where the
    // DURABLE SESSION is re-read — status and expiry included — so skipping it for the recovery path would
    // let a revoked or expired session keep transferring. The recovery relaxes exactly one thing, below:
    // which branch the entity may be leaving. It does not relax who the caller is, whether their session is
    // still usable, or whether they hold a valid branch context at all.
    var authorized = await branchAuthorizer.AuthorizeCurrentBranchAsync(tenantId, cancellationToken);
    if (authorized.IsFailure)
    {
      throw new Persistence.TenantErp.TenantStorageUnavailableException(authorized.Error);
    }

    var branchId = authorized.Value;

    // AN ORDINARY TRANSFER LEAVES THE BRANCH THE CALLER IS ACTUALLY IN. The declaration does not get to name
    // a source the caller has not been proven to hold; this is where the two facts are joined.
    //
    // The recovery is the one exception (ADR-024 decision 12): its source is an INACTIVE branch, which no
    // principal can hold as an execution context, so requiring the two to match would refuse the very
    // operation that exists to recover from that state. Every other guard still applies — the transfer
    // authorizer has already re-verified administrator authority and that the source really is inactive.
    if (transfer is not null &&
      transfer.Mode != BranchTransferMode.InactiveSourceRecovery &&
      transfer.SourceBranchId != branchId)
    {
      throw new InvalidOperationException(
        "A branch transfer must originate in the trusted branch context.");
    }

    foreach (var entry in entries)
    {
      // THE ONE EXCEPTION, AND IT IS PER ENTRY. A declaration authorizes exactly the entity it names moving
      // exactly the way it names; every other entry in the same save — including another entity moving
      // between the same two branches — falls through to the ordinary rules and is refused.
      if (transfer is not null && IsSanctionedTransfer(entry, transfer))
      {
        continue;
      }

      switch (entry.State)
      {
        case EntityState.Added:
          AssignBranch(entry.Entity, branchId);
          break;

        // BRANCH OWNERSHIP IS IMMUTABLE, exactly as tenant ownership is. Moving a document between
        // branches by editing the column would relocate history with no record that it moved; that is why
        // a transfer must be the explicit, audited operation above rather than a property assignment.
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

  // Does the open declaration authorize exactly this change? (FP-006C2, ADR-024 decision 3.)
  //
  // THE SOURCE IS READ FROM THE ORIGINAL VALUE, not the current one: the caller has already overwritten
  // BranchId with the destination by the time the change tracker sees it, so comparing the current value
  // against the source would never match and comparing it against nothing would match everything.
  //
  // Only a MODIFIED entry whose BranchId actually changed can be a transfer. An Added entity is stamped, and
  // a Deleted one is not moving anywhere.
  private static bool IsSanctionedTransfer(
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IBranchOwnedEntity> entry,
    BranchTransferDeclaration transfer)
  {
    if (entry.State != EntityState.Modified)
    {
      return false;
    }

    var branchProperty = entry.Property(nameof(IBranchOwnedEntity.BranchId));

    return branchProperty.IsModified &&
      branchProperty.OriginalValue is Guid originalBranchId &&
      transfer.Authorizes(entry.Entity, originalBranchId, entry.Entity.BranchId);
  }

  // ---- THE COMPANY OWNERSHIP BOUNDARY (FP-006C1, ADR-025 decision 9).
  //
  // IT TOUCHES ONLY ICompanyOwnedEntity. Tenant-global data — Company itself, Branch — is unaffected, which
  // is what lets an administrator create the very first company: demanding a company context in order to
  // write a Company would be unsatisfiable by construction, exactly as it would be for the first branch.
  //
  // IT IS THE SAME SHAPE AS THE BRANCH RULE AND A SEPARATE DECISION FROM IT. Company and Branch are sibling
  // dimensions beneath the tenant (ADR-023, ADR-025): an entity may be owned along either, both or neither,
  // and neither authorization can substitute for or widen the other. Sharing one code path would make that
  // independence an implementation detail rather than a boundary.
  //
  // IT IS DELIBERATELY NOT THE FUNCTIONAL PERMISSION CHECK. Whether the user may perform this OPERATION is
  // answered by the ordinary permission pipeline; this is the ownership stamp and the scope refusal.
  private async Task ApplyCompanyRulesAsync(CancellationToken cancellationToken)
  {
    var entries = ChangeTracker.Entries<ICompanyOwnedEntity>()
      .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
      .ToArray();

    // Nothing company-owned is being written, so no company is needed. This is what keeps company creation
    // and all tenant-global administration possible with no company selected.
    if (entries.Length == 0)
    {
      return;
    }

    if (companyAuthorizer is null || CurrentTenantId is not { } tenantId)
    {
      throw new InvalidOperationException(
        "A trusted company context is required to save company-owned entities.");
    }

    // ---- ONE AUTHORITATIVE ANSWER PER SAVE, used for every entry below. Asked once rather than per entity
    // so a single save cannot straddle two answers, and so the cost is one check per write rather than one
    // per row — the same economy the branch rule makes.
    var authorized = await companyAuthorizer.AuthorizeCurrentCompanyAsync(tenantId, cancellationToken);
    if (authorized.IsFailure)
    {
      throw new Persistence.TenantErp.TenantStorageUnavailableException(authorized.Error);
    }

    var companyId = authorized.Value;

    foreach (var entry in entries)
    {
      switch (entry.State)
      {
        case EntityState.Added:
          AssignCompany(entry.Entity, companyId);
          break;

        // COMPANY OWNERSHIP IS IMMUTABLE, exactly as tenant ownership is, and unlike branch there is no
        // sanctioned transfer: an employee does not move between legal entities, they are employed by a
        // different one. Reassigning a record's company by editing the column would relocate it across a
        // legal boundary with no record that it moved.
        case EntityState.Modified when entry.Property(nameof(ICompanyOwnedEntity.CompanyId)).IsModified:
          throw new InvalidOperationException(
            "Company ownership cannot be changed after an entity is created.");

        // ---- MODIFYING OR DELETING ANOTHER COMPANY'S ROW IS STILL A CROSS-COMPANY WRITE. Authorizing only
        // inserts would let a user acting within one legal entity edit or delete another's records, which is
        // the same breach as creating one there.
        case EntityState.Modified or EntityState.Deleted when entry.Entity.CompanyId != companyId:
          throw new InvalidOperationException(
            "Company ownership must match the trusted company context.");

        default:
          break;
      }
    }
  }

  private static void AssignCompany(ICompanyOwnedEntity entity, Guid companyId)
  {
    if (entity.CompanyId == Guid.Empty)
    {
      entity.CompanyId = companyId;
      return;
    }

    // A CALLER-SUPPLIED CompanyId IS ONLY EVER CONFIRMED, NEVER TRUSTED. An entity arriving with a company
    // that is not the trusted one is a write aimed at a legal entity the caller is not acting within —
    // refused rather than quietly rewritten, because silently correcting it would hide the attempt.
    if (entity.CompanyId != companyId)
    {
      throw new InvalidOperationException("Company ownership must match the trusted company context.");
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

  // ---- APPEND-ONLY RECORDS ARE NEVER UPDATED AND NEVER DELETED (FP-006C3, ADR-024 decision 5).
  //
  // Enforced centrally rather than by the absence of a repository method, because the absence of a method
  // protects only the callers who use the repository. A record of what happened that can be edited
  // afterwards is not a record of what happened, and Employee branch history is the first of them.
  //
  // The refusal names no entity type: it is a rule about a classification, and the message a caller sees
  // should describe the rule rather than the row.
  private void PreventAppendOnlyMutation()
  {
    var mutated = ChangeTracker.Entries<IAppendOnlyEntity>()
      .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

    if (mutated)
    {
      throw new InvalidOperationException(
        "Append-only records cannot be modified or deleted after they are written.");
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
