using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Subscriptions.EnabledModules;

public sealed class GetEnabledModulesQueryHandler(
    ICurrentTenant currentTenant,
    ITenantEntitlementReader reader,
    ITenantEntitlementCache cache,
    IDateTimeProvider clock)
{
    public async Task<Result<IEnumerable<string>>> Handle(GetEnabledModulesQuery request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            return Result.Success<IEnumerable<string>>(Array.Empty<string>());
        }

        if (!cache.TryGet(tenantId, out var snapshot))
        {
            snapshot = await reader.ReadAsync(tenantId, cancellationToken);
            cache.Store(snapshot);
        }

        var instant = clock.UtcNow;

        if (snapshot.Term is null || snapshot.Term.HasExpiredAt(instant))
        {
            return Result.Success<IEnumerable<string>>(Array.Empty<string>());
        }

        var enabledModules = new HashSet<string>(snapshot.PlanModules, StringComparer.Ordinal);

        var validGrants = snapshot.Grants
            .Where(g => g.Kind == EntitlementGrantKind.ModuleGrant 
                && g.EffectiveFromUtc <= instant 
                && g.IsInForceAt(instant)
                && !string.IsNullOrWhiteSpace(g.ModuleKey));

        foreach (var grant in validGrants)
        {
            enabledModules.Add(grant.ModuleKey!);
        }

        return Result.Success<IEnumerable<string>>(enabledModules);
    }
}
