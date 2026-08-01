using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Authentication;

public interface IActionTokenService
{
  GeneratedActionToken Generate(AccountActionTokenPurpose purpose);

  bool TryReadPublicId(string presentedToken, out Guid publicId);

  bool Verify(AccountActionToken actionToken, string presentedToken);
}
