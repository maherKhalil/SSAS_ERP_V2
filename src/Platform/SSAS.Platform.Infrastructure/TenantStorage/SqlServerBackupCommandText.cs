using System.Globalization;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// SQL Server backup command construction (ADR-022 §9, §10).
//
// Kept as its own small, testable unit rather than inline strings in the provider, because the two things
// most worth proving about a backup command — that the database identifier is safely quoted, and that
// COPY_ONLY never appears in the managed chain — are exactly the things that get lost in a longer method.
//
// The DESTINATION PATH IS NEVER INTERPOLATED. It travels as a SqlParameter, so nothing here can be turned
// into a path injection even if configuration were somehow wrong.
internal static class SqlServerBackupCommandText
{
  public const string PathParameterName = "@path";

  // A database name is an IDENTIFIER, so it cannot be a SqlParameter — it must be quoted. This is
  // QUOTENAME's rule applied client-side: double any closing bracket, then wrap. The name itself comes from
  // the trusted TenantDatabase registry and never from a caller, so this is defence in depth rather than the
  // primary control.
  public static string QuoteIdentifier(string identifier)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
    return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
  }

  // Builds one managed backup command.
  //
  // COPY_ONLY is deliberately absent and unreachable: a copy-only full does not reset the differential base,
  // so a chain built on one silently produces differentials anchored to an older full (ADR-022 §9). Copy-only
  // remains an explicit break-glass capability outside the managed chain, which this method does not serve.
  public static string Build(
    TenantDatabaseBackupOperation operation,
    string databaseName,
    bool compress)
  {
    ArgumentNullException.ThrowIfNull(operation);

    var quoted = QuoteIdentifier(databaseName);
    var compression = compress ? "COMPRESSION" : "NO_COMPRESSION";

    // CHECKSUM is always on (ADR-022 §9): it is what makes a torn or corrupt backup set detectable at write
    // time rather than at restore time.
    //
    // INIT is safe because every run writes a UNIQUE generated artifact name, so there is never an existing
    // managed backup set to overwrite. It is included so a retried run cannot append to a partial file left
    // by a failed attempt.
    return operation.OperationCode switch
    {
      "Full" => string.Create(CultureInfo.InvariantCulture,
        $"BACKUP DATABASE {quoted} TO DISK = {PathParameterName} WITH CHECKSUM, INIT, {compression}"),

      "Differential" => string.Create(CultureInfo.InvariantCulture,
        $"BACKUP DATABASE {quoted} TO DISK = {PathParameterName} WITH DIFFERENTIAL, CHECKSUM, INIT, {compression}"),

      "TransactionLog" => string.Create(CultureInfo.InvariantCulture,
        $"BACKUP LOG {quoted} TO DISK = {PathParameterName} WITH CHECKSUM, INIT, {compression}"),

      _ => throw new NotSupportedException($"Unsupported SQL Server backup operation '{operation.OperationCode}'.")
    };
  }

  // Deterministic, non-secret artifact name (ADR-022 §11). Physical database identity, operation, timestamp
  // and run identifier — enough to be unique and interpretable, and deliberately carrying no customer or
  // company name.
  public static string ArtifactFileName(
    long tenantDatabaseId,
    TenantDatabaseBackupOperation operation,
    long backupRunId,
    DateTimeOffset occurredUtc)
  {
    ArgumentNullException.ThrowIfNull(operation);

    var extension = operation.OperationCode == "TransactionLog" ? "trn" : "bak";
    var stamp = occurredUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    return string.Create(CultureInfo.InvariantCulture,
      $"{tenantDatabaseId}_{operation.OperationCode}_{stamp}_{backupRunId}.{extension}");
  }

  // Whether the deployed engine supports backup compression. Determined from engine metadata rather than by
  // issuing a command and catching the error: a capability that can be known beforehand should not be
  // discovered through a failed operation (ADR-022 §9).
  //
  // EngineEdition 4 is Express, which cannot compress. Everything else in the supported range can.
  public static bool SupportsCompression(int engineEdition) => engineEdition != ExpressEngineEdition;

  private const int ExpressEngineEdition = 4;

  // Resolves the policy's compression intent against what the engine can actually do.
  public static Result<bool> ResolveCompression(
    TenantDatabaseBackupCompressionMode mode,
    bool engineSupportsCompression) =>
    mode switch
    {
      TenantDatabaseBackupCompressionMode.Disabled => Result.Success(false),

      // An unavailable capability is not a policy violation: take an approved uncompressed backup rather
      // than reporting a perfectly protected database unprotected (ADR-022 §9).
      TenantDatabaseBackupCompressionMode.PreferredWhereSupported => Result.Success(engineSupportsCompression),

      // Genuinely required, genuinely unsupported. Fail BEFORE issuing the command so there is no partial
      // operation to reconcile.
      TenantDatabaseBackupCompressionMode.Required => engineSupportsCompression
        ? Result.Success(true)
        : Result.Failure<bool>(TenantStorageErrors.BackupCompressionNotSupported),

      _ => Result.Failure<bool>(TenantStorageErrors.BackupCompressionNotSupported)
    };
}
