using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Design-time factory for `dotnet ef ... --context TenantDbContext` (ADR-018).
//
// Migration TOOLING is separate from runtime routing on purpose. At design time there is no tenant, no
// assignment and no route — the tool only needs the model in order to scaffold or script a migration, so
// it must never go through the resolver. ADR-017 rule 7 also keeps migration context and options separate
// from runtime context and options.
//
// The target database comes from `SSAS_TENANT_MIGRATION_SQLSERVER` and there is deliberately NO fallback.
// An earlier version defaulted to a local database when the variable was absent, which was tolerable while
// the command lived only in a developer's head. It stopped being tolerable once ADR-018 published the
// command as operational procedure: an operator who omitted the variable would get an apparently
// successful `database update` against a stray local database while the intended tenant database stayed
// unmigrated. Failing fast — matching PlatformDesignTimeDbContextFactory — makes that mistake loud.
//
// Applying migrations to real tenant databases at runtime goes through the migration orchestrator, which
// resolves a genuine route.
public sealed class TenantDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
  public const string ConnectionStringVariable = "SSAS_TENANT_MIGRATION_SQLSERVER";

  public TenantDbContext CreateDbContext(string[] args)
  {
    var connectionString = ResolveConnectionString(
      Environment.GetEnvironmentVariable(ConnectionStringVariable));

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
        TenantPersistenceConstants.MigrationHistoryTable,
        TenantPersistenceConstants.MigrationHistorySchema))
      .Options;

    return new TenantDbContext(options, DesignTimeUser.Instance, DesignTimeTenant.Instance, DesignTimeClock.Instance);
  }

  // Extracted so the fail-fast contract is directly testable without invoking EF tooling. The message
  // names the variable and nothing else — never a value, which would be a connection string.
  internal static string ResolveConnectionString(string? configuredConnectionString) =>
    string.IsNullOrWhiteSpace(configuredConnectionString)
      ? throw new InvalidOperationException(
        $"{ConnectionStringVariable} is required for tenant design-time migrations. " +
        "Set it to the target tenant database connection string; there is no default.")
      : configuredConnectionString;

  // Design-time stubs. A migration never saves entities, so these are never consulted for auditing or
  // tenant assignment; the tenant is deliberately null so nothing here can be mistaken for a real context.
  private sealed class DesignTimeUser : ICurrentUser
  {
    public static readonly DesignTimeUser Instance = new();

    public string? UserId => null;

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class DesignTimeTenant : ICurrentTenant
  {
    public static readonly DesignTimeTenant Instance = new();

    public Guid? TenantId => null;
  }

  private sealed class DesignTimeClock : IDateTimeProvider
  {
    public static readonly DesignTimeClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
