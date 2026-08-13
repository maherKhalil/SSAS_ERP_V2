namespace SSAS.Platform.Infrastructure.TenantStorage;

// Trusted tenant-storage configuration (ADR-017). ServerKey is a LOOKUP KEY into this configuration, not
// an address: the Platform database persists the key and the catalog name, while addresses and credentials
// stay in configuration/secret storage where one entry serves many database rows.
//
// This slice needs only the default server key naming today's physical database. The per-server connection
// map arrives with the TS-1C connection factory, which is the only component that will ever resolve a key
// to a connection.
public sealed class TenantStorageOptions
{
  public const string SectionName = "TenantStorage";

  public const string FallbackDefaultServerKey = "PrimarySqlServer";

  // Logical identifier for the server hosting the current shared database. Configurable so a deployment
  // can name its own server, with a stable fallback so the single-database deployment needs no new
  // configuration to adopt the registry.
  public string DefaultServerKey { get; set; } = FallbackDefaultServerKey;
}
