namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

// ================================================================================================
// THE ONE TRUTH ABOUT THE TENANT SCHEMA AND ITS COLLATION.
// ================================================================================================
//
// **PROMOTED 2026-08-24 under `ADR-027` decision 4. This file IS that review's outcome.**
//
// ---- THE TRIGGER, AND WHO WROTE IT.
//
// `GlPersistenceConstants` carried its own copies and named the exact condition that would end that:
//
//   > If a THIRD module needs them, that is the moment the promotion earns its review — three call sites is
//   > where drift starts to cost something. Recorded here so that moment is recognised rather than
//   > rediscovered.
//
// FP-012 made Payroll the third module. The trigger fired, was recognised rather than rediscovered, and was
// raised rather than taken unilaterally — because `ADR-027` decision 4 is explicit that promotion is "a
// deliberate, reviewed change to shared foundations, not a side effect of a feature package needing a type".
// The review happened; this is its record.
//
// ---- WHAT THE SWEEP ACTUALLY FOUND, WHICH WAS WORSE THAN THE TRIGGER ANTICIPATED.
//
// The trigger expected three copies. A grep for the declarations found **five**: HR (on
// `EmployeeConfiguration`), GL, Payroll, and two inside Platform. The three MODULE copies are the ones this
// promotion consolidates.
//
// **Platform's two are deliberately left alone**, and that is not an oversight. `PlatformPersistenceConstants`
// describes the PLATFORM database, which is a different database with its own migration stream; that its
// collation literal is currently the same string is a fact about SQL Server, not a shared decision. Folding
// it in here would assert a coupling between the two databases that nobody has ruled and that `ADR-014`
// deliberately keeps apart.
//
// ---- WHY THESE ARE SAFE TO SHARE WHEN A SCOPE TYPE WOULD NOT BE.
//
// These are facts about the DATABASE — a schema name and a collation name, unchanged since Sprint-00 and
// fixed by the storage engine rather than by any module's opinion. Sharing a fact costs nothing when it
// drifts, because it cannot drift; it is the same string or the model is broken.
//
// That is precisely NOT true of an authorization scope, which is why the sibling promotion in this same
// commit shares only the DATA SHAPE of a company set and leaves each module's unforgeable scope type where
// it is. See `AuthorizedCompanySet`.
public static class TenantPersistenceConventions
{
  // Tenant business data lives in ONE schema, in ONE context, on ONE migration stream (`ADR-017`).
  public const string TenantSchema = "tenant";

  // Binary collation, so comparison on normalized shadow columns is ordinal and a unique index is
  // authoritative about what counts as the same code.
  public const string OrdinalCollation = "Latin1_General_100_BIN2";

  // The actor column width used everywhere in the tenant model.
  public const int ActorMaximumLength = 256;

  // ---- MONEY (ADR-027 decision 1).
  //
  // `decimal(19,4)`. Adopted by `DEC-GL-0001` and `DEC-PAY-0004`; promoted here so a fourth module cannot
  // adopt it by observation, which `ADR-027` names as the outcome it exists to prevent.
  public const int MoneyPrecision = 19;
  public const int MoneyScale = 4;
}
