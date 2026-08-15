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

  // ---- Backup authority (ADR-022 §11, TS-Backup Phase B). A SEPARATE credential profile, keyed by the
  // SAME ServerKey namespace as `Servers` above.
  //
  // The key namespace is shared deliberately: ServerKey remains the one physical server registry, and a
  // second registry would let backup reach a server routing would refuse. What differs is the IDENTITY
  // behind the key — `BACKUP DATABASE` reads the entire database to a file, so granting that to the
  // request-serving credential would widen any application compromise from "what the ERP can query" to "a
  // complete copy of the database". Separate entries keep the two authorities independently rotatable and
  // independently scoped.
  //
  // Absent entry = backup is not configured for that server, and fails closed. There is no fallback to
  // `Servers`, because falling back would silently reintroduce the very credential reuse this separation
  // exists to prevent.
  public IDictionary<string, TenantStorageServerOptions> BackupServers { get; } =
    new Dictionary<string, TenantStorageServerOptions>(StringComparer.Ordinal);

  // Trusted backup destinations, keyed by `BackupDestinationKey` (ADR-022 §11, compliance rule 23).
  //
  // THE ONLY place a physical backup location may come from. A caller, tenant or request may contribute
  // the KEY and nothing else; resolution to a directory happens here, in trusted configuration, entirely
  // inside Infrastructure. An unknown key fails closed.
  public IDictionary<string, TenantStorageBackupDestinationOptions> BackupDestinations { get; } =
    new Dictionary<string, TenantStorageBackupDestinationOptions>(StringComparer.Ordinal);
}

// One trusted backup destination. Holds a location, never a credential: V1 destinations are filesystem or
// UNC directories reached by the SQL Server service identity's own Windows authentication, so there is no
// secret to store here and none may be added without revisiting ADR-022 §11.
public sealed class TenantStorageBackupDestinationOptions
{
  // A directory the SQL SERVER SERVICE IDENTITY can write to — not the application process. Those are
  // different accounts with different access, and confusing them is the single most common way a correctly
  // configured backup fails with OS error 5.
  public string DirectoryPath { get; set; } = string.Empty;
}

// Trusted connection configuration for one logical server. Credential material lives in configuration or a
// secret store, never in the Platform database.
public sealed class TenantStorageServerOptions
{
  public string ConnectionString { get; set; } = string.Empty;
}
