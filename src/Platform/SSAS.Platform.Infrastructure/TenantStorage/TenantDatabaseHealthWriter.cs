using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.TenantStorage;

// Persists health and migration state onto the PHYSICAL TenantDatabase row (ADR-018).
//
// State lives on TenantDatabase, never duplicated per TenantDatabaseAssignment: a shared database has one
// schema and one migration state, and copying that onto every tenant's assignment would create rows that
// can disagree with each other about the same physical database.
public interface ITenantDatabaseHealthWriter
{
  Task RecordHealthAsync(
    long tenantDatabaseId,
    Action<TenantDatabase> mutate,
    CancellationToken cancellationToken = default);
}

public sealed class TenantDatabaseHealthWriter(PlatformDbContext dbContext) : ITenantDatabaseHealthWriter
{
  public async Task RecordHealthAsync(
    long tenantDatabaseId,
    Action<TenantDatabase> mutate,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(mutate);

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
    }
    catch (DbUpdateConcurrencyException)
    {
      // RowVersion rejected the write: another orchestrator or checker updated this database first.
      // Health is a CACHED OBSERVATION, and the winner's is at least as fresh as ours, so losing the race
      // is the correct outcome rather than something to retry over the top of. Retrying would let a slow
      // checker overwrite a newer result with an older one.
      dbContext.Entry(database).State = EntityState.Detached;
    }
    finally
    {
      // A fleet sweep touches every physical database in the estate; leaving each one tracked would grow
      // the change tracker for the whole run and make every subsequent SaveChanges progressively slower.
      if (dbContext.Entry(database).State != EntityState.Detached)
      {
        dbContext.Entry(database).State = EntityState.Detached;
      }
    }
  }
}
