using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Application.ImportExport;

// THE IMPORT'S OWN ERRORS (FP-009, DEC-DOC-0002, DEC-DOC-0003).
//
// ================================================================================================
// DELIBERATELY FEW, BECAUSE MOST ROW FAILURES ALREADY HAVE A NAME.
// ================================================================================================
//
// A row failing uniqueness fails it for exactly the reason a single create does, so it reports
// `EmployeeErrors.NumberConflict` — the same error, from the same namespace, mapped by the same API mapper
// to the same wire code. Inventing `employee_import.number_conflict` beside it would give one condition two
// names and let the two drift, and a client branching on `code` would have to know which surface it came
// from to know what it meant.
//
// What is here is only what the FILE FORMAT can get wrong, which single-record creation has no equivalent
// of: a header, a row's shape, a cap. Nothing here names another company's data or confirms the existence of
// any identifier, for the reason `EmployeeErrors` states — and under `OD-DOC-004` that matters more here than
// anywhere, because a file can probe one rejection message at a time.
public static class EmployeeImportErrors
{
  // ---- FILE-LEVEL. Every one of these is decided before the first data row is validated.

  public static readonly Error HeaderMissing =
    new("EmployeeImport.HeaderMissing", "The submitted file has no header row.");

  public static readonly Error HeaderColumnUnknown =
    new("EmployeeImport.HeaderColumnUnknown", "The header names a column this contract does not define.");

  public static readonly Error HeaderColumnMissing =
    new("EmployeeImport.HeaderColumnMissing", "The header omits a required column.");

  public static readonly Error HeaderColumnDuplicated =
    new("EmployeeImport.HeaderColumnDuplicated", "The header names the same column more than once.");

  public static readonly Error RowLimitExceeded =
    new("EmployeeImport.RowLimitExceeded", "The submitted file has more rows than an import may carry.");

  public static readonly Error ByteLimitExceeded =
    new("EmployeeImport.ByteLimitExceeded", "The submitted file is larger than an import may carry.");

  // ---- ROW-LEVEL, AND ONLY WHERE NO EXISTING ERROR SAYS IT.

  public static readonly Error RowShapeInvalid =
    new("EmployeeImport.RowShapeInvalid", "The row does not have one value for each declared column.");

  public static readonly Error EmploymentDateInvalid =
    new("EmployeeImport.EmploymentDateInvalid", "The employment date is not a valid ISO-8601 date.");

  // ---- THE MESSAGE NAMES THE REMEDY, WHICH IS THE POINT OF THE COLUMN BEING RECOGNIZED AT ALL.
  //
  // An operator re-importing a `status=Terminated` export needs to know that the file is legal and the VALUE
  // is not, and what to do about it. A header rejection could not have said that, and silent acceptance
  // would have created Active employees out of terminated ones without saying anything (`OD-DOC-010`).
  public static readonly Error StatusNotCreatable = new(
    "EmployeeImport.StatusNotCreatable",
    "An import creates only Active employees; remove the status value or the row.");

  // ---- THE DUPLICATE *WITHIN THE FILE*, WHICH THE DATABASE CANNOT SEE.
  //
  // Two rows of one file claiming the same employee number would pass every per-row check — neither number
  // exists yet — and then fail on the unique index partway through, in a save the all-or-nothing rule would
  // roll back with nothing to tell the operator about WHY. Detected here so the report names both rows.
  public static readonly Error DuplicateWithinFile =
    new("EmployeeImport.DuplicateWithinFile", "Another row in the same file claims this value.");
}
