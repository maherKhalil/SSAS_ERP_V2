using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Employees;

// THE EMPLOYEE'S DISPLAY NAME (FP-006 domain-model).
//
// ONE FIELD, DELIBERATELY. Decomposed name parts, transliteration and localized name forms are real HR
// requirements, but `HR.md` states none of them — and a speculative structure here would fix a shape that
// later requirements may contradict, in a column that is expensive to reshape once data exists.
//
// Mutable through the profile update operation, and not unique: two people may share a name.
public sealed class EmployeeFullName : ValueObject
{
  public const int MaximumLength = 200;

  private EmployeeFullName(string value)
  {
    Value = value;
  }

  // Trimmed, with display casing preserved exactly as entered.
  public string Value { get; }

  public static Result<EmployeeFullName> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<EmployeeFullName>(EmployeeErrors.InvalidFullName);
    }

    return Result.Success(new EmployeeFullName(trimmed));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }

  private static bool ContainsControlCharacter(string value)
  {
    foreach (var character in value)
    {
      if (char.IsControl(character))
      {
        return true;
      }
    }

    return false;
  }
}
