namespace SSAS.Platform.Application.Authentication;

public sealed class GeneratedRefreshToken
{
  private readonly byte[] secretHash;

  public GeneratedRefreshToken(Guid publicId, byte[] secretHash, SensitiveRefreshToken sensitiveToken)
  {
    if (publicId == Guid.Empty || secretHash is not { Length: 32 })
    {
      throw new ArgumentException("The generated refresh-token values are invalid.");
    }

    ArgumentNullException.ThrowIfNull(sensitiveToken);
    PublicId = publicId;
    this.secretHash = secretHash.ToArray();
    SensitiveToken = sensitiveToken;
  }

  public Guid PublicId { get; }
  public SensitiveRefreshToken SensitiveToken { get; }
  internal byte[] SecretHash => secretHash.ToArray();
}
