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

    // ---- THE LIMIT APPLIES TO WHAT IS STORED, NOT ONLY TO WHAT WAS TYPED.
    //
    // Both the display value and the normalized value go into `nvarchar(MaximumLength)` columns, so a value
    // that fitted before normalization and not after would pass validation and then fail to persist.
    //
    // **This check is defensive, and no test asserts it fires**, because on .NET it cannot: `ToUpperInvariant`
    // uses simple 1:1 case mapping and never changes a string's length — U+00DF (ß) and the ligatures are
    // returned unchanged rather than expanded. The check stays because the property it protects is a column
    // width, the cost is one comparison, and a future runtime or a change to the normalization rule could
    // make it reachable. It is documented as unreachable rather than left to imply a case that occurs.
    //
    // This is the LAST of three copies of a comment that claimed uppercasing "can lengthen a string in some
    // cultures". It never did on .NET. `CompanyDomainTests` has stated the correct fact since FP-005, which
    // is what makes the three copies a copied falsehood rather than a shared misunderstanding.
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
