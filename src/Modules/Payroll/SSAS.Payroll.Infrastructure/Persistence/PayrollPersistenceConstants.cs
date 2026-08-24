namespace SSAS.Payroll.Infrastructure.Persistence;

// ================================================================================================
// THE THIRD MODULE. THE PROMOTION TRIGGER GL RECORDED HAS NOW FIRED — AND IS FLAGGED, NOT TAKEN.
// ================================================================================================
//
// `GlPersistenceConstants` holds its own copies of the schema and collation names, and its comment named the
// exact condition under which that stops being the right answer:
//
//   > If a THIRD module needs them, that is the moment the promotion earns its review — three call sites is
//   > where drift starts to cost something. Recorded here so that moment is recognised rather than
//   > rediscovered.
//
// **Payroll is that third module.** HR, GL and now Payroll each hold the same two string literals. The
// condition was written down precisely so this file would not quietly become the third copy while nobody
// noticed, and it has not: **the trigger is recognised here and raised to the architect.**
//
// It is raised rather than acted on because `ADR-027` decision 4 is explicit that promotion into
// `SSAS.BuildingBlocks` "is a deliberate, reviewed change to shared foundations, not a side effect of a
// feature package needing a type". A build prompt is exactly such a side effect. Promoting unilaterally
// here would do the right thing by the wrong route, and would put Payroll in the business of editing shared
// foundations on its way past — the thing the ADR names.
//
// So: the copies stand for now, this comment is the flag, and the as-built records it for the review.
internal static class PayrollPersistenceConstants
{
  // Tenant business data lives in ONE schema, in ONE context, on ONE migration stream (`ADR-017`).
  public const string TenantSchema = "tenant";

  // Binary collation, so comparison on the normalized shadow columns is ordinal and the unique index is
  // authoritative about what counts as the same code.
  public const string OrdinalCollation = "Latin1_General_100_BIN2";

  // Matches the platform's actor column width everywhere else in the tenant model.
  public const int ActorMaximumLength = 256;

  // ---- EVERY MONETARY COLUMN IN THIS MODULE (DEC-PAY-0004, ADR-027 decision 1).
  //
  // `decimal(19,4)`. Note the relationship to `OD-PAY-0008`, which is easy to conflate: this is the STORAGE
  // precision. What a person is actually paid is rounded to two decimal places by `PayrollCalculator`
  // before it ever reaches a column, so the stored value already has at most two non-zero decimals. The
  // extra scale exists so no arithmetic performed on the way in can lose anything silently.
  public const int MoneyPrecision = 19;
  public const int MoneyScale = 4;

  // A payroll period's display name — "January 2026", "2026-01". Short and human-facing.
  public const int PeriodNameMaximumLength = 128;

  // The grade-band observation recorded with a compensation record (`OD-PAY-0004`). Prose for a human, not a
  // structured value: it explains what was observed at the moment the amount was set.
  public const int ObservationMaximumLength = 512;
}
