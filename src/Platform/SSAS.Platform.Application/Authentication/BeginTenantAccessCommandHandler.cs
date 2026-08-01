using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Application.Authentication;

public sealed class BeginTenantAccessCommandHandler(
  IAuthenticationAccountRepository accountRepository,
  ITenantSelectionTransactionRepository selectionRepository,
  IIdentityTenantMembershipReadService membershipReadService,
  IAuthenticationClientRegistry clientRegistry,
  IAuthenticationTokenService tokenService,
  AuthenticationSessionCreator sessionCreator,
  IPlatformUnitOfWork unitOfWork,
  AuthenticationPolicy policy,
  IDateTimeProvider clock)
{
  public async Task<Result<BeginTenantAccessResult>> HandleAsync(
    BeginTenantAccessCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    if (command.VerifiedIdentity is null || command.ClientId is null || !clientRegistry.IsAllowed(command.ClientId))
    {
      return Result.Failure<BeginTenantAccessResult>(AuthenticationErrors.GenericCredentialFailure);
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var account = await accountRepository.GetByIdentityIdForUpdateAsync(
      command.VerifiedIdentity.IdentityId,
      cancellationToken);
    if (!Matches(account, command.VerifiedIdentity))
    {
      return Result.Failure<BeginTenantAccessResult>(AuthenticationErrors.GenericCredentialFailure);
    }

    var memberships = await membershipReadService.ListEligibleMembershipsAsync(account!.IdentityId, cancellationToken);
    if (memberships.Count == 0)
    {
      return Result.Success<BeginTenantAccessResult>(new NoEligibleMembership());
    }

    var now = clock.UtcNow;
    if (memberships.Count == 1)
    {
      var eligibility = await membershipReadService.GetMembershipEligibilityForUpdateAsync(
        account.IdentityId,
        memberships[0].TenantUserId,
        memberships[0].TenantId,
        cancellationToken);
      if (!eligibility.IsEligible)
      {
        return Result.Success<BeginTenantAccessResult>(new NoEligibleMembership());
      }

      var created = await sessionCreator.CreateAsync(account, eligibility.Membership!, command.ClientId, now, cancellationToken);
      if (created.IsFailure)
      {
        return Result.Failure<BeginTenantAccessResult>(created.Error);
      }

      await transaction.CommitAsync(cancellationToken);
      return Result.Success<BeginTenantAccessResult>(new TenantSelectedAutomatically(created.Value));
    }

    var generated = tokenService.GenerateTenantSelectionProof(
      account.IdentityId,
      account.SecurityVersion,
      command.ClientId);
    var selection = TenantSelectionTransaction.Create(
      generated.PublicId,
      account.IdentityId,
      command.ClientId.Value,
      account.SecurityVersion,
      generated.SecretHash,
      now,
      now.Add(policy.TenantSelectionLifetime),
      Guid.NewGuid());
    await selectionRepository.AddAsync(selection, cancellationToken);
    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return Result.Failure<BeginTenantAccessResult>(saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);
    var summaries = memberships
      .Select(membership => new TenantMembershipSelectionSummary(
        membership.TenantUserId,
        membership.TenantId,
        membership.DisplayName))
      .ToArray();
    return Result.Success<BeginTenantAccessResult>(new TenantSelectionRequired(summaries, generated.SensitiveProof));
  }

  private static bool Matches(AuthenticationAccount? account, VerifiedIdentity verifiedIdentity) =>
    account is not null && account.IsAuthenticationEligible && account.IdentityId == verifiedIdentity.IdentityId &&
    account.SecurityVersion == verifiedIdentity.SecurityVersion;
}
