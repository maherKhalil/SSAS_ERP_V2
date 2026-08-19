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

  // ---- THERE IS DELIBERATELY NO TenantModel HERE ANY MORE (FP-006C6).
  //
  // A static contributor-free model used to live on this type, and the cutover copy engine derived its table
  // manifest from it. That manifest could not contain a module-contributed entity, so a Shared to Dedicated
  // promotion copied Platform's tenant tables, validated cleanly against the tables it knew about, and left
  // HR's behind without a single error.
  //
  // The model now comes from ITenantModelSource, which is built from the REGISTERED contributor set. Removing
  // the static rather than fixing it is the point: a contributor-free tenant model is no longer something a
  // future caller can reach for by accident.

  public static TenantDbContext ForConnection(SqlConnection connection)
  {
    ArgumentNullException.ThrowIfNull(connection);

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
        TenantPersistenceConstants.MigrationHistoryTable,
        TenantPersistenceConstants.MigrationHistorySchema))
      .Options;

    return new TenantDbContext(
      options, MaintenanceIdentity.User, MaintenanceIdentity.Tenant, MaintenanceIdentity.Clock);
  }

  // A schema-only application-model probe needs the real tenant query filters to compile, but it must not
  // choose or inspect any customer tenant. Guid.Empty is a reserved non-customer probe identity, paired with
  // a constant-false predicate by the caller so the database returns no business rows.
  public static TenantDbContext ForSchemaProbeConnection(SqlConnection connection)
  {
    ArgumentNullException.ThrowIfNull(connection);

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
        TenantPersistenceConstants.MigrationHistoryTable,
        TenantPersistenceConstants.MigrationHistorySchema))
      .Options;

    return new TenantDbContext(
      options, MaintenanceIdentity.User, MaintenanceIdentity.SchemaProbeTenant, MaintenanceIdentity.Clock);
  }

  private static IReadOnlyList<string> ReadKnownMigrations()
  {
    // A throwaway context built only to read the migration catalog. The connection string is never opened:
    // GetMigrations() is model/assembly metadata, not a database round trip.
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=catalog-only;Database=catalog-only;Integrated Security=True")
      .Options;
    using var context = new TenantDbContext(
      options, MaintenanceIdentity.User, MaintenanceIdentity.Tenant, MaintenanceIdentity.Clock);
    return [.. context.Database.GetMigrations()];
  }
}
