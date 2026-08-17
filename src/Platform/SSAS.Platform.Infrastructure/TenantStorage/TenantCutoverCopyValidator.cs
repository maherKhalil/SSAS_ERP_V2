using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// EXACT VALIDATION, NOT A CHECKSUM (ADR-020, TS-Storage Phase E3).
//
// The two sides are read with the IDENTICAL projection and the IDENTICAL primary-key ordering, then walked
// in lockstep and compared value by value. Every copied column of every row is compared; nothing is
// sampled, hashed or aggregated.
//
// WHY NOT CHECKSUM/BINARY_CHECKSUM/CHECKSUM_AGG. They collide — CHECKSUM_AGG is order-insensitive addition,
// so two rows swapping a value can leave the aggregate unchanged, and BINARY_CHECKSUM misses some
// single-character differences by construction. For "is this tenant's data intact after being moved to a
// new database", a mechanism that is usually right is not evidence. There is no probabilistic component
// here at all: this comparison is exact, so nothing needs documenting as approximate.
//
// ROWVERSION IS ABSENT FROM THE COMPARISON because it is absent from the plan's column list — the same list
// the copy used. That is the point of sharing it: a column cannot be skipped by the copy and then silently
// demanded by validation, or the reverse. Target rowversion is asserted separately as target-generated.
//
// IT READS THE TARGET THROUGH THE CALLER'S TRANSACTION, so a table can be validated before its insert is
// committed and rolled back as one unit if it does not match.
internal sealed class TenantCutoverCopyValidator(TenantCutoverCopyOptions options)
{
  public async Task<TenantCutoverTableValidation> ValidateAsync(
    TenantCutoverTablePlan table,
    Guid tenantId,
    SqlConnection source,
    SqlConnection target,
    SqlTransaction? targetTransaction,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(table);
    ArgumentNullException.ThrowIfNull(source);
    ArgumentNullException.ThrowIfNull(target);

    await using var sourceCommand = TenantRowsCommand(source, null, table, tenantId);
    await using var targetCommand = TenantRowsCommand(target, targetTransaction, table, tenantId);

    await using var sourceReader = await sourceCommand.ExecuteReaderAsync(cancellationToken);
    await using var targetReader = await targetCommand.ExecuteReaderAsync(cancellationToken);

    long rows = 0;
    while (true)
    {
      var sourceHasRow = await sourceReader.ReadAsync(cancellationToken);
      var targetHasRow = await targetReader.ReadAsync(cancellationToken);

      if (!sourceHasRow && !targetHasRow)
      {
        break;
      }

      // A row on one side only. Because both sides are ordered by the same primary key, this is where a
      // missing row and an extra row are both detected, and the key that differs is reported.
      if (sourceHasRow != targetHasRow)
      {
        var side = sourceHasRow ? "target is missing a row" : "target has an extra row";
        var key = KeyOf(sourceHasRow ? sourceReader : targetReader, table);
        return TenantCutoverTableValidation.Mismatch(
          table, rows, $"{side} at primary key {key}");
      }

      for (var ordinal = 0; ordinal < table.Columns.Count; ordinal++)
      {
        if (!ValuesEqual(sourceReader, targetReader, ordinal))
        {
          return TenantCutoverTableValidation.Mismatch(
            table, rows,
            $"column [{table.Columns[ordinal]}] differs at primary key {KeyOf(sourceReader, table)}");
        }
      }

      // TenantId is compared as part of the columns above, but it is asserted explicitly as well: "the
      // copied rows belong to the requested tenant" is the invariant the whole cutover rests on, and it
      // should not depend on that column happening to be in the projection.
      var tenantOrdinal = table.Columns
        .ToList()
        .FindIndex(column => string.Equals(column, table.TenantIdColumn, StringComparison.Ordinal));
      if (tenantOrdinal >= 0 && targetReader.GetGuid(tenantOrdinal) != tenantId)
      {
        return TenantCutoverTableValidation.Mismatch(
          table, rows, $"target row at primary key {KeyOf(targetReader, table)} belongs to another tenant");
      }

      rows++;
    }

    return TenantCutoverTableValidation.Exact(table, rows);
  }

  // Rows belonging to any OTHER tenant. Separate from the lockstep walk on purpose: the walk compares what
  // the requested tenant has, and could not see a co-tenant's row on a target that is supposed to be
  // dedicated to one tenant.
  public async Task<long> CountForeignTenantRowsAsync(
    TenantCutoverTablePlan table,
    Guid tenantId,
    SqlConnection target,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(table);
    ArgumentNullException.ThrowIfNull(target);

    await using var command = target.CreateCommand();
    command.CommandText =
      $"SELECT COUNT_BIG(*) FROM {table.QualifiedName} WHERE [{table.TenantIdColumn}] <> @TenantId";
    command.Parameters.Add("@TenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
    command.CommandTimeout = (int)options.CommandTimeout.TotalSeconds;

    return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
  }

  public async Task<long> CountTenantRowsAsync(
    TenantCutoverTablePlan table,
    Guid tenantId,
    SqlConnection connection,
    SqlTransaction? transaction,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(table);
    ArgumentNullException.ThrowIfNull(connection);

    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      $"SELECT COUNT_BIG(*) FROM {table.QualifiedName} WHERE [{table.TenantIdColumn}] = @TenantId";
    command.Parameters.Add("@TenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
    command.CommandTimeout = (int)options.CommandTimeout.TotalSeconds;

    return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
  }

  // Proof that the target generated its own rowversion rather than receiving the source's. Counts rows
  // whose rowversion is null or empty; a generated one is never either.
  public async Task<long> CountRowsMissingRowVersionAsync(
    TenantCutoverTablePlan table,
    string rowVersionColumn,
    Guid tenantId,
    SqlConnection target,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(table);

    await using var command = target.CreateCommand();
    command.CommandText =
      $"SELECT COUNT_BIG(*) FROM {table.QualifiedName} " +
      $"WHERE [{table.TenantIdColumn}] = @TenantId AND ([{rowVersionColumn}] IS NULL OR " +
      $"DATALENGTH([{rowVersionColumn}]) = 0)";
    command.Parameters.Add("@TenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
    command.CommandTimeout = (int)options.CommandTimeout.TotalSeconds;

    return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
  }

  private SqlCommand TenantRowsCommand(
    SqlConnection connection,
    SqlTransaction? transaction,
    TenantCutoverTablePlan table,
    Guid tenantId)
  {
    var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      $"SELECT {table.ColumnList} FROM {table.QualifiedName} " +
      $"WHERE [{table.TenantIdColumn}] = @TenantId ORDER BY {table.OrderByPrimaryKey}";
    command.Parameters.Add("@TenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
    command.CommandTimeout = (int)options.CommandTimeout.TotalSeconds;
    return command;
  }

  // Null-aware, and byte-array aware because SqlDataReader returns byte[] by reference rather than by value
  // — reference equality would report every binary column as different.
  private static bool ValuesEqual(SqlDataReader left, SqlDataReader right, int ordinal)
  {
    var leftNull = left.IsDBNull(ordinal);
    var rightNull = right.IsDBNull(ordinal);
    if (leftNull || rightNull)
    {
      return leftNull && rightNull;
    }

    var leftValue = left.GetValue(ordinal);
    var rightValue = right.GetValue(ordinal);

    if (leftValue is byte[] leftBytes && rightValue is byte[] rightBytes)
    {
      return leftBytes.AsSpan().SequenceEqual(rightBytes);
    }

    return Equals(leftValue, rightValue);
  }

  private static string KeyOf(SqlDataReader reader, TenantCutoverTablePlan table)
  {
    var parts = table.PrimaryKeyColumns.Select(column =>
    {
      var ordinal = table.Columns
        .ToList()
        .FindIndex(candidate => string.Equals(candidate, column, StringComparison.Ordinal));
      return ordinal < 0 || reader.IsDBNull(ordinal)
        ? $"{column}=<null>"
        : string.Create(CultureInfo.InvariantCulture, $"{column}={reader.GetValue(ordinal)}");
    });

    return string.Join(", ", parts);
  }
}

// What validation concluded about one table. A REASON, not a bool: an operator reading a failed cutover
// needs to know which table, which key and which column, and reconstructing that from a false is impossible.
internal sealed record TenantCutoverTableValidation(
  string EntityName,
  string TableName,
  long Rows,
  bool IsExact,
  string? Difference)
{
  public static TenantCutoverTableValidation Exact(TenantCutoverTablePlan table, long rows) =>
    new(table.EntityName, table.TableName, rows, true, null);

  public static TenantCutoverTableValidation Mismatch(
    TenantCutoverTablePlan table, long rows, string difference) =>
    new(table.EntityName, table.TableName, rows, false, difference);
}
