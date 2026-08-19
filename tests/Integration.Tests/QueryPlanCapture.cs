using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SSAS.Integration.Tests;

// DETERMINISTIC QUERY-PLAN CAPTURE.
//
// The plan tests exist to prove that hot-path queries SEEK their indexes at realistic cardinality. They
// originally read the plan back out of sys.dm_exec_query_stats after clearing the procedure cache. That
// reads a SERVER-WIDE CACHE, and a cache is not a record — it is a hint about what the server still happens
// to remember. On a developer instance running this suite the whole-server plan cache measures single-digit
// entries and is empty at rest, so the capture returned nothing and the tests failed having proven nothing.
// A test that cannot tell "the query scanned" from "the server forgot" is not a guard.
//
// So the plan now comes from SET STATISTICS XML ON, which returns the ACTUAL post-execution plan as a
// trailing result set on the connection that ran the statement. It cannot be evicted, because it is never
// stored — it is returned. That is the whole point: capture stops depending on cache residency.
//
// FIDELITY. The measured statement is not hand-written. ProductionSqlRecorder is an EF interceptor that
// records the exact CommandText and a faithful clone of every parameter as the PRODUCTION component issues
// it; ExplainAsync replays that recorded command verbatim. The statement measured is therefore the one the
// application runs, down to parameter types and sizes — the property the DMV approach existed to protect,
// and the one a hand-written SELECT would lose.
//
// It IS a replay, and that is worth naming: the plan describes a second execution of the production
// statement rather than the first. For an index-access assertion the distinction does not matter — the same
// statement over the same data with the same parameter types compiles to the same plan — and the DMV
// approach shared the property anyway, since it too read the plan after the fact rather than during.
internal sealed record RecordedCommand(string CommandText, IReadOnlyList<SqlParameter> Parameters);

// Records what production actually sent. Both the sync and async paths are intercepted, because which one
// runs is EF's choice and not something a test should have to know.
internal sealed class ProductionSqlRecorder : DbCommandInterceptor
{
  private readonly List<RecordedCommand> recorded = [];

  public IReadOnlyList<RecordedCommand> Recorded
  {
    get
    {
      lock (recorded)
      {
        return recorded.ToArray();
      }
    }
  }

  public void Clear()
  {
    lock (recorded)
    {
      recorded.Clear();
    }
  }

  // The most recent statement containing every required fragment and none of the excluded ones. Fragments
  // rather than one long string because a query is identified by the tables and columns it names, and EF is
  // free to reorder whitespace and aliases between versions. Newest-first, because a test issues the call it
  // means to measure last.
  public RecordedCommand Match(string[] required, params string[] excluded)
  {
    var candidates = Recorded
      .Where(command =>
        required.All(fragment => command.CommandText.Contains(fragment, StringComparison.Ordinal)) &&
        !excluded.Any(fragment => command.CommandText.Contains(fragment, StringComparison.Ordinal)))
      .ToArray();

    if (candidates.Length == 0)
    {
      var seen = string.Join(
        "\n  ---\n  ",
        Recorded.Select(command => string.Join(
          ' ', command.CommandText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))));
      throw new InvalidOperationException(
        $"No production statement matched [{string.Join(", ", required)}]" +
        (excluded.Length == 0 ? string.Empty : $" excluding [{string.Join(", ", excluded)}]") +
        $".\nRecorded {Recorded.Count} statement(s):\n  {seen}");
    }

    return candidates[^1];
  }

  public override InterceptionResult<DbDataReader> ReaderExecuting(
    DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
  {
    Capture(command);
    return base.ReaderExecuting(command, eventData, result);
  }

  public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
    DbCommand command,
    CommandEventData eventData,
    InterceptionResult<DbDataReader> result,
    CancellationToken cancellationToken = default)
  {
    Capture(command);
    return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
  }

  private void Capture(DbCommand command)
  {
    // Cloned, not referenced. EF reuses and re-values command objects, so holding the live parameter would
    // replay whatever the NEXT caller happened to pass.
    var parameters = command.Parameters
      .OfType<SqlParameter>()
      .Select(parameter => (SqlParameter)((ICloneable)parameter).Clone())
      .ToArray();

    lock (recorded)
    {
      recorded.Add(new RecordedCommand(command.CommandText, parameters));
    }
  }
}

internal sealed record CapturedPlan(string PlanXml, long LogicalReads, long Microseconds);

internal static class QueryPlanCapture
{
  // Runs the recorded production statement under SET STATISTICS XML ON and returns the actual plan.
  //
  // SET STATISTICS XML must be alone in its batch, so the switch, the statement and the reset are three
  // round trips on one connection rather than one batch.
  public static async Task<CapturedPlan> ExplainAsync(
    string connectionString, RecordedCommand recorded, CancellationToken cancellationToken = default)
  {
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    await using (var on = connection.CreateCommand())
    {
      on.CommandText = "SET STATISTICS XML ON;";
      await on.ExecuteNonQueryAsync(cancellationToken);
    }

    string? planXml = null;
    try
    {
      await using var command = connection.CreateCommand();
      command.CommandText = recorded.CommandText;
      foreach (var parameter in recorded.Parameters)
      {
        command.Parameters.Add((SqlParameter)((ICloneable)parameter).Clone());
      }

      // The plan arrives AFTER the statement's own result sets, so every result set is walked and whichever
      // one carries showplan is taken. Walking rather than assuming a position: a statement returning no
      // rows and one returning several put the plan in different places.
      await using var reader = await command.ExecuteReaderAsync(cancellationToken);
      do
      {
        while (await reader.ReadAsync(cancellationToken))
        {
          if (reader.FieldCount != 1 || reader.IsDBNull(0))
          {
            continue;
          }

          if (reader.GetValue(0)?.ToString() is { } value &&
              value.StartsWith("<ShowPlanXML", StringComparison.Ordinal))
          {
            planXml = value;
          }
        }
      }
      while (await reader.NextResultAsync(cancellationToken));
    }
    finally
    {
      await using var off = connection.CreateCommand();
      off.CommandText = "SET STATISTICS XML OFF;";
      await off.ExecuteNonQueryAsync(CancellationToken.None);
    }

    if (planXml is null)
    {
      throw new InvalidOperationException(
        "SET STATISTICS XML returned no showplan for the recorded production statement:\n" +
        recorded.CommandText);
    }

    return new CapturedPlan(planXml, Sum(planXml, "ActualLogicalReads=\""), MaxElapsedMicroseconds(planXml));
  }

  // Summed across operators and threads. Summing is deliberately the conservative direction: it can only
  // overstate the cost, so a reads threshold asserted against it can never be satisfied by a query that
  // actually did more work than the threshold allows.
  private static long Sum(string planXml, string marker)
  {
    long total = 0;
    var cursor = 0;
    while (true)
    {
      var start = planXml.IndexOf(marker, cursor, StringComparison.Ordinal);
      if (start < 0)
      {
        return total;
      }

      start += marker.Length;
      var end = planXml.IndexOf('"', start);
      if (end < 0)
      {
        return total;
      }

      if (long.TryParse(planXml[start..end], out var value))
      {
        total += value;
      }

      cursor = end;
    }
  }

  // Reported, never asserted on. Elapsed milliseconds are per-operator wall clock, so the maximum is the
  // statement duration while a sum would count parallel branches twice.
  private static long MaxElapsedMicroseconds(string planXml)
  {
    const string Marker = "ActualElapsedms=\"";
    long max = 0;
    var cursor = 0;
    while (true)
    {
      var start = planXml.IndexOf(Marker, cursor, StringComparison.Ordinal);
      if (start < 0)
      {
        return max * 1000;
      }

      start += Marker.Length;
      var end = planXml.IndexOf('"', start);
      if (end < 0)
      {
        return max * 1000;
      }

      if (long.TryParse(planXml[start..end], out var value) && value > max)
      {
        max = value;
      }

      cursor = end;
    }
  }
}
