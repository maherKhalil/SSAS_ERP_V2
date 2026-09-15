using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Subscriptions.EntitlementGrants;

public record TenantEntitlementGrantDto(
  Guid TenantEntitlementGrantId,
  Guid TenantId,
  string GrantKind,
  string? ModuleKey,
  string? LimitKey,
  long? LimitValue,
  DateTimeOffset EffectiveFromUtc,
  DateTimeOffset? ExpiresUtc,
  DateTimeOffset CreatedUtc,
  string GrantedBy,
  string? ReasonCode,
  string? ReasonText
);

public interface ITenantEntitlementGrantQueries
{
  Task<Result<IReadOnlyList<TenantEntitlementGrantDto>>> GetTenantGrantsAsync(Guid tenantId, CancellationToken cancellationToken);
}

public record GetTenantGrantsQuery(Guid TenantId);

public class GetTenantGrantsQueryHandler(ITenantEntitlementGrantQueries queries)
{
  public Task<Result<IReadOnlyList<TenantEntitlementGrantDto>>> HandleAsync(GetTenantGrantsQuery query, CancellationToken cancellationToken)
  {
    return queries.GetTenantGrantsAsync(query.TenantId, cancellationToken);
  }
}
