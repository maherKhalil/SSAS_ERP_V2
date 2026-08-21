namespace SSAS.HR.Domain.Positions;

// THE SHARED MECHANICS BEHIND FP-008's SIX TEXT VALUE OBJECTS.
//
// FP-008 introduces three aggregates, each with a code and a display label, so the `DepartmentCode` /
// `DepartmentName` pattern would be copied SIX times if written out. Six copies of a validator is six places
// for a rule to drift, and the drift would be silent: a code that normalizes one way in `PositionCode` and
// another in `JobGradeCode` produces two different answers to "do these collide", with a unique index
// enforcing whichever one happened to be written first.
//
// ---- THE TYPES STAY DISTINCT. ONLY THE MECHANICS ARE SHARED.
//
// This is not a step towards one `OrganizationCode` type used everywhere. `PositionCode` and `JobGradeCode`
// remain unrelated types, so neither can be passed where the other belongs, and each keeps its own error
// constant so a refusal says which aggregate refused. What is shared is the answer to "what is a valid code",
// which must be one answer.
//
// The rules themselves are `DepartmentCode`'s and `DepartmentName`'s, unchanged: trim, reject empty, reject
// control characters, bound the length, and normalize a code with `ToUpperInvariant` and nothing else. No
// Unicode NFC/NFD/NFKC/NFKD normalization is applied — two visually identical values that differ in
// composition are different codes, which is what makes the binary-collated index authoritative rather than
// approximate.
internal static class OrganizationalText
{
  // A display label: trimmed, casing preserved, never normalized for comparison. Returns null when invalid.
  internal static string? NormalizeLabel(string? value, int maximumLength)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maximumLength || ContainsControlCharacter(trimmed))
    {
      return null;
    }

    return trimmed;
  }

  // ================================================================================================
  // A DISPLAY LABEL WITH A SEARCH FORM (DEC-POS-0030)
  // ================================================================================================
  //
  // The same pair as a code — trimmed display form plus upper-invariant ordinal form — but the two exist for
  // DIFFERENT reasons, and the distinction is worth keeping visible.
  //
  //   * a CODE's normalized form decides IDENTITY: it backs a unique index, so two codes that normalize
  //     alike are the same code and the second one is refused;
  //   * a LABEL's normalized form decides nothing. It exists only so a search can be case-insensitive over a
  //     binary-collated column, and two positions may share a title forever (`BRULE-POS-0005`).
  //
  // Mechanically identical, semantically unrelated. They delegate to one implementation so the normalization
  // RULE has one definition, and are named apart so a reader cannot mistake the label form for a uniqueness
  // key and add an index to it.
  internal static bool TryNormalizeLabel(
    string? value, int maximumLength, out string trimmed, out string normalized) =>
    TryNormalizeCode(value, maximumLength, out trimmed, out normalized);

  // A business identifier: the trimmed display form plus the ordinal-comparison form. Returns false when
  // invalid, so a caller cannot accidentally use a half-built pair.
  internal static bool TryNormalizeCode(
    string? value, int maximumLength, out string trimmed, out string normalized)
  {
    trimmed = string.Empty;
    normalized = string.Empty;

    var candidate = NormalizeLabel(value, maximumLength);
    if (candidate is null)
    {
      return false;
    }

    var candidateNormalized = candidate.ToUpperInvariant();

    // ---- THE LIMIT APPLIES TO WHAT IS STORED, NOT ONLY TO WHAT WAS TYPED.
    //
    // Both the display value and the normalized value go into `nvarchar(maximumLength)` columns, so a value
    // that fitted before normalization and not after would pass validation and then fail to persist.
    //
    // **This check is defensive, and no test asserts it fires**, because on .NET it cannot: `ToUpperInvariant`
    // uses simple 1:1 case mapping and never changes a string's length — U+00DF (ß) and the ligatures are
    // returned unchanged rather than expanded. The check stays because the property it protects is a column
    // width, the cost is one comparison, and a future runtime or a change to the normalization rule could
    // make it reachable. It is documented as unreachable rather than left to imply a case that occurs.
    //
    // `DepartmentCode` and `EmployeeNumber` carry the same guard and used to carry the same incorrect claim.
    // All three were corrected in FP-008 Phase 2: the first by ruling, the other two by the sweep that ruling
    // widened into once the same copied falsehood turned up a third time.
    if (candidateNormalized.Length > maximumLength)
    {
      return false;
    }

    trimmed = candidate;
    normalized = candidateNormalized;
    return true;
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
