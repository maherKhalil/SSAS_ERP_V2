namespace SSAS.BuildingBlocks.Application.Abstractions.Identity;

public interface IPasswordHashingService
{
  string HashPassword(string password);

  PasswordVerificationOutcome VerifyPassword(string passwordHash, string providedPassword);

  void PerformDummyVerification(string providedPassword);
}
