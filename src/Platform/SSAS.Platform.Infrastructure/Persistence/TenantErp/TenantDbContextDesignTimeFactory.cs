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
// The connection string here is used only when a developer runs a design-time command against a database;
// `SSAS_TENANT_MIGRATION_SQLSERVER` overrides it. Applying migrations to real tenant databases at runtime
// goes through TenantMigrationRunner, which resolves a genuine route.
public sealed class TenantDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
  public TenantDbContext CreateDbContext(string[] args)
  {
    var connectionString =
      Environment.GetEnvironmentVariable("SSAS_TENANT_MIGRATION_SQLSERVER") ??
      "Server=localhost;Database=SSAS_ERP_TenantDesignTime;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
        TenantPersistenceConstants.MigrationHistoryTable,
        TenantPersistenceConstants.MigrationHistorySchema))
      .Options;

    return new TenantDbContext(options, DesignTimeUser.Instance, DesignTimeTenant.Instance, DesignTimeClock.Instance);
  }

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
