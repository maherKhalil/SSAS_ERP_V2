using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Departments;

// THE DEPARTMENT'S BUSINESS IDENTIFIER (REQ-HR-0100, ADR-026).
//
// USER-ENTERED, never generated. `BR-PLT-0006` lists per-company configurable numbering sequences, but no
// numbering mechanism exists in the platform and FP-006 declined to build one for `EmployeeNumber`. This
// follows that precedent rather than inventing a second answer.
//
// UNIQUE WITHIN A COMPANY. Two companies in one tenant may each have a `SALES`; two departments in one
// company may not.
//
// The normalization and comparison rules are exactly the established `EmployeeNumber` / `CompanyCode`
// convention, so a reader who knows one knows this. The LENGTH differs — 64 here, per the approved Phase 1
// specification — and that is the only deliberate divergence.
public sealed class DepartmentCode : ValueObject
{
  public const int MaximumLength = 64;

  private DepartmentCode(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  // The trimmed value with display casing preserved.
  public string Value { get; }

  // Exactly Trim().ToUpperInvariant(); the column carrying it is binary-collated so comparison is ordinal.
  public string NormalizedValue { get; }

  public static Result<DepartmentCode> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<DepartmentCode>(DepartmentErrors.InvalidCode);
    }

    // No Unicode NFC/NFD/NFKC/NFKD normalization is applied: two visually identical values that differ in
    // composition are different codes, which is what makes the binary-collated index authoritative.
    var normalized = trimmed.ToUpperInvariant();

    // Uppercasing can lengthen a string in some cultures, and the limit applies to the STORED normalized
    // value as well as the input — otherwise a value could pass validation and then not fit its column.
    if (normalized.Length > MaximumLength)
    {
      return Result.Failure<DepartmentCode>(DepartmentErrors.InvalidCode);
    }

    return Result.Success(new DepartmentCode(trimmed, normalized));
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
