using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Authentication;

public sealed class IssueTenantUserInvitationCommandHandler(
  IIdentityRepository identityRepository,
  ITenantUserRepository tenantUserRepository,
  IAuthenticationAccountRepository authenticationAccountRepository,
  IAccountActionTokenRepository actionTokenRepository,
  IPlatformUnitOfWork unitOfWork,
  IActionTokenService actionTokenService,
  AuthenticationPolicy policy,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<SensitiveActionToken>> HandleAsync(
    IssueTenantUserInvitationCommand command,
    CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<SensitiveActionToken>(execution.Error);
    }

    var loginEmail = LoginEmail.Create(command.LoginEmail);
    var tenantEmail = EmailAddress.Create(command.LoginEmail);
    var displayName = UserDisplayName.Create(command.DisplayName);
    if (loginEmail.IsFailure || tenantEmail.IsFailure || displayName.IsFailure)
    {
      var error = loginEmail.IsFailure ? loginEmail.Error : tenantEmail.IsFailure ? tenantEmail.Error : displayName.Error;
      return Result.Failure<SensitiveActionToken>(error);
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var account = await authenticationAccountRepository.GetByNormalizedLoginEmailAsync(
      loginEmail.Value.NormalizedValue,
      cancellationToken);
    long identityId;
    if (account is null)
    {
      var subject = AuthenticationSubject.CreateLocal(Guid.NewGuid());
      if (subject.IsFailure)
      {
        return Result.Failure<SensitiveActionToken>(subject.Error);
      }

      var identity = Identity.Create(subject.Value);
      await identityRepository.AddAsync(identity, cancellationToken);
      var identitySave = await unitOfWork.SaveChangesAsync(cancellationToken);
      if (identitySave.IsFailure)
      {
        return Result.Failure<SensitiveActionToken>(identitySave.Error);
      }

      identityId = identity.Id;
      account = AuthenticationAccount.CreatePending(identityId, loginEmail.Value);
      await authenticationAccountRepository.AddAsync(account, cancellationToken);
      var accountSave = await unitOfWork.SaveChangesAsync(cancellationToken);
      if (accountSave.IsFailure)
      {
        return Result.Failure<SensitiveActionToken>(accountSave.Error);
      }
    }
    else
    {
      identityId = account.IdentityId;
    }

    var tenantUser = await tenantUserRepository.GetByIdentityIdAsync(identityId, cancellationToken);
    if (tenantUser?.Status == TenantUserStatus.Active)
    {
      return Result.Failure<SensitiveActionToken>(AuthenticationErrors.ActiveMembershipCannotBeInvited);
    }

    if (tenantUser?.Status == TenantUserStatus.Deactivated)
    {
      return Result.Failure<SensitiveActionToken>(AuthenticationErrors.DeactivatedMembershipRequiresReactivation);
    }

    if (tenantUser is null)
    {
      tenantUser = TenantUser.CreatePending(
        identityId,
        execution.Value.TenantId,
        tenantEmail.Value,
        displayName.Value,
        Guid.NewGuid(),
        clock.UtcNow);
      await tenantUserRepository.AddAsync(tenantUser, cancellationToken);
      var membershipSave = await unitOfWork.SaveChangesAsync(cancellationToken);
      if (membershipSave.IsFailure)
      {
        return Result.Failure<SensitiveActionToken>(membershipSave.Error);
      }
    }

    var now = clock.UtcNow;
    var priorToken = await actionTokenRepository.GetActiveInvitationAsync(
      execution.Value.TenantId,
      tenantUser.Id,
      now,
      cancellationToken);
    if (priorToken is not null)
    {
      var revokeResult = priorToken.Revoke(execution.Value.Actor, "Replaced", Guid.NewGuid(), now);
      if (revokeResult.IsFailure)
      {
        return Result.Failure<SensitiveActionToken>(revokeResult.Error);
      }
    }

    var generated = actionTokenService.Generate(AccountActionTokenPurpose.Invitation);
    var actionToken = AccountActionToken.CreateInvitation(
      generated.PublicId,
      generated.SecretHash,
      identityId,
      account.Id,
      execution.Value.TenantId,
      tenantUser.Id,
      now,
      now.Add(policy.InvitationLifetime),
      Guid.NewGuid());
    await actionTokenRepository.AddAsync(actionToken, cancellationToken);
    var tokenSave = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (tokenSave.IsFailure)
    {
      return Result.Failure<SensitiveActionToken>(tokenSave.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    return Result.Success(generated.SensitiveToken);
  }
}
