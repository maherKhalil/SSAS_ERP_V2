namespace SSAS.BuildingBlocks.Application.Abstractions.Identity;

public interface ICurrentUser
{
  string? UserId { get; }

  string? UserName { get; }
}
