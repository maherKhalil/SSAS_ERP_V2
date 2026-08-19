using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Employees;

// THE EMPLOYEE'S BUSINESS IDENTIFIER (BR-HR-0001, DEC-EMP-0009, DEC-EMP-0010).
//
// USER-ENTERED IN V1. BR-PLT-0006 lists Employee Number among the per-company configurable numbering
// sequences, but no numbering mechanism exists in the platform and FP-005 explicitly excluded building one.
// So V1 accepts a caller-supplied value and enforces uniqueness by index.
//
// IT IS AN INPUT, NOT A CLIENT-OWNED IDENTITY. That distinction is what keeps a future generator additive:
// it supplies the value server-side before the aggregate is constructed, with no change to the column, the
// index, the constraint, or the resource shape.
//
// UNIQUE WITHIN A COMPANY, NOT A BRANCH. `BR-HR-0001` scopes the rule to the company, and ADR-023 states
// that Employee uniqueness which is company-wide must not include BranchId. Two employees in different
// branches of one company therefore cannot share a number, which is the intended reading.
//
// The length, normalization and comparison rules are exactly the established CompanyCode convention
// (DEC-CMP-0006, DEC-CMP-0007), so a reader who knows one knows the other.
public sealed class EmployeeNumber : ValueObject
{
  public const int MaximumLength = 64;

  private EmployeeNumber(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  // The trimmed value with display casing preserved.
  public string Value { get; }

  // Exactly Trim().ToUpperInvariant(); the column carrying it is binary-collated so comparison is ordinal.
  public string NormalizedValue { get; }

  public static Result<EmployeeNumber> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<EmployeeNumber>(EmployeeErrors.InvalidEmployeeNumber);
    }

    // No Unicode NFC/NFD/NFKC/NFKD normalization is applied: two visually identical values that differ in
    // composition are different numbers, which is what makes the binary-collated index authoritative.
    var normalized = trimmed.ToUpperInvariant();

    // Uppercasing can lengthen a string in some cultures, and the limit applies to the STORED normalized
    // value as well as the input — otherwise a value could pass validation and then not fit its column.
    if (normalized.Length > MaximumLength)
    {
      return Result.Failure<EmployeeNumber>(EmployeeErrors.InvalidEmployeeNumber);
    }

    return Result.Success(new EmployeeNumber(trimmed, normalized));
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
