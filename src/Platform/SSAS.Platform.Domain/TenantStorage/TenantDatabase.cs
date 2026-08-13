using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.TenantStorage;

// ONE physical tenant-storage database endpoint (ADR-017) — not a tenant. TenantId is deliberately absent:
// a shared database hosts many tenants, and the tenant -> endpoint mapping belongs to
// TenantDatabaseAssignment. This is Platform operational metadata, so it is deliberately NOT
// ITenantOwnedEntity: routing and bootstrap infrastructure must read it without an ambient tenant filter.
//
// Only trusted routing metadata is persisted. Complete connection strings, plaintext passwords,
// certificates and private keys must never be stored here; credential material for platform-managed
// servers comes from trusted application configuration keyed by ServerKey. Customer endpoint address and
// credential-reference fields belong to ADR-021 and are not modelled in this slice.
public sealed class TenantDatabase : AggregateRoot<long>, IAuditableEntity
{
  public const int ServerKeyMaximumLength = 128;

  // SQL Server catalog names are sysname (128 Unicode characters).
  public const int DatabaseNameMaximumLength = 128;

  public const int ActorMaximumLength = 256;

  private TenantDatabase(
    long id,
    TenantDatabaseHostingMode hostingMode,
    TenantDatabaseStorageMode storageMode,
    string serverKey,
    string databaseName,
    TenantDatabaseProvisioningStatus provisioningStatus,
    string actor,
    DateTimeOffset occurredUtc)
    : base(id)
  {
    HostingMode = hostingMode;
    StorageMode = storageMode;
    ServerKey = serverKey;
    DatabaseName = databaseName;
    ProvisioningStatus = provisioningStatus;
    CreatedUtc = occurredUtc.ToUniversalTime();
    CreatedBy = actor;
    ModifiedUtc = CreatedUtc;
    ModifiedBy = actor;
  }

  private TenantDatabase()
    : base(0)
  {
    ServerKey = string.Empty;
    DatabaseName = string.Empty;
  }

  public TenantDatabaseHostingMode HostingMode { get; private set; }

  public TenantDatabaseStorageMode StorageMode { get; private set; }

  // A trusted configuration LOOKUP KEY, never a hostname, endpoint or connection string. The Platform
  // database therefore stores no address and no credential for a platform-managed server: one
  // configuration entry serves many rows, and rotating a server or credential is a configuration change.
  public string ServerKey { get; private set; }

  public string DatabaseName { get; private set; }

  public TenantDatabaseProvisioningStatus ProvisioningStatus { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public static Result<TenantDatabase> Register(
    TenantDatabaseHostingMode hostingMode,
    TenantDatabaseStorageMode storageMode,
    string serverKey,
    string databaseName,
    TenantDatabaseProvisioningStatus provisioningStatus,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (string.IsNullOrWhiteSpace(serverKey) || serverKey.Length > ServerKeyMaximumLength)
    {
      return Result.Failure<TenantDatabase>(TenantStorageErrors.ServerKeyRequired);
    }

    if (string.IsNullOrWhiteSpace(databaseName) || databaseName.Length > DatabaseNameMaximumLength)
    {
      return Result.Failure<TenantDatabase>(TenantStorageErrors.DatabaseNameRequired);
    }

    // The one invalid hosting/storage combination (ADR-017). Enforced here AND by a database CHECK, so
    // neither an application path nor a direct SQL write can create it.
    if (hostingMode == TenantDatabaseHostingMode.CustomerManaged &&
      storageMode == TenantDatabaseStorageMode.Shared)
    {
      return Result.Failure<TenantDatabase>(TenantStorageErrors.CustomerManagedMustBeDedicated);
    }

    return Result.Success(new TenantDatabase(
      0, hostingMode, storageMode, serverKey, databaseName, provisioningStatus, actor, occurredUtc));
  }

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }
}
