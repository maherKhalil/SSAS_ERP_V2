using System;
using System.Threading;
using System.Threading.Tasks;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface ITenantEntitlementGrantRepository
{
  Task AddAsync(TenantEntitlementGrant grant, CancellationToken cancellationToken);
}
