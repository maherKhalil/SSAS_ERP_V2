using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// ADR-018 schema health and traffic gating. The gate is a pure function of route + clock, so the whole
// gating table can be asserted here without a database and then re-proven against real SQL.
public sealed class TenantDatabaseSchemaHealthTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

  // ---- Compatibility classification. Whole histories are compared, not endpoints: comparing only the
  // latest applied against the latest known cannot tell "behind" from "divergent", and those need
  // opposite responses.

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void An_exact_history_match_is_up_to_date() =>
    Assert.Equal(
      TenantDatabaseSchemaCompatibilityStatus.UpToDate,
      TenantDatabaseSchemaHealthService.Classify(["M1", "M2"], ["M1", "M2"]));

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void A_database_missing_later_migrations_is_pending() =>
    Assert.Equal(
      TenantDatabaseSchemaCompatibilityStatus.PendingMigrations,
      TenantDatabaseSchemaHealthService.Classify(["M1"], ["M1", "M2"]));

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void An_empty_history_is_pending_not_up_to_date()
  {
    // A fresh database with no tenant history has everything to apply. Classifying "nothing applied"
    // against "nothing known" is the only case where empty is UpToDate.
    Assert.Equal(
      TenantDatabaseSchemaCompatibilityStatus.PendingMigrations,
      TenantDatabaseSchemaHealthService.Classify([], ["M1"]));
    Assert.Equal(
      TenantDatabaseSchemaCompatibilityStatus.UpToDate,
      TenantDatabaseSchemaHealthService.Classify([], []));
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void A_database_carrying_every_known_migration_plus_more_is_ahead_of_the_application() =>
    // An older instance must never serve — or migrate — a newer database.
    Assert.Equal(
      TenantDatabaseSchemaCompatibilityStatus.AheadOfApplication,
      TenantDatabaseSchemaHealthService.Classify(["M1", "M2", "M3"], ["M1", "M2"]));

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void An_unknown_migration_alongside_missing_known_ones_is_a_history_mismatch() =>
    // Divergent lineage rather than merely newer: migrations must not be appended blindly on top.
    Assert.Equal(
      TenantDatabaseSchemaCompatibilityStatus.MigrationHistoryMismatch,
      TenantDatabaseSchemaHealthService.Classify(["M1", "MX"], ["M1", "M2", "M3"]));

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void A_gap_in_an_otherwise_known_history_is_a_mismatch() =>
    // Every applied migration is known, but they are not a prefix: M2 was skipped. Topping up would leave
    // the database permanently missing it.
    Assert.Equal(
      TenantDatabaseSchemaCompatibilityStatus.MigrationHistoryMismatch,
      TenantDatabaseSchemaHealthService.Classify(["M1", "M3"], ["M1", "M2", "M3"]));

  // ---- The ADR-018 traffic-gating table.

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void A_healthy_up_to_date_database_allows_traffic() =>
    Assert.True(Gate().Evaluate(Route(), Now).IsSuccess);

  [Theory]
  [Trait("Decision", "ADR-018")]
  [InlineData(TenantDatabaseConnectivityStatus.Unknown)]
  [InlineData(TenantDatabaseConnectivityStatus.Unreachable)]
  [InlineData(TenantDatabaseConnectivityStatus.AuthenticationFailed)]
  public void Any_non_healthy_connectivity_denies(TenantDatabaseConnectivityStatus status)
  {
    var result = Gate().Evaluate(Route(connectivity: status), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantDatabaseUnavailable.Code, result.Error.Code);
  }

  [Theory]
  [Trait("Decision", "ADR-018")]
  [InlineData(TenantDatabaseSchemaCompatibilityStatus.PendingMigrations)]
  [InlineData(TenantDatabaseSchemaCompatibilityStatus.AheadOfApplication)]
  [InlineData(TenantDatabaseSchemaCompatibilityStatus.MigrationHistoryMismatch)]
  public void Every_incompatible_schema_state_denies(TenantDatabaseSchemaCompatibilityStatus status)
  {
    // There is no read-only compatibility mode in V1: a schema the application cannot write correctly is
    // generally one it cannot read correctly either.
    var result = Gate().Evaluate(Route(compatibility: status), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.DatabaseUpgradeRequired.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void An_unverified_schema_denies()
  {
    var result = Gate().Evaluate(Route(compatibility: TenantDatabaseSchemaCompatibilityStatus.Unknown), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.SchemaHealthUnknown.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void A_migrating_database_denies_before_anything_else_is_considered()
  {
    // Checked first: its schema is changing underneath anything we would serve, and the operator-facing
    // reason should say "upgrading" rather than something derived from a mid-migration health snapshot.
    var route = Route(
      execution: TenantDatabaseMigrationExecutionStatus.Migrating,
      connectivity: TenantDatabaseConnectivityStatus.Unreachable,
      compatibility: TenantDatabaseSchemaCompatibilityStatus.Unknown);

    var result = Gate().Evaluate(route, Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.DatabaseUpgrading.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void Connectivity_is_reported_before_compatibility()
  {
    // ADR-018 is explicit that conflating these leaves an operator guessing whether they have a network
    // incident or a bad release.
    var route = Route(
      connectivity: TenantDatabaseConnectivityStatus.Unreachable,
      compatibility: TenantDatabaseSchemaCompatibilityStatus.PendingMigrations);

    Assert.Equal(
      TenantStorageErrors.TenantDatabaseUnavailable.Code,
      Gate().Evaluate(route, Now).Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void A_stale_but_compatible_status_still_allows_inside_the_hard_stale_bound()
  {
    // The deliberate asymmetry: denying on staleness would let a failure of the background checker take
    // down an otherwise healthy estate.
    var route = Route(lastSchemaCheckUtc: Now.AddMinutes(-30));

    Assert.True(Gate().Evaluate(route, Now).IsSuccess);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void Past_the_hard_stale_bound_even_a_compatible_status_denies()
  {
    var route = Route(lastSchemaCheckUtc: Now.AddHours(-2));

    var result = Gate().Evaluate(route, Now);

    Assert.True(result.IsFailure);
    Assert.Equal(TenantStorageErrors.SchemaHealthStale.Code, result.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void Staleness_never_upgrades_a_known_bad_database()
  {
    // The asymmetry only ever extends an ALLOW. A stale incompatible status must still deny, and for the
    // compatibility reason rather than the staleness one.
    var route = Route(
      compatibility: TenantDatabaseSchemaCompatibilityStatus.PendingMigrations,
      lastSchemaCheckUtc: Now.AddDays(-7));

    Assert.Equal(
      TenantStorageErrors.DatabaseUpgradeRequired.Code,
      Gate().Evaluate(route, Now).Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void An_up_to_date_status_with_no_check_timestamp_denies()
  {
    // Incoherent state — a verdict with nothing dating it — is treated as unverified rather than trusted.
    var route = Route(omitSchemaCheckTimestamp: true);

    Assert.Equal(
      TenantStorageErrors.SchemaHealthUnknown.Code,
      Gate().Evaluate(route, Now).Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void A_check_timestamped_in_the_future_denies()
  {
    // A clock problem somewhere; granting unbounded freshness to a row whose age cannot be computed
    // honestly is the wrong direction to fail.
    var route = Route(lastSchemaCheckUtc: Now.AddHours(1));

    Assert.Equal(
      TenantStorageErrors.SchemaHealthStale.Code,
      Gate().Evaluate(route, Now).Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void Gate_denials_never_disclose_infrastructure_detail()
  {
    foreach (var route in new[]
      {
        Route(connectivity: TenantDatabaseConnectivityStatus.Unreachable),
        Route(compatibility: TenantDatabaseSchemaCompatibilityStatus.PendingMigrations),
        Route(execution: TenantDatabaseMigrationExecutionStatus.Migrating)
      })
    {
      var message = Gate().Evaluate(route, Now).Error.Message;
      Assert.DoesNotContain("PrimarySqlServer", message, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("SSAS_Shared_01", message, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("Password", message, StringComparison.OrdinalIgnoreCase);
    }
  }

  // ---- L5: the design-time factory must fail fast rather than silently target a local database.

  [Theory]
  [Trait("Decision", "ADR-018")]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void The_tenant_design_time_factory_fails_fast_without_its_connection_string(string? configured)
  {
    var exception = Assert.Throws<InvalidOperationException>(
      () => TenantDbContextDesignTimeFactory.ResolveConnectionString(configured));

    Assert.Contains(TenantDbContextDesignTimeFactory.ConnectionStringVariable, exception.Message, StringComparison.Ordinal);
    Assert.Contains("no default", exception.Message, StringComparison.OrdinalIgnoreCase);
    // The old silent fallback is what made a forgotten variable look like a successful migration.
    Assert.DoesNotContain("localhost", exception.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  [Trait("Decision", "ADR-018")]
  public void The_tenant_design_time_factory_accepts_a_configured_connection_string() =>
    Assert.Equal("Server=x;Database=y", TenantDbContextDesignTimeFactory.ResolveConnectionString("Server=x;Database=y"));

  private static TenantDatabaseTrafficGate Gate() => new(TenantDatabaseHealthFreshness.Default);

  private static TenantDatabaseRoute Route(
    TenantDatabaseConnectivityStatus connectivity = TenantDatabaseConnectivityStatus.Healthy,
    TenantDatabaseSchemaCompatibilityStatus compatibility = TenantDatabaseSchemaCompatibilityStatus.UpToDate,
    TenantDatabaseMigrationExecutionStatus execution = TenantDatabaseMigrationExecutionStatus.Idle,
    DateTimeOffset? lastSchemaCheckUtc = null,
    // Distinguishes "not specified, use a fresh timestamp" from "explicitly absent", which the default
    // parameter alone cannot express.
    bool omitSchemaCheckTimestamp = false) =>
    new(
      Guid.NewGuid(), 25, "PrimarySqlServer", "SSAS_Shared_01",
      TenantDatabaseHostingMode.PlatformManaged, TenantDatabaseStorageMode.Shared, 1,
      new TenantDatabaseHealth(
        connectivity, Now,
        compatibility, omitSchemaCheckTimestamp ? null : lastSchemaCheckUtc ?? Now,
        execution, TenantDatabaseMigrationManagementMode.AutomaticByPlatform, null, null));
}
