using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Employees;

// THE EMPLOYEE'S NATIONAL IDENTIFIER (BR-HR-0002, DEC-EMP-0013).
//
// OPTIONAL IN V1, AND DELIBERATELY SO. BR-HR-0002 constrains its UNIQUENESS ("National ID shall be unique
// within a company") without requiring its presence, and an employee may legitimately be recorded before
// their documentation is. Making it required would be a stricter rule than any authority states.
//
// Where present it is unique within the company, enforced by a FILTERED unique index so that many employees
// without one remain possible while every recorded value stays distinct.
//
// MUTABLE, unlike the employee number: a recorded national identity may be corrected, and there is no
// reason to force a new employment record for a transcription error.
public sealed class NationalId : ValueObject
{
  public const int MaximumLength = 64;

  private NationalId(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  public string Value { get; }

  public string NormalizedValue { get; }

  public static Result<NationalId> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<NationalId>(EmployeeErrors.InvalidNationalId);
    }

    var normalized = trimmed.ToUpperInvariant();
    if (normalized.Length > MaximumLength)
    {
      return Result.Failure<NationalId>(EmployeeErrors.InvalidNationalId);
    }

    return Result.Success(new NationalId(trimmed, normalized));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return NormalizedValue;
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
