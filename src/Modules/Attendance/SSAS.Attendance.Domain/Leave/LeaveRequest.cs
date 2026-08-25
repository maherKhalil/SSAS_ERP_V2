using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Leave;

// ================================================================================================
// THE LEAVE REQUEST (REQ-ATT-0012 to REQ-ATT-0016; OD-ATT-0007).
// ================================================================================================
//
// The Glossary's only mention of leave is as an example of a **Workflow**, beside Employee Hiring, Journal
// Approval and Purchase Approval. This is that example made real — and it is worth saying plainly that the
// Glossary reference is the whole of the authored authority for leave existing at all.
//
// ---- WHO SUBMITS IT, AND WHY THAT IS NOT THE EMPLOYEE.
//
// `OD-ATT-0013` deferred self-service because **no identity-to-employee mapping exists** — verified, not
// assumed: `Employee` carries no user identifier, and neither HR's domain nor its contracts expose one.
// `OD-PAY-0016` deferred payroll self-service for exactly this reason and `PayrollPermissionNames` records
// the refusal in code.
//
// So `EmployeeId` is supplied by an administrator holding `Attendance.Leave.Manage`, and it is MANDATORY
// rather than inferred. This is the third consecutive feature to meet the same missing input, and it is now
// a recorded future package rather than a coincidence.
//
// COMPANY-OWNED, NOT branch-owned: approval runs through the DEPARTMENT chain, not the branch tree, so a
// branch predicate here would filter on a dimension the workflow does not use. Asserted negatively per
// `DEC-ATT-0014`.
public enum LeaveRequestStatus
{
  Submitted = 0,
  Approved = 1,
  Rejected = 2,
  Cancelled = 3
}

public sealed class LeaveRequest
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  public const int ActorMaximumLength = 256;
  public const int DecisionNoteMaximumLength = 1000;

  private LeaveRequest(
    Guid id, Guid companyId, Guid employeeId, Guid leaveTypeId,
    DateOnly startDate, DateOnly endDate, decimal workingDaysConsumed)
    : base(id)
  {
    CompanyId = companyId;
    EmployeeId = employeeId;
    LeaveTypeId = leaveTypeId;
    StartDate = startDate;
    EndDate = endDate;
    WorkingDaysConsumed = workingDaysConsumed;
    Status = LeaveRequestStatus.Submitted;
  }

  // EF materialization only.
  private LeaveRequest(Guid id)
    : base(id)
  {
  }

  public Guid LeaveRequestId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid EmployeeId { get; private set; }

  public Guid LeaveTypeId { get; private set; }

  public DateOnly StartDate { get; private set; }

  public DateOnly EndDate { get; private set; }

  // ================================================================================================
  // STORED, NOT DERIVED ON READ (BR-ATT-0003, AC-ATT-0019).
  // ================================================================================================
  //
  // The figure is computed from the working calendar at SUBMISSION and frozen there.
  //
  // **Because the calendar is maintainable.** A holiday added next year would otherwise silently change how
  // many days a request taken last year consumed — and therefore a balance that was already settled, and
  // therefore what somebody was paid. Storing the number freezes the fact at the moment the decision was
  // made, which is the same instinct that makes `PayrollRunLine` append-only.
  //
  // It is a `decimal` rather than an `int` so half-days remain expressible without a schema change, even
  // though nothing in v1 produces one.
  public decimal WorkingDaysConsumed { get; private set; }

  public LeaveRequestStatus Status { get; private set; }

  public string? DecidedBy { get; private set; }

  public DateTimeOffset? DecidedUtc { get; private set; }

  public string? DecisionNote { get; private set; }

  // The employee whose approval authority was used. Recorded because `OD-ATT-0007`'s chain can escalate past
  // an unmanaged or self-referential department, and "who actually approved this" is then not derivable from
  // the requester's department alone — a reader six months later would have to re-walk a department tree
  // that has since changed.
  public Guid? ApproverEmployeeId { get; private set; }

  public DateTimeOffset CreatedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? ModifiedBy { get; set; }

  public byte[] RowVersion { get; private set; } = [];

  public bool IsDecided => Status is LeaveRequestStatus.Approved or LeaveRequestStatus.Rejected;

  public static Result<LeaveRequest> Submit(
    Guid companyId, Guid employeeId, Guid leaveTypeId,
    DateOnly startDate, DateOnly endDate, decimal workingDaysConsumed)
  {
    if (companyId == Guid.Empty)
    {
      return Result.Failure<LeaveRequest>(LeaveErrors.CompanyRequired);
    }

    if (employeeId == Guid.Empty || leaveTypeId == Guid.Empty)
    {
      return Result.Failure<LeaveRequest>(LeaveErrors.RequestSubjectRequired);
    }

    if (endDate < startDate)
    {
      return Result.Failure<LeaveRequest>(LeaveErrors.InvalidRequestRange);
    }

    // A range containing no working day at all — a request for a weekend — consumes nothing and would
    // decrement nothing on approval. Refused at submission so the requester learns immediately rather than
    // holding an approved request that had no effect.
    if (workingDaysConsumed <= 0m)
    {
      return Result.Failure<LeaveRequest>(LeaveErrors.RequestContainsNoWorkingDay);
    }

    return Result.Success(new LeaveRequest(
      Guid.NewGuid(), companyId, employeeId, leaveTypeId, startDate, endDate, workingDaysConsumed));
  }

  // ---- THE SELF-APPROVAL BAR LIVES HERE, NOT AT THE ENDPOINT (AC-ATT-0020, BR-ATT-0007).
  //
  // A permission check answers "may this person approve requests". It **cannot** answer "may this person
  // approve THIS request", because only the aggregate knows both the requester and the approver.
  //
  // `OD-PAY-0009` drew the same line when it barred the calculator from approving its own run: the actor who
  // produces a figure must not be the actor who blesses it. Under the department-manager model the bar is
  // not hypothetical — a department manager submitting leave would otherwise resolve to themselves.
  public Result Approve(Guid approverEmployeeId, string? decidedBy, DateTimeOffset decidedUtc, string? note)
  {
    var guard = GuardDecision(decidedBy, note);
    if (guard.IsFailure)
    {
      return guard;
    }

    if (approverEmployeeId == Guid.Empty)
    {
      return Result.Failure(LeaveErrors.ApproverRequired);
    }

    if (approverEmployeeId == EmployeeId)
    {
      return Result.Failure(LeaveErrors.SelfApprovalBarred);
    }

    Status = LeaveRequestStatus.Approved;
    ApproverEmployeeId = approverEmployeeId;
    DecidedBy = decidedBy;
    DecidedUtc = decidedUtc;
    DecisionNote = Trim(note);
    return Result.Success();
  }

  public Result Reject(Guid approverEmployeeId, string? decidedBy, DateTimeOffset decidedUtc, string? note)
  {
    var guard = GuardDecision(decidedBy, note);
    if (guard.IsFailure)
    {
      return guard;
    }

    if (approverEmployeeId == EmployeeId)
    {
      return Result.Failure(LeaveErrors.SelfApprovalBarred);
    }

    Status = LeaveRequestStatus.Rejected;
    ApproverEmployeeId = approverEmployeeId;
    DecidedBy = decidedBy;
    DecidedUtc = decidedUtc;
    DecisionNote = Trim(note);
    return Result.Success();
  }


  // ================================================================================================
  // THE ROOT-FALLBACK DECISION (OD-ATT-0007) — DELIBERATELY A DIFFERENT METHOD.
  // ================================================================================================
  //
  // Reached when no department manager above the requester can decide: every department unmanaged, every
  // manager the requester, or the chain exhausted at the top. `Attendance.Leave.ApproveAtRoot` is what
  // permits it, and `LeaveApprovalRouter` is what establishes that the ordinary path was genuinely absent.
  //
  // **`ApproverEmployeeId` stays NULL here, and that is a statement rather than a gap.** The holder is
  // authenticated as a USER, and no identity-to-employee mapping exists (`OD-ATT-0013`, verified). There is
  // no employee to record, so nothing is recorded — as opposed to recording `Guid.Empty` and letting a
  // reader mistake it for an employee.
  //
  // ---- AND THE HONEST LIMITATION, STATED RATHER THAN PAPERED OVER.
  //
  // **The self-approval bar cannot be enforced on this path.** `Approve` compares an approver EMPLOYEE to
  // the requester; here the actor is a user whose employee identity is unknowable. If the root-fallback
  // holder happens to be the requesting employee, nothing here can tell.
  //
  // That is a real consequence of the missing mapping and not a design choice. It is bounded by making
  // `ApproveAtRoot` a separate, strictly wider grant that an administrator gives deliberately, and it is
  // recorded in `DecidedBy` plus the null approver so the path is auditable after the fact. **When the
  // identity-to-employee mapping is built, this is the first thing that should be tightened.**
  public Result ApproveAtRoot(string? decidedBy, DateTimeOffset decidedUtc, string? note)
  {
    var guard = GuardDecision(decidedBy, note);
    if (guard.IsFailure)
    {
      return guard;
    }

    Status = LeaveRequestStatus.Approved;
    ApproverEmployeeId = null;
    DecidedBy = decidedBy;
    DecidedUtc = decidedUtc;
    DecisionNote = Trim(note);
    return Result.Success();
  }

  public Result RejectAtRoot(string? decidedBy, DateTimeOffset decidedUtc, string? note)
  {
    var guard = GuardDecision(decidedBy, note);
    if (guard.IsFailure)
    {
      return guard;
    }

    Status = LeaveRequestStatus.Rejected;
    ApproverEmployeeId = null;
    DecidedBy = decidedBy;
    DecidedUtc = decidedUtc;
    DecisionNote = Trim(note);
    return Result.Success();
  }
  // ---- CANCELLATION, AND WHY THE DATES MATTER (REQ-ATT-0016, AC-ATT-0042).
  //
  // Before the dates pass, cancelling is ordinary: the leave has not happened.
  //
  // **After they pass it is different in kind**, because by then the absence is a fact that occurred.
  // Cancelling it is a CORRECTION, and corrections route through `OD-ATT-0012`'s adjustment path rather
  // than through this method — which is why this refuses instead of quietly reversing a balance for days
  // somebody actually took off.
  public Result Cancel(DateOnly today)
  {
    if (Status == LeaveRequestStatus.Cancelled)
    {
      return Result.Failure(LeaveErrors.RequestAlreadyCancelled);
    }

    if (Status == LeaveRequestStatus.Rejected)
    {
      return Result.Failure(LeaveErrors.RejectedRequestNotCancellable);
    }

    if (StartDate <= today)
    {
      return Result.Failure(LeaveErrors.RequestAlreadyStarted);
    }

    Status = LeaveRequestStatus.Cancelled;
    return Result.Success();
  }

  private Result GuardDecision(string? decidedBy, string? note)
  {
    if (Status != LeaveRequestStatus.Submitted)
    {
      return Result.Failure(LeaveErrors.RequestAlreadyDecided);
    }

    if (string.IsNullOrWhiteSpace(decidedBy) || decidedBy.Length > ActorMaximumLength)
    {
      return Result.Failure(LeaveErrors.InvalidActor);
    }

    if (note is not null && (note.Length > DecisionNoteMaximumLength || note.Any(char.IsControl)))
    {
      return Result.Failure(LeaveErrors.InvalidDecisionNote);
    }

    return Result.Success();
  }

  private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
