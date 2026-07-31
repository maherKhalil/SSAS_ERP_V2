using System.Net.Mail;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class EmailAddress : ValueObject
{
  private EmailAddress(string value)
  {
    Value = value;
    NormalizedEmail = value.ToUpperInvariant();
  }

  public string Value { get; }

  public string NormalizedEmail { get; }

  public static Result<EmailAddress> Create(string value)
  {
    var trimmed = value?.Trim();
    return string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 320 || !MailAddress.TryCreate(trimmed, out _)
      ? Result.Failure<EmailAddress>(IdentityAccessErrors.InvalidEmail)
      : Result.Success(new EmailAddress(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedEmail;
  }
}
