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
    // This comment previously claimed that uppercasing "can lengthen a string in some cultures". That is not
    // true of .NET's invariant casing, and FP-008 Phase 1 removed the identical claim from the position value
    // objects after a test written against it asserted a premise that could never hold. Corrected here on the
    // same terms, by architect ruling: identical treatment, comment only, no behaviour change.
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
