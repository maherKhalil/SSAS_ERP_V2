using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Authentication;

public sealed record AuthenticationClientId
{
  public const int MaximumLength = 64;
  public const string V1Web = "ssas-erp-web";

  private AuthenticationClientId(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<AuthenticationClientId> Create(string? value)
  {
    return string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength ||
      !string.Equals(value, value.Trim(), StringComparison.Ordinal)
      ? Result.Failure<AuthenticationClientId>(AuthenticationErrors.InvalidClientId)
      : Result.Success(new AuthenticationClientId(value));
  }

  public override string ToString() => Value;
}
