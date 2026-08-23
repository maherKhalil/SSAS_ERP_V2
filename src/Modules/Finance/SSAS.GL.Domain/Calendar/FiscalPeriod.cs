using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Calendar;

public enum FiscalPeriodStatus
{
  Open = 0,
  Closed = 1
}

// A PERIOD WITHIN A FISCAL YEAR (REQ-GL-0009, REQ-GL-0010, BR-GL-0003).
//
// Owned by its `FiscalYear` and has no independent life: it is created only through the year that defines
// it, and the contiguity invariant belongs to that year rather than to any period alone. A period cannot
// check whether it leaves a gap; only the set can.
//
// ---- CLOSE AND REOPEN ARE BOTH EXPLICIT OPERATIONS.
//
// `BR-GL-0003` gives the closed state exactly one stated consequence — posting is prohibited — and says
// nothing about reopening. `lifecycle-model.md` recorded that silence rather than resolving it, and the
// build ruling settled it: both transitions exist and both are explicit, so reopening is an action someone
// took and can be audited, never a side effect of something else.
//
// Closing is a **company-scoped write**: the owning `FiscalYear` is `ICompanyOwnedEntity` (`OD-GL-0004`), so
// `AuthorizeCurrentCompanyAsync` runs at the write boundary before any of this is persisted.
public sealed class FiscalPeriod : Entity<Guid>, ITenantOwnedEntity
{
  internal FiscalPeriod(Guid id, Guid fiscalYearId, string name, DateTimeOffset startUtc, DateTimeOffset endUtc)
    : base(id)
  {
    FiscalYearId = fiscalYearId;
    Name = name;
    StartUtc = startUtc;
    EndUtc = endUtc;
    Status = FiscalPeriodStatus.Open;
  }

  // EF materialization only.
  private FiscalPeriod(Guid id)
    : base(id)
  {
    Name = null!;
  }

  // ---- ITenantOwnedEntity IS NOT OPTIONAL ON AN OWNED CHILD, AND THE REASON IS THE CUTOVER.
  //
  // `TenantCutoverCopyPlan.Build` derives the E3 manifest by reflecting over `ITenantOwnedEntity`. A table
  // whose type does not implement it is absent from the manifest, and therefore **absent from Shared to
  // Dedicated cutover** — which fails SILENTLY, taking the rows with it. HR's owned children carry the
  // marker for exactly this reason (`DepartmentManager` is the precedent).
  //
  // The parent's ownership is not inherited by the child at the model level. Being owned is a domain fact;
  // being copied is a reflection fact, and only the interface expresses the second.
  public Guid TenantId { get; set; }

  public Guid FiscalYearId { get; private set; }

  public string Name { get; private set; }

  // Inclusive start, EXCLUSIVE end — the half-open convention, chosen so contiguity is expressible without
  // depending on the resolution of the underlying type. With an inclusive end, "contiguous" would mean
  // "the next period starts one tick later", and the size of a tick would become part of the business rule.
  public DateTimeOffset StartUtc { get; private set; }

  public DateTimeOffset EndUtc { get; private set; }

  public FiscalPeriodStatus Status { get; private set; }

  public byte[]? RowVersion { get; set; }

  public bool Covers(DateTimeOffset instant) => instant >= StartUtc && instant < EndUtc;

  public Result Close()
  {
    if (Status == FiscalPeriodStatus.Closed)
    {
      return Result.Failure(CalendarErrors.PeriodAlreadyClosed);
    }

    Status = FiscalPeriodStatus.Closed;
    return Result.Success();
  }

  public Result Reopen()
  {
    if (Status == FiscalPeriodStatus.Open)
    {
      return Result.Failure(CalendarErrors.PeriodAlreadyOpen);
    }

    Status = FiscalPeriodStatus.Open;
    return Result.Success();
  }

  // ---- PERIODS MAY BE CLOSED OUT OF ORDER, AND THAT IS RECORDED RATHER THAN ENFORCED.
  //
  // `lifecycle-model.md` raised "must periods close in order?" and observed that nothing says which. No rule
  // was created here: inventing an ordering constraint would be this package deciding a product question
  // nobody asked it. If the owner later requires in-order closing, this is the method that grows a guard.
  public Result EnsureOpenForPosting()
  {
    return Status == FiscalPeriodStatus.Open
      ? Result.Success()
      : Result.Failure(CalendarErrors.PeriodClosed);
  }
}
