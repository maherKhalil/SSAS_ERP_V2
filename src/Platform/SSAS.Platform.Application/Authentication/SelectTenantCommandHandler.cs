using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Authentication;

public sealed class SelectTenantCommandHandler(
  IAuthenticationAccountRepository accountRepository,
  ITenantSelectionTransactionRepository selectionRepository,
  IIdentityTenantMembershipReadService membershipReadService,
  IAuthenticationClientRegistry clientRegistry,
  IAuthenticationTokenService tokenService,
  AuthenticationSessionCreator sessionCreator,
  IPlatformUnitOfWork unitOfWork,
  IDateTimeProvider clock)
{
  public async Task<Result<SessionCreated>> HandleAsync(
    SelectTenantCommand command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    if (command.SelectionProof is null || command.ClientId is null || command.TenantUserId <= 0 || command.TenantId == Guid.Empty ||
      !clientRegistry.IsAllowed(command.ClientId) || !tokenService.TryReadPublicId(command.SelectionProof, out var publicId))
    {
      return GenericFailure();
    }

    var identityId = await selectionRepository.GetIdentityIdByPublicIdAsync(publicId, cancellationToken);
    if (!identityId.HasValue)
    {
      return GenericFailure();
    }

    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
    var account = await accountRepository.GetByIdentityIdForUpdateAsync(identityId.Value, cancellationToken);
    if (account is null || !account.IsAuthenticationEligible)
    {
      return GenericFailure();
    }

    var selection = await selectionRepository.GetByPublicIdForUpdateAsync(publicId, cancellationToken);
    var now = clock.UtcNow;
    if (selection is null || selection.IdentityId != account.IdentityId || selection.SecurityVersionAtAuthentication != account.SecurityVersion ||
      !selection.IsActive(now) || !string.Equals(selection.ClientId, command.ClientId.Value, StringComparison.Ordinal) ||
      !tokenService.VerifyTenantSelection(selection, command.SelectionProof))
    {
      return GenericFailure();
    }

    var eligibility = await membershipReadService.GetMembershipEligibilityForUpdateAsync(
      selection.IdentityId,
      command.TenantUserId,
      command.TenantId,
      cancellationToken);
    if (!eligibility.IsEligible)
    {
      return GenericFailure();
    }

    var created = await sessionCreator.CreateAsync(account, eligibility.Membership!, command.ClientId, now, cancellationToken);
    if (created.IsFailure)
    {
      return GenericFailure();
    }

    var consumed = selection.Consume(
      command.TenantUserId,
      command.TenantId,
      created.Value.AuthenticationSessionId,
      Guid.NewGuid(),
      now);
    if (consumed.IsFailure)
    {
      return GenericFailure();
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      return GenericFailure();
    }

    await transaction.CommitAsync(cancellationToken);
    return created;
  }

  private static Result<SessionCreated> GenericFailure() =>
    Result.Failure<SessionCreated>(AuthenticationErrors.GenericTenantSelectionFailure);
}
