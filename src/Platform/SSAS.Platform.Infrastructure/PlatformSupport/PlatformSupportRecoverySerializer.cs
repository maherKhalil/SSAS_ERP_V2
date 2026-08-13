using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.PlatformSupport;

// Serializes concurrent genesis/recovery workers on ONE resource common to every configured candidate
// (DEC-TEN-0019 / DEC-TEN-0026): an exclusive lock on the platform-support principal table itself, taken
// inside the caller's transaction and therefore held until that transaction commits or rolls back.
//
// Why the table and not a row: at genesis the table is empty, so there is no row or key range to lock, and a
// candidate-keyed lock (identity/principal/subject) would let two workers holding two DIFFERENT candidate
// locks both proceed — exactly the multi-subject race this exists to close. The table is the smallest
// existing resource every recovery worker necessarily contends on, and it needs no schema, no coordination
// row and no new locking primitive: it reuses the raw-SQL table-hint convention already used for the
// UPDLOCK/HOLDLOCK reads elsewhere in platform persistence.
//
// Cost is confined to the recovery path: callers evaluate authority unserialized first and only reach here
// when the platform is actually missing general or administrative authority, so a healthy host start never
// takes this lock.
public sealed class PlatformSupportRecoverySerializer(PlatformDbContext dbContext) : IPlatformSupportRecoverySerializer
{
  // Bounded wait: a worker that cannot serialize fails closed instead of recovering unserialized. The value is
  // generous enough to absorb a peer's full recovery write, short enough not to hang a host start forever.
  private const int LockTimeoutMilliseconds = 30_000;
  private const int LockRequestTimeoutError = 1222;

  public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      // TABLOCKX + HOLDLOCK: exclusive, held to the end of the enclosing transaction. LOCK_TIMEOUT is a
      // session setting that the connection pool resets when the connection is returned.
      await dbContext.Database.ExecuteSqlRawAsync(
        $"SET LOCK_TIMEOUT {LockTimeoutMilliseconds}; SELECT TOP 1 1 FROM [platform].[PlatformSupportPrincipals] WITH (TABLOCKX, HOLDLOCK);",
        cancellationToken);
      return true;
    }
    catch (SqlException exception) when (exception.Number == LockRequestTimeoutError)
    {
      return false;
    }
  }
}
