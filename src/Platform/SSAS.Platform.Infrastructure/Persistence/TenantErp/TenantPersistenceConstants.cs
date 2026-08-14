namespace SSAS.Platform.Infrastructure.Persistence.TenantErp;

// Tenant ERP persistence constants (ADR-017 / ADR-018).
//
// The tenant stream is deliberately separate from the platform stream in BOTH schema and migration
// history. Today the shared physical database is the same SQL catalog as the Platform database, so the
// separation is logical; when a tenant moves to a dedicated database the same schema and the same history
// table travel with it unchanged. Sharing `platform.__EFMigrationsHistory` would have made a tenant
// database indistinguishable from a platform database at exactly the point where they diverge.
//
// The namespace is `TenantErp` rather than `Tenant` on purpose: a `...Persistence.Tenant` namespace would
// shadow the `Tenant` entity type inside `PlatformDbContext`, which sits in the parent namespace.
public static class TenantPersistenceConstants
{
  public const string Schema = "tenant";

  public const string MigrationHistoryTable = "__EFMigrationsHistory";

  // Tenant migration history lives in the tenant schema, so a tenant database's applied migrations can
  // never be mistaken for the platform stream's, nor the reverse (ADR-018).
  public const string MigrationHistorySchema = Schema;

  // Same ordinal collation convention as the platform stream: case/accent-sensitive comparison for
  // normalized codes and enum-backed strings.
  public const string OrdinalCollation = PlatformPersistenceConstants.OrdinalCollation;

  // Marker namespace that keeps the two contexts' EF configurations from bleeding into each other.
  public static readonly string ConfigurationNamespace =
    typeof(TenantPersistenceConstants).Namespace + ".Configurations";
}
