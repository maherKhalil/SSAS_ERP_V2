using System.Data.Common;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Integration.Tests;

// A write fence that admits everything, for suites that predate the cutover freeze and are about routing or
// schema health rather than write admission.
//
// DELIBERATELY TEST-ONLY AND DELIBERATELY NOT A PRODUCTION OPTION. `TenantDbContextFactory` takes the fence
// as a required dependency precisely so no production composition can omit it; these suites supply this
// explicitly, which keeps "this test does not exercise the freeze" a visible statement rather than a
// default nobody notices. The freeze itself is proven in TenantCutoverFreezeSqlServerTests.
internal sealed class UnfencedTenantWrites : ITenantWriteFence
{
  public static readonly UnfencedTenantWrites Instance = new();

  public Task AdmitWriteAsync(
    Guid tenantId,
    long tenantDatabaseId,
    DbConnection connection,
    DbTransaction transaction,
    CancellationToken cancellationToken = default) => Task.CompletedTask;
}
