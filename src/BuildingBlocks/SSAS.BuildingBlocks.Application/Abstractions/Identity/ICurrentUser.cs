namespace SSAS.BuildingBlocks.Application.Abstractions.Identity;

public interface ICurrentUser
{
  string? UserId { get; }

  string? UserName { get; }

  string? Email { get; }

  Guid? CompanyId { get; }

  string? SessionId { get; }

  string? TokenId { get; }

  IReadOnlyCollection<string> Roles { get; }

  IReadOnlyCollection<string> Permissions { get; }
}
