using System.Data;
using Microsoft.Data.SqlClient;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// ONE TABLE, STREAMED FROM SOURCE TO TARGET (ADR-020, TS-Storage Phase E3).
//
// CROSS-INSTANCE BY CONSTRUCTION. There is deliberately no `INSERT target SELECT FROM source` and no
// three-part name anywhere in this file: source and target may be different SQL Server instances, and a
// set-based cross-database insert would silently make them required to be the same one. The data travels
// through this process, which is what makes a linked server unnecessary.
//
// STREAMED, NOT MATERIALISED. The source reader is handed straight to SqlBulkCopy with EnableStreaming, so
// a tenant with millions of rows moves in bounded memory rather than being loaded first. Nothing here
// builds a list of rows.
//
// TRIGGERS ARE NOT FIRED. SqlBulkCopy does not fire them unless asked, and it is deliberately not asked —
// see the type comment on TenantCutoverCopyService for why that is the correct choice for a copy whose
// contract is "audit values verbatim".
internal sealed class TenantCutoverTableCopier(TenantCutoverCopyOptions options)
{
  public async Task<long> CopyAsync(
    TenantCutoverTablePlan table,
    Guid tenantId,
    SqlConnection source,
    SqlConnection target,
    SqlTransaction targetTransaction,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(table);
    ArgumentNullException.ThrowIfNull(source);
    ArgumentNullException.ThrowIfNull(target);
    ArgumentNullException.ThrowIfNull(targetTransaction);

    await using var command = source.CreateCommand();

    // THE TENANT FILTER IS IN THE SQL, AND IS PARAMETERISED. Not applied after reading, not inferred from
    // the database being dedicated, not taken from session context: on a shared source the co-tenants' rows
    // are in the same table, and reading them at all — even to discard them — would put another tenant's
    // data in this process's memory.
    command.CommandText =
      $"SELECT {table.ColumnList} FROM {table.QualifiedName} " +
      $"WHERE [{table.TenantIdColumn}] = @TenantId ORDER BY {table.OrderByPrimaryKey}";
    command.Parameters.Add("@TenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
    command.CommandTimeout = (int)options.CommandTimeout.TotalSeconds;

    // DEFAULT COMMAND BEHAVIOUR, DELIBERATELY NOT SequentialAccess. Rows still stream from the server — that
    // is SqlDataReader's normal mode, and EnableStreaming below is what keeps SqlBulkCopy from buffering
    // them. SequentialAccess additionally forbids reading a column ordinal backwards, and SqlBulkCopy reads
    // source columns in DESTINATION order, which is the target table's column order rather than this
    // SELECT's. The two are incompatible whenever those orders differ, and it fails at run time rather than
    // at compile time.
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

    using var bulkCopy = new SqlBulkCopy(target, BulkCopyOptions(table), targetTransaction)
    {
      DestinationTableName = table.QualifiedName,
      BatchSize = options.BatchSize,
      BulkCopyTimeout = (int)options.BulkCopyTimeout.TotalSeconds,
      EnableStreaming = true
    };

    // EXPLICIT MAPPINGS, BY NAME. Ordinal mapping would bind correctly today and silently misbind the day a
    // column is added to one side — writing one column's values into another column of the same type.
    foreach (var column in table.Columns)
    {
      bulkCopy.ColumnMappings.Add(column, column);
    }

    await bulkCopy.WriteToServerAsync(reader, cancellationToken);
    return bulkCopy.RowsCopied;
  }

  private static SqlBulkCopyOptions BulkCopyOptions(TenantCutoverTablePlan table)
  {
    // KEEPNULLS: without it SqlBulkCopy substitutes column defaults for nulls, so a nullable column that is
    // null at the source would arrive holding a default — a difference validation would catch, but only
    // after writing wrong data.
    //
    // CHECKCONSTRAINTS: the target's CHECK constraints and foreign keys are ENFORCED during the copy rather
    // than left untrusted afterwards. This is the opposite of the usual bulk-load advice, and deliberate:
    // the point of the copy is a target the application can trust, and FK-ordered insertion means nothing
    // legitimate is rejected.
    var bulkOptions = SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.CheckConstraints;

    // KEEPIDENTITY only where the model says a column is an identity. Setting it unconditionally would
    // error on tables that have none.
    return table.HasIdentityColumn ? bulkOptions | SqlBulkCopyOptions.KeepIdentity : bulkOptions;
  }
}

// Deployment configuration for the copy. NOTHING HERE IS A SAFETY CONTROL: there is no setting that skips
// validation, disables constraints, or fires triggers.
public sealed class TenantCutoverCopyOptions
{
  public const string SectionName = "TenantStorage:CutoverCopy";

  // Rows per bulk-copy batch. Bounded so a large tenant commits its table in steady memory on both sides.
  public int BatchSize { get; set; } = 5_000;

  public TimeSpan BulkCopyTimeout { get; set; } = TimeSpan.FromMinutes(30);

  public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(30);

  // How long a copy waits to become the operation's owner. Short: if another instance holds it, the right
  // outcome is to report that immediately, not to queue behind a copy that may run for an hour.
  public TimeSpan OwnershipTimeout { get; set; } = TimeSpan.FromSeconds(5);

  // How long a freeze release waits for a running copy to finish. Also short, and for the same reason —
  // release must return an answer rather than block an operator.
  public TimeSpan ReleaseOwnershipTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
