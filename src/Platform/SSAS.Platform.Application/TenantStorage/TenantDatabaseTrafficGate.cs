using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Application.TenantStorage;

// The ADR-018 traffic-gating table, implemented as a pure function of route + clock.
//
// It is pure on purpose: gating is the rule that keeps requests off a database the application cannot
// correctly serve, and a rule that reads no I/O can be exhaustively tested against the ADR table without
// a database, then re-proven against real SQL.
//
// THE ORDER OF CHECKS IS DELIBERATE. Connectivity is evaluated before compatibility so an unreachable
// database is reported as unreachable rather than as a schema problem — ADR-018 is explicit that
// conflating those two leaves an operator guessing whether they have a network incident or a bad release.
public sealed class TenantDatabaseTrafficGate(TenantDatabaseHealthFreshness freshness) : ITenantDatabaseTrafficGate
{
  public Result Evaluate(TenantDatabaseRoute route, DateTimeOffset nowUtc)
  {
    ArgumentNullException.ThrowIfNull(route);
    var health = route.Health;

    // A migration owns the database: its schema is changing underneath anything we would serve.
    if (health.MigrationExecutionStatus == TenantDatabaseMigrationExecutionStatus.Migrating)
    {
      return Result.Failure(TenantStorageErrors.DatabaseUpgrading);
    }

    // Connectivity first — see above.
    switch (health.ConnectivityStatus)
    {
      case TenantDatabaseConnectivityStatus.Unreachable:
      case TenantDatabaseConnectivityStatus.AuthenticationFailed:
      case TenantDatabaseConnectivityStatus.Unknown:
        return Result.Failure(TenantStorageErrors.TenantDatabaseUnavailable);
    }

    switch (health.SchemaCompatibilityStatus)
    {
      case TenantDatabaseSchemaCompatibilityStatus.Unknown:
        // Pre-verification denies. Absence of evidence is not evidence of compatibility.
        return Result.Failure(TenantStorageErrors.SchemaHealthUnknown);

      case TenantDatabaseSchemaCompatibilityStatus.PendingMigrations:
      case TenantDatabaseSchemaCompatibilityStatus.AheadOfApplication:
      case TenantDatabaseSchemaCompatibilityStatus.MigrationHistoryMismatch:
        // All three deny. There is no read-only compatibility mode in V1 (ADR-018): a schema the
        // application cannot write correctly is generally one it cannot read correctly either.
        return Result.Failure(TenantStorageErrors.DatabaseUpgradeRequired);
    }

    // Only UpToDate reaches the freshness test, which is what makes the asymmetry below safe: a stale
    // status can only ever extend an ALLOW, never rescue a DENY.
    return EvaluateFreshness(health.LastSchemaCheckUtc, nowUtc);
  }

  // ADR-018's deliberate asymmetry: a stale COMPATIBLE result keeps allowing inside the grace window,
  // because denying on staleness would let a failure of the background checker take down an otherwise
  // healthy estate. Past the hard-stale bound it denies regardless — an indefinitely trusted cache is the
  // other half of the same mistake.
  private Result EvaluateFreshness(DateTimeOffset? lastCheckUtc, DateTimeOffset nowUtc)
  {
    if (lastCheckUtc is not { } checkedUtc)
    {
      // UpToDate with no check timestamp is incoherent state; treat it as unverified rather than trusted.
      return Result.Failure(TenantStorageErrors.SchemaHealthUnknown);
    }

    var age = nowUtc.ToUniversalTime() - checkedUtc.ToUniversalTime();

    // A check timestamped in the future means a clock problem somewhere. Deny rather than grant unbounded
    // freshness to a row whose age cannot be computed honestly.
    if (age < TimeSpan.Zero)
    {
      return Result.Failure(TenantStorageErrors.SchemaHealthStale);
    }

    return age > freshness.HardStaleAfter
      ? Result.Failure(TenantStorageErrors.SchemaHealthStale)
      : Result.Success();
  }
}

// Freshness bounds for the gating decision (ADR-018). Exact durations are configuration; the existence of
// both bounds and their asymmetric treatment are binding.
//
// `RefreshAfter` marks a result as due for background refresh while still being served. `HardStaleAfter`
// is the point past which even a compatible result stops being trusted.
public sealed record TenantDatabaseHealthFreshness(TimeSpan RefreshAfter, TimeSpan HardStaleAfter)
{
  public static readonly TenantDatabaseHealthFreshness Default =
    new(TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));

  public bool IsDueForRefresh(DateTimeOffset? lastCheckUtc, DateTimeOffset nowUtc) =>
    lastCheckUtc is not { } checkedUtc || nowUtc.ToUniversalTime() - checkedUtc.ToUniversalTime() > RefreshAfter;
}
