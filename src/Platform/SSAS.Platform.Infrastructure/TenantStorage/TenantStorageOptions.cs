namespace SSAS.Platform.Infrastructure.TenantStorage;

// Trusted tenant-storage configuration (ADR-017). ServerKey is a LOOKUP KEY into this configuration, not
// an address: the Platform database persists the key and the catalog name, while addresses and credentials
// stay in configuration/secret storage where one entry serves many database rows.
//
// `DefaultServerKey` names the server hosting today's baseline shared database; `Servers` holds the
// per-server connection material. The connection factory is the only component that ever resolves a key
// to a connection, and TenantStorageOptionsValidator checks the shape of both at startup.
public sealed class TenantStorageOptions
{
  public const string SectionName = "TenantStorage";

  public const string FallbackDefaultServerKey = "PrimarySqlServer";

  // Logical identifier for the server hosting the current shared database. Configurable so a deployment
  // can name its own server, with a stable fallback so the single-database deployment needs no new
  // configuration to adopt the registry.
  //
  // NOTE the asymmetry with Servers below: this fallback only decides which key BOOTSTRAP stamps onto the
  // registry row it creates. It is never consulted when routing an already-registered database — an
  // unknown route ServerKey fails closed rather than falling back to anything.
  public string DefaultServerKey { get; set; } = FallbackDefaultServerKey;

  // Trusted per-server connection configuration, keyed by ServerKey. The Platform database stores only the
  // key; the address and credentials live here, so one entry serves many database rows and rotating a
  // server or credential is a configuration change rather than a data migration.
  //
  // There is deliberately NO fallback entry and no default: a route naming a key that is absent here is a
  // routing failure, never a redirect to the Platform connection (ADR-017 "No automatic fallback").
  public IDictionary<string, TenantStorageServerOptions> Servers { get; } =
    new Dictionary<string, TenantStorageServerOptions>(StringComparer.Ordinal);
}

// Trusted connection configuration for one logical server. Credential material lives in configuration or a
// secret store, never in the Platform database.
public sealed class TenantStorageServerOptions
{
  public string ConnectionString { get; set; } = string.Empty;
}
