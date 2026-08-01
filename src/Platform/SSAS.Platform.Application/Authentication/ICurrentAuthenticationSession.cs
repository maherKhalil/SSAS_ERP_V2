namespace SSAS.Platform.Application.Authentication;

public interface ICurrentAuthenticationSession
{
  CurrentAuthenticationSession? Value { get; }
}
