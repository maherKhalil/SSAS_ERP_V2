namespace SSAS.BuildingBlocks.Application.Abstractions.Identity;

public interface IPasswordHashingService
{
  string HashPassword(string password);

  bool VerifyPassword(string passwordHash, string providedPassword);
}
