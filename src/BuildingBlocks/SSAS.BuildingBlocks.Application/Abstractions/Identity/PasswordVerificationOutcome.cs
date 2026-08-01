namespace SSAS.BuildingBlocks.Application.Abstractions.Identity;

public enum PasswordVerificationOutcome
{
  Failed = 1,
  Success = 2,
  SuccessRehashNeeded = 3
}
