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

  private DepartmentName(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  // Trimmed, with display casing preserved exactly as entered.
  public string Value { get; }

  // Upper-invariant and trimmed, for SEARCH and nothing else (`DEC-POS-0030`). It backs no index and no
  // uniqueness rule — two departments in one company may share a name — and it exists because the search
  // predicate must run against a plain string column.
  //
  // ---- IT ARRIVED LATE, AND THE REASON IS WORTH KNOWING.
  //
  // FP-007's department search filtered on `Name.Value.Contains(text)`. `Name` is mapped with a value
  // converter, and EF Core cannot translate a member access through a converter inside a PREDICATE — so
  // every search carrying a `searchText` threw `InvalidOperationException` instead of returning rows, from
  // FP-007 until FP-008 Phase 2. No test covered that path. This column is the fix.
  public string NormalizedValue { get; }

  public static Result<DepartmentName> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<DepartmentName>(DepartmentErrors.InvalidName);
    }

    var normalized = trimmed.ToUpperInvariant();

    // The stored normalized value has its own column of the same width. Defensive and unreachable on .NET
    // for the reason `DepartmentCode` now records: invariant casing never changes a string's length.
    if (normalized.Length > MaximumLength)
    {
      return Result.Failure<DepartmentName>(DepartmentErrors.InvalidName);
    }

    return Result.Success(new DepartmentName(trimmed, normalized));
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
