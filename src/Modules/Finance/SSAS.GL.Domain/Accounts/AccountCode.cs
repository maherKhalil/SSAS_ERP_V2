using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Accounts;

// THE ACCOUNT'S BUSINESS IDENTIFIER (REQ-GL-0005, OD-GL-0003).
//
// USER-ENTERED, never generated — the same precedent `DepartmentCode` followed from `EmployeeNumber`: the
// platform has no numbering mechanism and GL is not the package that invents one.
//
// UNIQUE WITHIN THE TENANT, not within a company. `OD-GL-0003` ruled the chart of accounts TENANT-level, so
// every company in a tenant reads one chart and two companies cannot each own a `4100`. That is the single
// substantive divergence from `DepartmentCode`, and it is a consequence of the ruling rather than a
// preference: an `Account` is `ITenantOwnedEntity` and nothing more.
//
// The normalization and comparison rules are exactly the established `EmployeeNumber` / `CompanyCode` /
// `DepartmentCode` convention, so a reader who knows one knows this.
public sealed class AccountCode : ValueObject
{
  public const int MaximumLength = 64;

  private AccountCode(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  // The trimmed value with display casing preserved.
  public string Value { get; }

  // Exactly Trim().ToUpperInvariant(); the column carrying it is binary-collated so comparison is ordinal.
  public string NormalizedValue { get; }

  public static Result<AccountCode> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<AccountCode>(AccountErrors.InvalidCode);
    }

    // No Unicode NFC/NFD/NFKC/NFKD normalization is applied: two visually identical values that differ in
    // composition are different codes, which is what makes the binary-collated index authoritative.
    var normalized = trimmed.ToUpperInvariant();

    // ---- THE LIMIT APPLIES TO WHAT IS STORED, NOT ONLY TO WHAT WAS TYPED.
    //
    // Defensive, and documented as unreachable rather than left to imply a case that occurs: .NET's
    // `ToUpperInvariant` uses simple 1:1 case mapping and never changes a string's length. `DepartmentCode`
    // carries the same check and the same note, after FP-008 removed a comment claiming otherwise and the
    // test written against that false premise with it. Kept because the property it protects is a column
    // width and the cost is one comparison.
    if (normalized.Length > MaximumLength)
    {
      return Result.Failure<AccountCode>(AccountErrors.InvalidCode);
    }

    return Result.Success(new AccountCode(trimmed, normalized));
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
