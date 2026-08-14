using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.TenantStorage;

// WHICH provider operation a backup run represents, as a PROVIDER-SCOPED pair rather than a universal enum.
//
// ADR-022 §10 is explicit that `Full`, `Differential` and `TransactionLog` are SQL SERVER VOCABULARY, not
// universal domain concepts. Modelling them as a core enum would encode one provider's chain model as the
// architecture: Oracle expresses its chain through RMAN and archived redo, PostgreSQL through WAL and
// physical or logical mechanisms, and mapping either onto those three names is lossy in both directions.
// The first non-SQL-Server provider would then be a rewrite rather than a registration.
//
// So the run records (ProviderKey, OperationCode). SQL Server's three codes are named below because SQL
// Server is the only V1 provider; a future provider contributes its own codes without this type changing.
public sealed class TenantDatabaseBackupOperation : ValueObject
{
  public const int ProviderKeyMaximumLength = 32;

  public const int OperationCodeMaximumLength = 32;

  // The only provider supported in V1 (ADR-017, ADR-022 §9).
  public const string SqlServerProviderKey = "SqlServer";

  private TenantDatabaseBackupOperation(string providerKey, string operationCode)
  {
    ProviderKey = providerKey;
    OperationCode = operationCode;
  }

  public string ProviderKey { get; }

  public string OperationCode { get; }

  // ---- SQL Server chain vocabulary. Named factories rather than enum members, so the provider scoping is
  // visible at every call site.

  public static TenantDatabaseBackupOperation SqlServerFull() =>
    new(SqlServerProviderKey, "Full");

  public static TenantDatabaseBackupOperation SqlServerDifferential() =>
    new(SqlServerProviderKey, "Differential");

  public static TenantDatabaseBackupOperation SqlServerTransactionLog() =>
    new(SqlServerProviderKey, "TransactionLog");

  public static Result<TenantDatabaseBackupOperation> Create(string? providerKey, string? operationCode)
  {
    var provider = providerKey?.Trim();
    var operation = operationCode?.Trim();

    if (string.IsNullOrEmpty(provider) || provider.Length > ProviderKeyMaximumLength)
    {
      return Result.Failure<TenantDatabaseBackupOperation>(TenantStorageErrors.BackupOperationInvalid);
    }

    if (string.IsNullOrEmpty(operation) || operation.Length > OperationCodeMaximumLength)
    {
      return Result.Failure<TenantDatabaseBackupOperation>(TenantStorageErrors.BackupOperationInvalid);
    }

    return Result.Success(new TenantDatabaseBackupOperation(provider, operation));
  }

  public override string ToString() => $"{ProviderKey}:{OperationCode}";

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return ProviderKey;
    yield return OperationCode;
  }
}
