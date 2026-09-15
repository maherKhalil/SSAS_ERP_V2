using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Application.Leave;

// ==================================================================================================
// SERIALISES ONE EMPLOYEE'S LEAVE SUBMISSIONS (T-151).
// ==================================================================================================
//
// **`SubmitLeaveRequestCommandHandler` checks for an overlapping request and then inserts.** Between those
// two steps it held nothing: no transaction, READ COMMITTED, and a shared lock released at statement end.
// **Two submissions seconds apart both read, both find no overlap, and both commit** — a double-clicked
// button is sufficient, and the result is double-counted unpaid absence on a payslip.
//
// ---- WHY A LOCK AND NOT A CONSTRAINT.
//
// T-150 added a unique index for IDENTICAL ranges, which catches the double-click and nothing else. **A
// unique index constrains equality on a key; overlap is a range predicate across rows and no index can
// express it** (`DEC-L-084`). Two submissions for 7th–11th and 9th–15th still both commit without this.
//
// ---- ⚠ AND WHY GL'S REFUSAL OF A LOCK DOES NOT DECIDE THIS ONE.
//
// `CalendarCommandHandlers.cs:73` declines a lock for fiscal-year overlap: *"the exposure is small (defining
// a fiscal year is rare and deliberate) and the alternative is a lock held across a human-scale operation."*
//
// **That describes a COMPANY-WIDE lock held while somebody defines a year. This is neither.** The resource
// is named per EMPLOYEE, so two employees submitting at the same instant never contend at all, and it is
// held for one query and one insert inside a single transaction. **The only serialised case is one employee
// submitting twice concurrently, which is exactly the case worth serialising.**
//
// **And leave is self-service** — an ordinary user, whenever they like — where a fiscal year is an
// accountant once a year. The frequency argument GL relies on does not transfer.
public interface ILeaveSubmissionLock
{
  // Transaction-owned: released by COMMIT or ROLLBACK, so there is no path where the write commits and the
  // lock outlives it. **It REFUSES if no transaction is open** rather than granting something ineffective —
  // the precondition enforcing itself, as `SqlServerDepartmentHierarchyLock` does.
  Task<Result> AcquireAsync(Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default);
}
