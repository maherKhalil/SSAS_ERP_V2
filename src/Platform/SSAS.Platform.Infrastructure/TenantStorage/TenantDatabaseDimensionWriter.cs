using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// The ONE dimension-scoped write path onto the physical TenantDatabase row, shared by every dimension
// writer (ADR-018 v1.6, ADR-022 §3).
//
// Extracted rather than duplicated because recovery readiness makes this the THIRD independent writer on a
// single row. Two copies of a retry loop drift; three reliably do. Sharing the implementation also means the
// deterministic RowVersion conflict test proves the retry path that every dimension actually uses, rather
// than proving one writer's copy of it.
//
// It is deliberately internal and deliberately takes a mutation rather than a set of fields: the public
// surface stays one method per dimension (ITenantDatabaseHealthWriter, ITenantDatabaseRecoveryReadinessWriter),
// so no caller can express "write several dimensions at once" — which is the mistake that let a connectivity
// check overwrite schema state it had never observed.
internal sealed class TenantDatabaseDimensionWriter(PlatformDbContext dbContext)
{
  // Independent checkers write the same row on different cadences, so RowVersion conflicts are ordinary
  // rather than exceptional. The bound is small because each retry re-reads and reapplies ONE dimension's
  // observation — it converges immediately, or the row is genuinely contended and that is an operational
  // signal rather than something to spin on.
  private const int MaximumConcurrencyRetries = 3;

  public async Task ApplyAsync(
    long tenantDatabaseId,
    Action<TenantDatabase> mutate,
    CancellationToken cancellationToken)
  {
    for (var attempt = 0; ; attempt++)
    {
      var database = await dbContext.TenantDatabases
        .SingleOrDefaultAsync(item => item.Id == tenantDatabaseId, cancellationToken);
      if (database is null)
      {
        return;
      }

      mutate(database);

      try
      {
        await dbContext.SaveChangesAsync(cancellationToken);
        return;
      }
      catch (DbUpdateConcurrencyException)
      {
        // Another writer updated this row first. ONLY this exception is retried: a SqlException is a
        // different problem and retrying it would spin on a deterministic failure.
        if (attempt >= MaximumConcurrencyRetries)
        {
          return;
        }
      }
      finally
      {
        // Detaching is what makes the next attempt RE-READ rather than reuse the stale tracked instance, so
        // the mutation replays against whatever the winner wrote. Reloading and saving the whole stale
        // aggregate — the obvious alternative — would be last-write-wins across dimensions, which is
        // precisely the bug this design exists to remove.
        //
        // It also keeps the change tracker from growing across a fleet sweep that touches every physical
        // database in the estate.
        dbContext.Entry(database).State = EntityState.Detached;
      }
    }
  }
}
