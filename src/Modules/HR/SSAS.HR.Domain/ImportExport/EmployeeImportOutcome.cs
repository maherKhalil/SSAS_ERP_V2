namespace SSAS.HR.Domain.ImportExport;

// WHAT AN IMPORT RUN CONCLUDED (FP-009 lifecycle-model, DEC-DOC-0006).
//
// TERMINAL VALUES ONLY. There is deliberately no `InProgress`, and its absence is a consequence of
// `DEC-DOC-0007`'s synchronous execution rather than an oversight: a persisted `InProgress` row is a promise
// that something will come back and finish it, and when the process dies that promise becomes a permanent
// lie. Under synchronous execution the record is written when the outcome is already known, so every row in
// the table describes something that actually completed.
//
// If the caps ever rise and execution becomes asynchronous, `InProgress` arrives TOGETHER WITH the machinery
// that resolves it — a timeout, an owner and a reconciliation pass — never before it.
public enum EmployeeImportOutcome
{
  // The file was checked and NOTHING WAS WRITTEN. Reachable only through the dry-run route (`FR-DOC-0101`).
  Validated,

  // Employees were created. Under `OD-DOC-003` this means EVERY row: an applied run's accepted count always
  // equals its row count, which the factory makes structurally true rather than conventionally expected.
  Applied,

  // The submission was rejected as a whole — a bad header, a cap exceeded, or ANY row invalid, which under
  // `OD-DOC-003` are the same thing. THE IMPORT KEY IS CONSUMED ANYWAY: a key that a failed run released
  // would let the run it was meant to make unrepeatable be replayed under it.
  Refused
}
