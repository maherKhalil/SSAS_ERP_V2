using System;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Application.Abstractions.Persistence;

namespace SSAS.Platform.Application.Subscriptions.EntitlementGrants;

public record GrantModuleCommand(
  Guid TenantId,
  string ModuleKey,
  DateTimeOffset EffectiveFromUtc,
  DateTimeOffset? ExpiresUtc,
  string? ReasonCode,
  string? ReasonText
);

public record RaiseLimitCommand(
  Guid TenantId,
  string LimitKey,
  long LimitValue,
  DateTimeOffset EffectiveFromUtc,
  DateTimeOffset? ExpiresUtc,
  string? ReasonCode,
  string? ReasonText
);

public record RevokeModuleCommand(
  Guid TenantId,
  string ModuleKey,
  DateTimeOffset EffectiveFromUtc,
  string? ReasonCode,
  string? ReasonText
);

public record RevokeLimitCommand(
  Guid TenantId,
  string LimitKey,
  DateTimeOffset EffectiveFromUtc,
  string? ReasonCode,
  string? ReasonText
);

public class EntitlementGrantsCommandHandler(
  IPlatformUnitOfWork unitOfWork,
  ITenantEntitlementGrantRepository repository,
  ITenantEntitlementReader reader,
  ITenantEntitlementCache cache,
  ICurrentUser currentUser)
{
  public async Task<Result<Guid>> HandleGrantModuleAsync(GrantModuleCommand command, CancellationToken cancellationToken)
  {
    var moduleKeyResult = ModuleKey.Create(command.ModuleKey);
    if (moduleKeyResult.IsFailure) return Result.Failure<Guid>(moduleKeyResult.Error);

    var grantResult = TenantEntitlementGrant.GrantModule(
      command.TenantId,
      moduleKeyResult.Value,
      command.EffectiveFromUtc,
      command.ExpiresUtc,
      currentUser.UserId ?? "System",
      command.ReasonCode,
      command.ReasonText,
      DateTimeOffset.UtcNow
    );

    if (grantResult.IsFailure) return Result.Failure<Guid>(grantResult.Error);

    await repository.AddAsync(grantResult.Value, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    cache.InvalidateTenant(command.TenantId);

    return Result.Success(grantResult.Value.Id);
  }

  public async Task<Result<Guid>> HandleRaiseLimitAsync(RaiseLimitCommand command, CancellationToken cancellationToken)
  {
    var snapshot = await reader.ReadAsync(command.TenantId, cancellationToken);
    // If we want to check plan limit value, we get it from the snapshot
    // But wait, the snapshot gives us the MAX of plan and grants. 
    // Wait, the method signature requires planLimitValue. Let's just pass the plan limit from snapshot.
    long? planLimitValue = snapshot.PlanLimits.TryGetValue(command.LimitKey, out var val) ? val : null;

    var grantResult = TenantEntitlementGrant.RaiseLimit(
      command.TenantId,
      command.LimitKey,
      command.LimitValue,
      planLimitValue,
      command.EffectiveFromUtc,
      command.ExpiresUtc,
      currentUser.UserId ?? "System",
      command.ReasonCode,
      command.ReasonText,
      DateTimeOffset.UtcNow
    );

    if (grantResult.IsFailure) return Result.Failure<Guid>(grantResult.Error);

    await repository.AddAsync(grantResult.Value, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    cache.InvalidateTenant(command.TenantId);

    return Result.Success(grantResult.Value.Id);
  }

  public async Task<Result<Guid>> HandleRevokeModuleAsync(RevokeModuleCommand command, CancellationToken cancellationToken)
  {
    var moduleKeyResult = ModuleKey.Create(command.ModuleKey);
    if (moduleKeyResult.IsFailure) return Result.Failure<Guid>(moduleKeyResult.Error);

    var grantResult = TenantEntitlementGrant.RevokeModule(
      command.TenantId,
      moduleKeyResult.Value,
      command.EffectiveFromUtc,
      currentUser.UserId ?? "System",
      command.ReasonCode,
      command.ReasonText,
      DateTimeOffset.UtcNow
    );

    if (grantResult.IsFailure) return Result.Failure<Guid>(grantResult.Error);

    await repository.AddAsync(grantResult.Value, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    cache.InvalidateTenant(command.TenantId);

    return Result.Success(grantResult.Value.Id);
  }

  public async Task<Result<Guid>> HandleRevokeLimitAsync(RevokeLimitCommand command, CancellationToken cancellationToken)
  {
    var grantResult = TenantEntitlementGrant.RevokeLimit(
      command.TenantId,
      command.LimitKey,
      command.EffectiveFromUtc,
      currentUser.UserId ?? "System",
      command.ReasonCode,
      command.ReasonText,
      DateTimeOffset.UtcNow
    );

    if (grantResult.IsFailure) return Result.Failure<Guid>(grantResult.Error);

    await repository.AddAsync(grantResult.Value, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    cache.InvalidateTenant(command.TenantId);

    return Result.Success(grantResult.Value.Id);
  }
}
