using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Departments;

// THE DEPARTMENT'S DISPLAY NAME (REQ-HR-0100).
//
// NOT UNIQUE, and not normalized for comparison. Two departments in different parts of one hierarchy may
// legitimately share a name — "Support" under Sales and "Support" under Operations are different units —
// and the CODE is what distinguishes them. Adding a uniqueness rule here would forbid a shape organizations
// actually have.
//
// Same length and same validation as `EmployeeFullName`, so the two read alike.
public sealed class DepartmentName : ValueObject
{
  public const int MaximumLength = 200;

  private DepartmentName(string value)
  {
    Value = value;
  }

  // Trimmed, with display casing preserved exactly as entered.
  public string Value { get; }

  public static Result<DepartmentName> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<DepartmentName>(DepartmentErrors.InvalidName);
    }

    return Result.Success(new DepartmentName(trimmed));
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
