using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Builds a TenantDbContext over an ALREADY-CONSTRUCTED trusted connection, for schema health and migration
// (ADR-018).
//
// This is the narrow maintenance exception ADR-017 permits: tooling that operates outside a tenant context
// deliberately and under review, never on the request path. It reads and applies MIGRATION metadata only —
// it never queries a tenant-owned entity, which is why the absence of a trusted tenant is safe here and
// would not be anywhere else. The tenant is deliberately null rather than a placeholder, so any accidental
// entity query fails closed against the global filter instead of returning another tenant's rows.
//
// It does NOT construct connections. Callers obtain one from ITenantDatabaseConnectionFactory so there is
// exactly one trusted path to a tenant database.
internal static class TenantDbContextBuilder
{
  // The deployed Tenant migration catalog — EF's own list, so it cannot drift from what is shipped.
  public static IReadOnlyList<string> KnownMigrations { get; } = ReadKnownMigrations();

  public static TenantDbContext ForConnection(SqlConnection connection)
  {
    ArgumentNullException.ThrowIfNull(connection);

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
        TenantPersistenceConstants.MigrationHistoryTable,
        TenantPersistenceConstants.MigrationHistorySchema))
      .Options;

    return new TenantDbContext(
      options, MaintenanceUser.Instance, MaintenanceTenant.Instance, MaintenanceClock.Instance);
  }

  private static IReadOnlyList<string> ReadKnownMigrations()
  {
    // A throwaway context built only to read the migration catalog. The connection string is never opened:
    // GetMigrations() is model/assembly metadata, not a database round trip.
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=catalog-only;Database=catalog-only;Integrated Security=True")
      .Options;
    using var context = new TenantDbContext(
      options, MaintenanceUser.Instance, MaintenanceTenant.Instance, MaintenanceClock.Instance);
    return [.. context.Database.GetMigrations()];
  }

  private sealed class MaintenanceUser : ICurrentUser
  {
    public static readonly MaintenanceUser Instance = new();

    public string? UserId => "tenant-storage-maintenance";

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class MaintenanceTenant : ICurrentTenant
  {
    public static readonly MaintenanceTenant Instance = new();

    // Null on purpose — see the type comment.
    public Guid? TenantId => null;
  }

  private sealed class MaintenanceClock : IDateTimeProvider
  {
    public static readonly MaintenanceClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
