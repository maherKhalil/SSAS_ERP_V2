namespace SSAS.Platform.Application.Authentication;

public sealed class GeneratedTenantSelectionProof
{
  private readonly byte[] secretHash;

  public GeneratedTenantSelectionProof(Guid publicId, byte[] secretHash, SensitiveTenantSelectionProof sensitiveProof)
  {
    if (publicId == Guid.Empty || secretHash is not { Length: 32 })
    {
      throw new ArgumentException("The generated tenant-selection values are invalid.");
    }

    ArgumentNullException.ThrowIfNull(sensitiveProof);
    PublicId = publicId;
    this.secretHash = secretHash.ToArray();
    SensitiveProof = sensitiveProof;
  }

  public Guid PublicId { get; }
  public SensitiveTenantSelectionProof SensitiveProof { get; }
  internal byte[] SecretHash => secretHash.ToArray();
}
