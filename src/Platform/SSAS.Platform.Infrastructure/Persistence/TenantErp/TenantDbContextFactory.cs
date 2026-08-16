using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
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
  ITenantWriteFence writeFence) : ITenantDbContextFactory
{
  public async Task<Result<TenantDbContext>> CreateAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    // Routing is resolved on EVERY context creation, never cached and never captured at registration
    // (ADR-017 binding lifetime rules 1 and 2). RoutingVersion is carried on the route for the day a
    // cache exists; until then freshness is guaranteed by simply reading current state each time.
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
    return Result.Success(new TenantDbContext(
      options, currentUser, currentTenant, dateTimeProvider, writeFence));
  }
}
