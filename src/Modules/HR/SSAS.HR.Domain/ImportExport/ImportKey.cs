using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.ImportExport;

// THE CALLER'S IDEMPOTENCY KEY FOR ONE IMPORT (DEC-DOC-0004).
//
// CLIENT-CHOSEN, UNIQUE PER COMPANY. It is not an identity the server issues and not a value the server ever
// interprets — it is an opaque token the caller picks so that replaying a submission can be recognised as the
// same submission rather than a second one.
//
// ---- WHAT IT PROTECTS AGAINST, AND WHAT IT DOES NOT.
//
// `OD-DOC-003`'s all-or-nothing ruling removed the partially-applied file, so this key is not reconciling
// which rows landed. The job that remains is the AMBIGUOUS TIMEOUT: the operator whose connection dropped and
// who cannot tell whether five thousand employees now exist. Replaying the key answers that exact question
// without importing anything a second time.
//
// The length, normalization and comparison rules are exactly the `EmployeeNumber` convention
// (`DEC-CMP-0006`, `DEC-CMP-0007`), so a reader who knows one knows this one — the only difference is the
// width, which `data-model.md` sets at 128.
public sealed class ImportKey : ValueObject
{
  public const int MaximumLength = 128;

  private ImportKey(string value, string normalizedValue)
  {
    Value = value;
    NormalizedValue = normalizedValue;
  }

  // The trimmed value with the caller's casing preserved, so a run report echoes back what was submitted.
  public string Value { get; }

  // Exactly Trim().ToUpperInvariant(); the column carrying it is binary-collated, so the uniqueness index
  // compares ordinally and two keys differing only in case are ONE key.
  public string NormalizedValue { get; }

  public static Result<ImportKey> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength || ContainsControlCharacter(trimmed))
    {
      return Result.Failure<ImportKey>(ImportExportErrors.InvalidImportKey);
    }

    // No Unicode NFC/NFD/NFKC/NFKD normalization, for the reason `EmployeeNumber` gives: two visually
    // identical values that differ in composition are different keys, which is what makes the
    // binary-collated index authoritative rather than advisory.
    var normalized = trimmed.ToUpperInvariant();

    // Defensive, and — as `EmployeeNumber` documents at length — unreachable on .NET, where
    // ToUpperInvariant uses simple 1:1 case mapping and never changes a string's length. It stays because
    // the property it protects is a COLUMN WIDTH: a value that fitted before normalization and not after
    // would pass validation and then fail to persist. Documented as unreachable rather than left implying
    // a case that occurs.
    if (normalized.Length > MaximumLength)
    {
      return Result.Failure<ImportKey>(ImportExportErrors.InvalidImportKey);
    }

    return Result.Success(new ImportKey(trimmed, normalized));
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
