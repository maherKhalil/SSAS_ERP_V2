namespace SSAS.GL.Infrastructure.Persistence;

// GL'S OWN COPIES OF THE TWO PERSISTENCE CONSTANTS, AND WHY THEY ARE COPIES.
//
// `ADR-012` forbids one module referencing another's assembly, and it is compiler-enforced rather than a
// rule anyone can forget. So GL cannot reach for `EmployeeConfiguration.TenantSchema` or
// `EmployeeConfiguration.OrdinalCollation`, however identical the values are.
//
// This is duplication with its eyes open, and it is the RIGHT duplication. `ADR-027` decision 4 governs the
// alternative: a type two modules need is PROMOTED into `SSAS.BuildingBlocks`, never duplicated and never
// reached across — but promotion "is a deliberate, reviewed change to shared foundations, not a side effect
// of a feature package needing a type". A schema name and a collation name are two string literals that have
// not changed since Sprint-00 and are fixed by the database itself, not by either module's opinion. Raising
// a foundations change to share them would cost more review than it saves, and would put GL in the business
// of editing BuildingBlocks on its way past.
//
// If a third module needs them, that is the moment the promotion earns its review — three call sites is
// where drift starts to cost something. Recorded here so that moment is recognised rather than rediscovered.
internal static class GlPersistenceConstants
{
  // Tenant business data lives in ONE schema, in ONE context, on ONE migration stream (`ADR-017`).
  public const string TenantSchema = "tenant";

  // Binary collation, so comparison on the normalized shadow columns is ordinal and the unique index is
  // authoritative about what counts as the same code.
  public const string OrdinalCollation = "Latin1_General_100_BIN2";

  // Matches the platform's actor column width everywhere else in the tenant model.
  public const int ActorMaximumLength = 256;

  // ---- EVERY MONETARY COLUMN IN THIS MODULE (DEC-GL-0001, ADR-027 decision 1).
  //
  // `ADR-027` named General Ledger in its deferred obligations: "must either adopt decision 1 or amend this
  // ADR. Matching HR by observation, without a recorded decision, is the outcome this ADR exists to
  // prevent." `DEC-GL-0001` adopted it, and this is where the adoption becomes a column type.
  public const int MoneyPrecision = 19;
  public const int MoneyScale = 4;
}
