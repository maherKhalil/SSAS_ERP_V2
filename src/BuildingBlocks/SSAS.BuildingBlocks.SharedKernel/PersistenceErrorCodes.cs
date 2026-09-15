namespace SSAS.BuildingBlocks.SharedKernel;

// ==================================================================================================
// THE PERSISTENCE ERROR CODES A MODULE MAY HAVE TO RECOGNISE (T-166).
// ==================================================================================================
//
// ---- WHY THIS EXISTS, AND WHY IT IS STRINGS AND NOTHING ELSE.
//
// The unit of work translates a failed save into an `Error` declared in `SSAS.Platform.Domain`. **A module
// under `src/Modules` may not reference that assembly** (`ADR-012`), so a module handler that needs to
// recognise one of these codes has only the string.
//
// A raw literal in a handler is **a typo away from silently never matching** — the condition it guards
// would simply stop being recognised, with no compiler error and no failing test. These constants give the
// one safety a string comparison can have.
//
// ---- ⚠ THIS IS NOT A SECOND ERROR CATALOGUE, AND MUST NOT BECOME ONE.
//
// **Constants only. No `Error` instances, no messages, no behaviour.** `IdentityAccessErrors` remains the
// single place these errors are DECLARED; this is the single place their codes are QUOTED.
//
// **`DEC-L-080`: one place states, the rest cite** — and a copy that cites is safe only while something
// checks the copy. `PersistenceErrorCodeParityTests` is that check, and it is the condition on which this
// file exists rather than a nicety beside it.
public static class PersistenceErrorCodes
{
  public const string UniqueConstraint = "Persistence.UniqueConstraint";

  public const string ConcurrencyConflict = "Persistence.ConcurrencyConflict";

  public const string WriteFailure = "Persistence.WriteFailure";
}
