using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Route -> connection -> context (ADR-017).
//
// The three responsibilities stay separate and are composed here, not merged: the RESOLVER answers "which
// database", the CONNECTION FACTORY answers "how do I reach it", and this type answers "give me a context
// bound to it". Keeping them apart is what lets the route object stay free of secrets while credential
// material never leaves Infrastructure.
//
// FAIL CLOSED, WITH NO FALLBACK. Every failure below returns the routing error unchanged. None of them
// substitutes the Platform connection, a default ServerKey, or the shared database. This is the property
// the whole slice rests on: from this commit onward a routing failure is a request failure, and that is
// strictly preferable to a request that silently reads or writes the wrong database.
public sealed class TenantDbContextFactory(
  ITenantDatabaseResolver resolver,
  ITenantDatabaseConnectionFactory connectionFactory,
  ITenantDatabaseTrafficGate trafficGate,
  ICurrentUser currentUser,
  ICurrentTenant currentTenant,
  IDateTimeProvider dateTimeProvider,
  ITenantWriteFence writeFence,
  // Optional so every existing construction site — tests and maintenance paths included — keeps working
  // and simply has no active branch, which is the correct answer for them.
  IBranchWriteAuthorizer? branchAuthorizer = null,
  // Optional for exactly the same reason (FP-006C1). A context built without one has no company context,
  // which refuses every company-owned write rather than permitting one.
  ICompanyWriteAuthorizer? companyAuthorizer = null,
  // Optional again (FP-006C2). A context built without one authorizes no branch transfer at all, which
  // leaves the original immutability invariant fully in force.
  IBranchTransferAuthorizer? branchTransferAuthorizer = null) : ITenantDbContextFactory
{
  public async Task<Result<TenantDbContext>> CreateAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    // Routing is resolved on EVERY context creation, never captured at registration (ADR-017 binding
    // lifetime rules 1 and 2). Since TS-Storage Phase E2 the injected resolver is the version-aware one, so
    // this call may be answered from a process-local entry — but only one whose RoutingVersion still matches
    // the authoritative value, which is read on every resolution. A context therefore cannot be built on a
    // route that has been superseded, and a Platform outage refuses rather than serving a remembered route.
    var route = await resolver.ResolveAsync(tenantId, cancellationToken);
    if (route.IsFailure)
    {
      return Result.Failure<TenantDbContext>(route.Error);
    }

    // ADR-018 traffic gating, applied BEFORE any connection is built. A database that is unreachable,
    // unverified, behind, ahead, history-mismatched, or currently migrating does not serve ERP traffic —
    // and the denial is a controlled TenantStorage.* result rather than a raw SqlException surfacing from
    // somewhere deeper. Nothing here migrates, and nothing falls back to another database.
    var gate = trafficGate.Evaluate(route.Value, dateTimeProvider.UtcNow);
    if (gate.IsFailure)
    {
      return Result.Failure<TenantDbContext>(gate.Error);
    }

    // The connection factory refuses CustomerManaged independently of the resolver, and refuses a
    // ServerKey absent from trusted configuration. Going through it — rather than building provider
    // options here — is what keeps that single choke point authoritative.
    var connection = connectionFactory.Create(route.Value);
    if (connection.IsFailure)
    {
      return Result.Failure<TenantDbContext>(connection.Error);
    }

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connection.Value, contextOwnsConnection: true, sql => sql.MigrationsHistoryTable(
        TenantPersistenceConstants.MigrationHistoryTable,
        TenantPersistenceConstants.MigrationHistorySchema))
      .Options;

    // The context owns the connection, so disposing the context closes it. Without this a routed context
    // would leak one pooled connection per request.
    //
    // The route's TenantDatabaseId travels with the context (TS-Storage Phase E4) so the write fence can
    // tell a writer bound to the cutover SOURCE from one bound to the TARGET. It is captured here, at the
    // moment routing was resolved, which is precisely what makes a context created before a flip still
    // identify itself as the source afterwards.
    // The ACTIVE BRANCH travels with the context too (Branch foundation B0/B1). Like the tenant, it is an
    // ambient server-side fact rather than something a caller passes per write; null means no branch has
    // been selected yet, which the write boundary turns into a refusal for branch-owned data only.
    return Result.Success(new TenantDbContext(
      options, currentUser, currentTenant, dateTimeProvider, writeFence, branchAuthorizer, companyAuthorizer,
      branchTransferAuthorizer, route.Value.TenantDatabaseId));
  }
}
