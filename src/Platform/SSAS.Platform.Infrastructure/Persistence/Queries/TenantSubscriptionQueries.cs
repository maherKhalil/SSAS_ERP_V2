using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Subscriptions.TenantSubscriptions;

namespace SSAS.Platform.Infrastructure.Persistence.Queries;

internal sealed class TenantSubscriptionQueries(PlatformDbContext dbContext) : ITenantSubscriptionQueries
{
  public async Task<Result<IEnumerable<TenantSubscriptionDto>>> GetTenantSubscriptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
  {
    var dtos = await dbContext.TenantSubscriptions
      .AsNoTracking()
      .Where(x => x.TenantId == tenantId)
      .OrderByDescending(x => x.EffectiveFromUtc)
      .Select(x => new TenantSubscriptionDto
      {
        TenantSubscriptionId = x.Id,
        TenantId = x.TenantId,
        SubscriptionPlanId = x.SubscriptionPlanId,
        EffectiveFromUtc = x.EffectiveFromUtc,
        BillingCurrencyCode = x.BillingCurrencyCode,
        CreatedUtc = x.CreatedUtc,
        ChangedBy = x.ChangedBy,
        ChangeReasonCode = x.ChangeReasonCode,
        ChangeReasonText = x.ChangeReasonText,
        Term = new SubscriptionTermDto
        {
          Kind = x.Term.Kind,
          StartUtc = x.Term.StartUtc,
          EndUtc = x.Term.EndUtc
        }
      })
      .ToListAsync(cancellationToken);

    return Result.Success(dtos.AsEnumerable());
  }

  public async Task<Result<TenantSubscriptionDto>> GetCurrentTenantSubscriptionAsync(Guid tenantId, DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
  {
    var current = await dbContext.TenantSubscriptions
      .AsNoTracking()
      .Where(x => x.TenantId == tenantId && x.EffectiveFromUtc <= asOfUtc)
      .OrderByDescending(x => x.EffectiveFromUtc)
      .Select(x => new TenantSubscriptionDto
      {
        TenantSubscriptionId = x.Id,
        TenantId = x.TenantId,
        SubscriptionPlanId = x.SubscriptionPlanId,
        EffectiveFromUtc = x.EffectiveFromUtc,
        BillingCurrencyCode = x.BillingCurrencyCode,
        CreatedUtc = x.CreatedUtc,
        ChangedBy = x.ChangedBy,
        ChangeReasonCode = x.ChangeReasonCode,
        ChangeReasonText = x.ChangeReasonText,
        Term = new SubscriptionTermDto
        {
          Kind = x.Term.Kind,
          StartUtc = x.Term.StartUtc,
          EndUtc = x.Term.EndUtc
        }
      })
      .FirstOrDefaultAsync(cancellationToken);

    if (current is null)
    {
      return Result.Failure<TenantSubscriptionDto>(new Error("TenantSubscription.NotFound", "No subscription record in force."));
    }

    return Result.Success(current);
  }

  public async Task<Result<IEnumerable<TenantSubscriptionDto>>> GetAllSubscriptionsAsync(CancellationToken cancellationToken = default)
  {
    var dtos = await dbContext.TenantSubscriptions
      .AsNoTracking()
      .OrderByDescending(x => x.EffectiveFromUtc)
      .Select(x => new TenantSubscriptionDto
      {
        TenantSubscriptionId = x.Id,
        TenantId = x.TenantId,
        SubscriptionPlanId = x.SubscriptionPlanId,
        EffectiveFromUtc = x.EffectiveFromUtc,
        BillingCurrencyCode = x.BillingCurrencyCode,
        CreatedUtc = x.CreatedUtc,
        ChangedBy = x.ChangedBy,
        ChangeReasonCode = x.ChangeReasonCode,
        ChangeReasonText = x.ChangeReasonText,
        Term = new SubscriptionTermDto
        {
          Kind = x.Term.Kind,
          StartUtc = x.Term.StartUtc,
          EndUtc = x.Term.EndUtc
        }
      })
      .ToListAsync(cancellationToken);

    return Result.Success(dtos.AsEnumerable());
  }
}
