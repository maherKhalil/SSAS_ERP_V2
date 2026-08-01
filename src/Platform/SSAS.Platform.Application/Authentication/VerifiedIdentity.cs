namespace SSAS.Platform.Application.Authentication;

public sealed class VerifiedIdentity
{
  internal VerifiedIdentity(long identityId, long securityVersion)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(identityId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(securityVersion);
    IdentityId = identityId;
    SecurityVersion = securityVersion;
  }

  public long IdentityId { get; }
  public long SecurityVersion { get; }

  public override string ToString() => "VerifiedIdentity [INTERNAL AUTHENTICATION CAPABILITY]";
}
