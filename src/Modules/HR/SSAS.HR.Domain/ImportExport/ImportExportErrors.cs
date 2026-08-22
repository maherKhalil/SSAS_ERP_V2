using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.ImportExport;

// THE RUN RECORDS' OWN DOMAIN ERRORS (FP-009 Phase 1).
//
// Deliberately few. A run record is written when an outcome is already known, so almost everything that can
// go wrong went wrong before this type was reached and is reported by the import pipeline's own row report
// (`DEC-DOC-0003`) rather than here. What remains is the small set of ways a CALLER OF THIS TYPE could
// construct a record that does not describe anything that happened.
//
// Nothing here names another company's data or the existence of any identifier, for the reason
// `EmployeeErrors` states.
public static class ImportExportErrors
{
  public static readonly Error InvalidImportKey =
    new("EmployeeImportRun.InvalidImportKey", "The import key is invalid.");

  public static readonly Error InvalidFileName =
    new("EmployeeImportRun.InvalidFileName", "The submitted file name is invalid.");

  public static readonly Error InvalidActor =
    new("EmployeeImportRun.InvalidActor", "A trusted execution actor is required.");

  public static readonly Error InvalidCounts =
    new("EmployeeImportRun.InvalidCounts", "The run counts do not describe a possible run.");

  public static readonly Error InvalidColumnSet =
    new("EmployeeExportRun.InvalidColumnSet", "The exported column set is invalid.");
}
