namespace SSAS.Platform.Domain.Enums;

// A GRANT RAISES ONE OF EXACTLY TWO THINGS (FP-014, `OD-SUB-0011`).
//
// A closed domain enum with a database `CHECK`, per `ADR-017`'s category D — not a lookup table. A row that
// is neither shape is a row the resolution function cannot interpret, which is why the two are closed here
// and constrained again in the schema.
public enum EntitlementGrantKind
{
  ModuleGrant = 0,
  LimitRaise = 1
}
