using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace SSAS.Host.API.Authentication;

public sealed record SigningKeySnapshot(
  X509SecurityKey ActiveSigningKey,
  IReadOnlyDictionary<string, X509SecurityKey> EnabledVerificationKeys);

public interface ISigningKeyProvider
{
  SigningKeySnapshot Snapshot { get; }
}

public sealed class SigningKeyProvider : ISigningKeyProvider, IDisposable
{
  private readonly List<X509Certificate2> certificates = [];

  public SigningKeyProvider(Microsoft.Extensions.Options.IOptions<JwtOptions> optionsAccessor,
    IHostEnvironment environment)
  {
    var options = optionsAccessor.Value;
    var now = DateTimeOffset.UtcNow;
    X509Certificate2 active;
    if (environment.IsDevelopment())
    {
      active = CreateDevelopmentCertificate(now);
      Console.Error.WriteLine(
        "WARNING: Using a process-local ephemeral RSA JWT signing certificate. Restarting invalidates Development access tokens.");
    }
    else
    {
      active = LoadCertificate(options.ActiveSigningCertificatePath, options.ActiveSigningCertificatePassword);
    }

    ValidateActive(active, now, options.AccessTokenLifetime, TimeSpan.FromSeconds(options.ClockSkewSeconds));
    certificates.Add(active);
    var activeKey = CreateKey(active);
    var verification = new Dictionary<string, X509SecurityKey>(StringComparer.Ordinal)
    {
      [activeKey.KeyId] = activeKey
    };

    foreach (var configured in options.VerificationCertificates.Where(item => item.Enabled))
    {
      var certificate = LoadCertificate(configured.Path, null);
      ValidateVerification(certificate);
      certificates.Add(certificate);
      var key = CreateKey(certificate);
      if (!verification.TryAdd(key.KeyId, key))
        throw new InvalidOperationException($"Duplicate JWT verification kid '{key.KeyId}'.");
      if (configured.RetireAfterUtc is { } retireAfter &&
        retireAfter < now.Add(options.AccessTokenLifetime).AddSeconds(options.ClockSkewSeconds))
        throw new InvalidOperationException($"JWT verification key '{key.KeyId}' retires before the required overlap window.");
    }

    Snapshot = new SigningKeySnapshot(activeKey, verification);
  }

  public SigningKeySnapshot Snapshot { get; }

  public void Dispose()
  {
    foreach (var certificate in certificates) certificate.Dispose();
  }

  private static X509Certificate2 CreateDevelopmentCertificate(DateTimeOffset now)
  {
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=SSAS ERP Development JWT", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return request.CreateSelfSigned(now.AddMinutes(-1), now.AddDays(30));
  }

  private static X509Certificate2 LoadCertificate(string path, string? password)
  {
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
      throw new InvalidOperationException("The configured JWT certificate does not exist.");
    return new X509Certificate2(path, password, X509KeyStorageFlags.EphemeralKeySet);
  }

  private static void ValidateActive(X509Certificate2 certificate, DateTimeOffset now, TimeSpan lifetime, TimeSpan skew)
  {
    using var rsa = certificate.GetRSAPrivateKey();
    if (rsa is null) throw new InvalidOperationException("The active JWT certificate must contain an RSA private key.");
    if (rsa.KeySize < 2048) throw new InvalidOperationException("The active JWT RSA key must be at least 2048 bits.");
    if (now < certificate.NotBefore || now > certificate.NotAfter)
      throw new InvalidOperationException("The active JWT certificate is not currently valid.");
    if (certificate.NotAfter.ToUniversalTime() < now.Add(lifetime).Add(skew))
      throw new InvalidOperationException("The active JWT certificate expires before the access-token safety window.");
  }

  private static X509SecurityKey CreateKey(X509Certificate2 certificate)
  {
    var kid = Base64UrlEncoder.Encode(SHA256.HashData(certificate.RawData));
    return new X509SecurityKey(certificate) { KeyId = kid };
  }

  private static void ValidateVerification(X509Certificate2 certificate)
  {
    using var rsa = certificate.GetRSAPublicKey();
    if (rsa is null || rsa.KeySize < 2048)
      throw new InvalidOperationException("Every enabled JWT verification certificate must contain an RSA key of at least 2048 bits.");
  }
}
