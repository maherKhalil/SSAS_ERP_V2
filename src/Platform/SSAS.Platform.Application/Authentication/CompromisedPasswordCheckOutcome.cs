namespace SSAS.Platform.Application.Authentication;

public enum CompromisedPasswordCheckOutcome
{
  Safe = 1,
  Compromised = 2,
  Unavailable = 3
}
