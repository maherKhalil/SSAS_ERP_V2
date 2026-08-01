using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Authentication;

public interface IPasswordPolicyValidator
{
  Task<Result> ValidateAsync(string password, CancellationToken cancellationToken = default);
}
