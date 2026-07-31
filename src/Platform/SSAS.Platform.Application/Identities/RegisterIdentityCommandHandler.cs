using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Application.Identities;

public sealed class RegisterIdentityCommandHandler(
  IIdentityRepository identityRepository,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result<long>> HandleAsync(RegisterIdentityCommand command, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<long>(IdentityAccessErrors.Unauthorized);
    }

    var subject = AuthenticationSubject.Create(command.Subject);
    if (subject.IsFailure)
    {
      return Result.Failure<long>(subject.Error);
    }

    if (await identityRepository.SubjectExistsAsync(subject.Value.Value, cancellationToken))
    {
      return Result.Failure<long>(new Error("Identity.SubjectExists", "The authentication subject already exists."));
    }

    var identity = Identity.Create(subject.Value);
    await identityRepository.AddAsync(identity, cancellationToken);
    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saveResult.IsFailure ? Result.Failure<long>(saveResult.Error) : Result.Success(identity.Id);
  }
}
