using System;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Subscriptions.TenantSubscriptions;

public record AppendTenantSubscriptionCommand(
  Guid TenantId,
  Guid SubscriptionPlanId,
  DateTimeOffset EffectiveFromUtc,
  SubscriptionTermKind TermKind,
  DateTimeOffset TermStartUtc,
  DateTimeOffset? TermEndUtc,
  string BillingCurrencyCode,
  string? ChangeReasonCode,
  string? ChangeReasonText
);

public sealed class AppendTenantSubscriptionCommandHandler(
  IPlatformUnitOfWork unitOfWork,
  ITenantSubscriptionRepository repository,
  ITenantEntitlementCache entitlementCache,
  ICurrentUser currentUser)
{
  public async Task<Result<Guid>> HandleAsync(AppendTenantSubscriptionCommand command, CancellationToken cancellationToken)
  {
    var maxEffective = await repository.GreatestEffectiveFromUtcAsync(command.TenantId, cancellationToken);

    var termResult = SubscriptionTerm.Rehydrate(command.TermKind, command.TermStartUtc, command.TermEndUtc);
    if (termResult.IsFailure)
    {
      return Result.Failure<Guid>(termResult.Error);
    }

    var planExists = await repository.PlanExistsAsync(command.SubscriptionPlanId, cancellationToken);
    if (!planExists)
    {
       return Result.Failure<Guid>(SubscriptionErrors.InvalidPlan);
    }

    var recordResult = TenantSubscription.Append(
      command.TenantId,
      command.SubscriptionPlanId,
      command.EffectiveFromUtc,
      maxEffective,
      termResult.Value,
      command.BillingCurrencyCode,
      currentUser.UserId ?? "System",
      command.ChangeReasonCode,
      command.ChangeReasonText,
      DateTimeOffset.UtcNow
    );

    if (recordResult.IsFailure)
    {
      return Result.Failure<Guid>(recordResult.Error);
    }

    await repository.AddAsync(recordResult.Value, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    // Invalidate entitlement cache for the tenant
    entitlementCache.InvalidateTenant(command.TenantId);

    return Result.Success(recordResult.Value.Id);
  }
}
