using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class AuthenticationSubject : ValueObject
{
  private AuthenticationSubject(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<AuthenticationSubject> Create(string value)
  {
    return string.IsNullOrWhiteSpace(value) || value.Length > 256
      ? Result.Failure<AuthenticationSubject>(IdentityAccessErrors.InvalidIdentity)
      : Result.Success(new AuthenticationSubject(value));
  }

  public static Result<AuthenticationSubject> CreateLocal(Guid subjectId)
  {
    return subjectId == Guid.Empty
      ? Result.Failure<AuthenticationSubject>(IdentityAccessErrors.InvalidIdentity)
      : Result.Success(new AuthenticationSubject($"local:{subjectId:N}"));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }
}
