using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Post-operation evidence reconciliation against msdb (ADR-022 §14).
//
// EXTRACTED AS ITS OWN UNIT for the same reason as the visibility check: this is the code that decides
// whether a backup may be called successful, so a test must be able to exercise IT rather than a lookalike
// query written beside it.
//
// A completed ExecuteNonQuery is not evidence. What counts as evidence here is a backup set that SQL Server
// itself recorded, matching on FOUR independent facts:
//
//   1. the database it should have backed up,
//   2. the EXACT device this run wrote to,
//   3. the backup TYPE the requested operation should have produced,
//   4. and quality markers — checksums present, and not copy-only.
//
// Any of them missing means this run does not get to claim success.
internal static class SqlServerBackupEvidence
{
  // SQL Server's own backup type codes, verified against msdb on a live instance and against the recorded
  // types the managed chain produces: 'D' database (full), 'I' differential, 'L' log.
  //
  // Correlating on type is what stops a run from adopting a backup set that happens to sit at the same
  // device but records a different operation.
  public static string? TypeCodeFor(TenantDatabaseBackupOperation operation)
  {
    ArgumentNullException.ThrowIfNull(operation);

    return operation.OperationCode switch
    {
      "Full" => "D",
      "Differential" => "I",
      "TransactionLog" => "L",
      _ => null
    };
  }

  public static async Task<Result<SqlServerBackupEvidenceRecord>> ReadAsync(
    SqlConnection connection,
    string databaseName,
    TenantDatabaseBackupOperation operation,
    string devicePath,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(connection);

    var typeCode = TypeCodeFor(operation);
    if (typeCode is null)
    {
      return Result.Failure<SqlServerBackupEvidenceRecord>(TenantStorageErrors.BackupEvidenceMissing);
    }

    await using var command = connection.CreateCommand();

    // THE DEVICE IS MATCHED EXACTLY, not with LIKE.
    //
    // The previous suffix pattern was `'%' + fileName`, and the generated file name contains underscores —
    // which are SINGLE-CHARACTER WILDCARDS in T-SQL. The pattern was therefore broader than it read, matching
    // any device whose tail differed only at those positions. Equality has no metacharacters to escape and no
    // way to widen, and it is available because this provider supplied the device string itself.
    //
    // backup_finish_date is converted to TRUE UTC SERVER-SIDE. It is a `datetime` with no timezone, written
    // from the server's LOCAL clock, so labelling it UTC client-side was simply wrong — on a UTC+03 host it
    // produced timestamps three hours in the future. The offset used is the server's CURRENT one, which is
    // correct here because this reconciles the backup that finished moments ago on this same connection, not
    // an arbitrary historical row (see the DST note on the caller).
    command.CommandText =
      "SELECT TOP (1) bs.backup_set_uuid, bs.first_lsn, bs.last_lsn, bs.database_backup_lsn, " +
      "bs.backup_size, " +
      "DATEADD(second, DATEDIFF(second, GETDATE(), GETUTCDATE()), bs.backup_finish_date) AS backup_finish_utc, " +
      "bs.backup_set_id, bs.has_backup_checksums, bs.is_copy_only " +
      "FROM msdb.dbo.backupset AS bs " +
      "INNER JOIN msdb.dbo.backupmediafamily AS bmf ON bmf.media_set_id = bs.media_set_id " +
      "WHERE bs.database_name = @database AND bmf.physical_device_name = @device AND bs.type = @type " +
      "ORDER BY bs.backup_set_id DESC";

    command.Parameters.Add("@database", SqlDbType.NVarChar, 128).Value = databaseName;
    command.Parameters.Add("@device", SqlDbType.NVarChar, 260).Value = devicePath;
    command.Parameters.Add("@type", SqlDbType.Char, 1).Value = typeCode;

    SqlServerBackupEvidenceRecord? record;
    await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
    {
      if (!await reader.ReadAsync(cancellationToken))
      {
        // No backup set for this database, at this device, of this type. Nothing to claim.
        return Result.Failure<SqlServerBackupEvidenceRecord>(TenantStorageErrors.BackupEvidenceMissing);
      }

      record = new SqlServerBackupEvidenceRecord(
        reader.IsDBNull(0) ? null : reader.GetGuid(0),
        reader.IsDBNull(1) ? null : reader.GetDecimal(1),
        reader.IsDBNull(2) ? null : reader.GetDecimal(2),
        reader.IsDBNull(3) ? null : reader.GetDecimal(3),
        reader.IsDBNull(4) ? null : Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture),
        reader.IsDBNull(5) ? null : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)),
        reader.IsDBNull(6) ? null : Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture),
        !reader.IsDBNull(7) && reader.GetBoolean(7),
        !reader.IsDBNull(8) && reader.GetBoolean(8));
    }

    // CHECKSUMS. The managed chain always issues WITH CHECKSUM, so evidence saying otherwise means the set
    // that landed is not the one this provider believes it took. Checksums are what make a torn backup
    // detectable at write time instead of at restore time, so a set without them is not an acceptable
    // baseline (ADR-022 §9).
    if (!record.HasChecksums)
    {
      return Result.Failure<SqlServerBackupEvidenceRecord>(TenantStorageErrors.BackupEvidenceRejected);
    }

    // COPY-ONLY. A copy-only full does not reset the differential base, so accepting one as a managed full
    // would leave later differentials anchored to an older baseline — a chain that looks healthy and
    // restores to the wrong point.
    if (record.IsCopyOnly)
    {
      return Result.Failure<SqlServerBackupEvidenceRecord>(TenantStorageErrors.BackupEvidenceRejected);
    }

    return Result.Success(record);
  }
}

internal sealed record SqlServerBackupEvidenceRecord(
  Guid? BackupSetGuid,
  decimal? FirstLsn,
  decimal? LastLsn,
  decimal? DatabaseBackupLsn,
  long? BackupSizeBytes,
  DateTimeOffset? FinishedUtc,
  long? BackupSetId,
  bool HasChecksums,
  bool IsCopyOnly)
{
  // The provider's stable identity for this backup set. Prefers the backup-set GUID, which is what a restore
  // would be selected by, and falls back to the numeric identity where the GUID is unavailable.
  public string BackupSetIdentity =>
    BackupSetGuid?.ToString() ??
    BackupSetId?.ToString(CultureInfo.InvariantCulture) ??
    string.Empty;
}
