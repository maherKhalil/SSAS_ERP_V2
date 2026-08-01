using Microsoft.AspNetCore.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;

namespace SSAS.Platform.Infrastructure.Identity;

public sealed class AspNetPasswordHashingService : IPasswordHashingService
{
  private static readonly object PasswordOwner = new();
  private readonly IPasswordHasher<object> passwordHasher;
  private readonly string dummyPasswordHash;

  public AspNetPasswordHashingService(IPasswordHasher<object> passwordHasher)
  {
    this.passwordHasher = passwordHasher;
    dummyPasswordHash = passwordHasher.HashPassword(PasswordOwner, "SSAS credential timing equalizer");
  }

  public string HashPassword(string password)
  {
    ArgumentNullException.ThrowIfNull(password);
    if (password.Length == 0)
    {
      throw new ArgumentException("Password cannot be empty.", nameof(password));
    }

    return passwordHasher.HashPassword(PasswordOwner, password);
  }

  public PasswordVerificationOutcome VerifyPassword(string passwordHash, string providedPassword)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
    ArgumentNullException.ThrowIfNull(providedPassword);

    var result = passwordHasher.VerifyHashedPassword(PasswordOwner, passwordHash, providedPassword);

    return result switch
    {
      PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
      PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
      _ => PasswordVerificationOutcome.Failed
    };
  }

  public void PerformDummyVerification(string providedPassword)
  {
    ArgumentNullException.ThrowIfNull(providedPassword);
    _ = passwordHasher.VerifyHashedPassword(PasswordOwner, dummyPasswordHash, providedPassword);
  }
}
