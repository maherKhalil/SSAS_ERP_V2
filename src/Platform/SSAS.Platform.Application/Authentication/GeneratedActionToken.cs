namespace SSAS.Platform.Application.Authentication;

public sealed class GeneratedActionToken
{
  private readonly byte[] secretHash;

  public GeneratedActionToken(Guid publicId, byte[] secretHash, SensitiveActionToken sensitiveToken)
  {
    ArgumentNullException.ThrowIfNull(secretHash);
    ArgumentNullException.ThrowIfNull(sensitiveToken);
    PublicId = publicId;
    this.secretHash = secretHash.ToArray();
    SensitiveToken = sensitiveToken;
  }

  public Guid PublicId { get; }

  public SensitiveActionToken SensitiveToken { get; }

  internal byte[] SecretHash => secretHash.ToArray();
}
