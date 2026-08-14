using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// Unit of work for tenant ERP persistence (ADR-017), distinct from IPlatformUnitOfWork.
//
// The separation is not cosmetic. Once the Platform and Tenant ERP databases are physically apart there is
// no single atomic transaction spanning both, and ADR-017 prohibits assuming distributed-transaction
// atomicity. Two named units of work make that boundary visible in handler signatures: a handler that
// commits to both planes has to say so, rather than appearing to commit once.
public interface ITenantUnitOfWork
{
  Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default);

  Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
