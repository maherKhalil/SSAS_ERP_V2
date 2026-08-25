namespace SSAS.Payroll.Infrastructure.Persistence;

// ================================================================================================
// THE THIRD MODULE — AND THE PROMOTION IT TRIGGERED HAS NOW HAPPENED.
// ================================================================================================
//
// `GlPersistenceConstants` named "a third module" as the condition under which its copies stop being the
// right answer. Payroll was that third module, the trigger was recognised rather than rediscovered, and the
// promotion was RULED and performed on 2026-08-24 under `ADR-027` decision 4.
//
// So the values below are ALIASES of `TenantPersistenceConventions`. The local names remain because Payroll's
// configurations read better citing their own module's constants, but there is now exactly one source of
// truth for what "tenant" and the ordinal collation are. **Do not re-inline a literal here** — that would
// re-create the drift the promotion just removed.

internal static class PayrollPersistenceConstants
{
  // Tenant business data lives in ONE schema, in ONE context, on ONE migration stream (`ADR-017`).
  public const string TenantSchema = SSAS.BuildingBlocks.Infrastructure.Persistence.TenantPersistenceConventions.TenantSchema;

  // Binary collation, so comparison on the normalized shadow columns is ordinal and the unique index is
  // authoritative about what counts as the same code.
  public const string OrdinalCollation = SSAS.BuildingBlocks.Infrastructure.Persistence.TenantPersistenceConventions.OrdinalCollation;

  // Matches the platform's actor column width everywhere else in the tenant model.
  public const int ActorMaximumLength = SSAS.BuildingBlocks.Infrastructure.Persistence.TenantPersistenceConventions.ActorMaximumLength;

  // ---- EVERY MONETARY COLUMN IN THIS MODULE (DEC-PAY-0004, ADR-027 decision 1).
  //
  // `decimal(19,4)`. Note the relationship to `OD-PAY-0008`, which is easy to conflate: this is the STORAGE
  // precision. What a person is actually paid is rounded to two decimal places by `PayrollCalculator`
  // before it ever reaches a column, so the stored value already has at most two non-zero decimals. The
  // extra scale exists so no arithmetic performed on the way in can lose anything silently.
  public const int MoneyPrecision = SSAS.BuildingBlocks.Infrastructure.Persistence.TenantPersistenceConventions.MoneyPrecision;
  public const int MoneyScale = SSAS.BuildingBlocks.Infrastructure.Persistence.TenantPersistenceConventions.MoneyScale;

  // A payroll period's display name — "January 2026", "2026-01". Short and human-facing.
  public const int PeriodNameMaximumLength = 128;

  // The grade-band observation recorded with a compensation record (`OD-PAY-0004`). Prose for a human, not a
  // structured value: it explains what was observed at the moment the amount was set.
  public const int ObservationMaximumLength = 512;
}
