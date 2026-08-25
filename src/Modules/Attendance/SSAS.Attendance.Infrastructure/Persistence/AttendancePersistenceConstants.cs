namespace SSAS.Attendance.Infrastructure.Persistence;

// ================================================================================================
// THE FOURTH MODULE, AND IT ALIASES RATHER THAN COPIES.
// ================================================================================================
//
// `GlPersistenceConstants` named "a third module" as the condition under which its copies stop being the
// right answer; Payroll was that third module and the promotion into `TenantPersistenceConventions` was
// ruled and performed on 2026-08-24 under `ADR-027` decision 4.
//
// So these are ALIASES. The local names remain because Attendance's configurations read better citing their
// own module's constants, but there is exactly one source of truth. **Do not re-inline a literal here** —
// that would re-create the drift the promotion removed.
internal static class AttendancePersistenceConstants
{
  public const string TenantSchema = SSAS.BuildingBlocks.Infrastructure.Persistence.TenantPersistenceConventions.TenantSchema;

  public const string OrdinalCollation = SSAS.BuildingBlocks.Infrastructure.Persistence.TenantPersistenceConventions.OrdinalCollation;

  public const int ActorMaximumLength = SSAS.BuildingBlocks.Infrastructure.Persistence.TenantPersistenceConventions.ActorMaximumLength;

  // ================================================================================================
  // QUANTITY, AND WHY IT IS DELIBERATELY NOT THE MONEY TYPE (DEC-ATT-0004).
  // ================================================================================================
  //
  // `decimal(9,2)`. Money in this product is `decimal(19,4)` (`ADR-027` d1), and **no column in this module
  // uses it** — an integration test asserts exactly that, positively.
  //
  // The two being different shapes is the point rather than an accident of sizing. Attendance records HOW
  // MUCH; Payroll decides what it is worth. If a `decimal(19,4)` column ever appears in an Attendance table,
  // the module boundary has drifted and the schema is the first place it shows.
  //
  // Hours are not integers, so a quantity is still a decimal. Two places is ample for hours and days, and
  // nine digits of precision covers any quantity a period can contain.
  public const int QuantityPrecision = 9;
  public const int QuantityScale = 2;

  // A weekend pattern persisted as comma-separated day ordinals ("5,6"). See `WeekendPattern` for why this
  // is a column rather than a child table.
  public const int WeekendPatternMaximumLength = 13;
}
