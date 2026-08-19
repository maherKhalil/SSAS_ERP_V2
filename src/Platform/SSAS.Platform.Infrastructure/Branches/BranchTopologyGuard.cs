using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Branches;

// THE SAME RESOURCE B1a's DEACTIVATION TAKES — deliberately, and this is the whole point of the type.
//
// It delegates to BranchTopologyLock rather than defining a second lock name. Two differently-named locks
// would each work perfectly and protect nothing together: branch deactivation and branch-assignment editing
// would serialise against their own kind and interleave with each other, which is exactly the R1/R2 pair
// B1a documented as open.
internal sealed class BranchTopologyGuard(PlatformDbContext platform) : IBranchTopologyGuard
{
  private static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(15);

  public async Task<IBranchTopologyLease?> AcquireAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty)
    {
      return null;
    }

    // A DEDICATED CONNECTION, so the lease is independent of whatever transaction the caller later opens on
    // the context. A session-owned lock on the context's own connection would be released or entangled by
    // that transaction's lifetime, which is not the scope this needs.
    var connection = new SqlConnection(platform.Database.GetConnectionString());
    await connection.OpenAsync(cancellationToken);

    if (await BranchTopologyLock.TryAcquireForSessionAsync(
      connection, tenantId, LeaseTimeout, cancellationToken))
    {
      return new Lease(tenantId, connection);
    }

    await connection.DisposeAsync();
    return null;
  }

  private sealed class Lease(Guid tenantId, SqlConnection connection) : IBranchTopologyLease
  {
    public Guid TenantId => tenantId;

    // Closing the connection releases the session lock. Nothing else has to happen, and nothing is left
    // behind if the process dies first.
    public ValueTask DisposeAsync() => connection.DisposeAsync();
  }
}
