using Microsoft.AspNetCore.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;

namespace SSAS.Platform.Infrastructure.Identity;

public sealed class AspNetPasswordHashingService : IPasswordHashingService
{
  private static readonly object PasswordOwner = new();
  private readonly IPasswordHasher<object> passwordHasher;

  public AspNetPasswordHashingService(IPasswordHasher<object> passwordHasher)
  {
    this.passwordHasher = passwordHasher;
  }

  public string HashPassword(string password)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(password);

    return passwordHasher.HashPassword(PasswordOwner, password);
  }

  public bool VerifyPassword(string passwordHash, string providedPassword)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
    ArgumentException.ThrowIfNullOrWhiteSpace(providedPassword);

    var result = passwordHasher.VerifyHashedPassword(PasswordOwner, passwordHash, providedPassword);

    return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
  }
}
