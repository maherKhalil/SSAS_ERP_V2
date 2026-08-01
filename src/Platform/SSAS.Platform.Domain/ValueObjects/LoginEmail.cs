using System.Net.Mail;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class LoginEmail : ValueObject
{
  private LoginEmail(string value)
  {
    Value = value;
    NormalizedValue = value.ToUpperInvariant();
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<LoginEmail> Create(string value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 320 || !MailAddress.TryCreate(trimmed, out _)
      ? Result.Failure<LoginEmail>(AuthenticationErrors.InvalidLoginEmail)
      : Result.Success(new LoginEmail(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
  }
}
