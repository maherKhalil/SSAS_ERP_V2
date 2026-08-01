namespace SSAS.Platform.Application.Authentication;

public interface IAuthenticationClientRegistry
{
  bool IsAllowed(AuthenticationClientId clientId);
}
