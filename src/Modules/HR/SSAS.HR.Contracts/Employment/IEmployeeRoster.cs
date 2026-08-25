namespace SSAS.HR.Contracts.Employment;

// ================================================================================================
// THE HR SIDE OF THE PAYROLL BOUNDARY. SHAPED BY ITS CONSUMER, ON THE SAME TERMS AS THE LEDGER'S.
// ================================================================================================
//
// `SSAS.HR.Contracts` has existed and been empty since HR shipped. Payroll is its first consumer, and this
// is the first thing in it.
//
// ---- WHY THIS EXISTS AT ALL.
//
// A payroll run must know **who was employed, and between which dates** (`REQ-PAY-0008`, `OD-PAY-0010`).
// That is HR's fact, and `ADR-012` forbids Payroll referencing `SSAS.HR.Domain` or `SSAS.HR.Application` to
// reach it — the same rule that governs the ledger direction.
//
// `OD-PAY-0013` ruled the mechanism for the GL direction: **a synchronous contract, shaped by the
// consumer.** The FP-012 package recorded that the ruling "governs both directions of traffic, not only the
// GL side". This is that ruling applied consistently rather than a second mechanism invented for the second
// direction — two integration styles in one module would be the thing nobody could later explain.
//
// ---- WHAT IT DELIBERATELY DOES NOT EXPOSE, WHICH IS MOST OF AN EMPLOYEE.
//
// No name, no national identifier, no department, no position, no branch, no status reason. **Payroll needs
// four facts to decide who to pay and for how many days, and it gets four facts.**
//
// This is not minimalism for its own sake. A roster contract that returned `EmployeeDetail` would make every
// future Payroll feature able to read HR's personal data without anyone reviewing the widening — and the
// widening would be invisible, because the call site would not change. A contract shaped by its consumer's
// need is also a contract that cannot quietly become a data-sharing agreement.
//
// **And nothing flows the other way.** `DEC-PAY-0014`: Payroll never writes to HR. There is no method here
// that mutates anything, and adding one would silently reverse `DEC-POS-0023` by putting compensation into
// HR through a side door.
public sealed record EmploymentRecord(
  Guid EmployeeId,
  Guid CompanyId,

  // The date employment began. Payroll prorates from it for a mid-period joiner (`OD-PAY-0007`).
  DateTimeOffset EmploymentDateUtc,

  // Null while employed. When set, Payroll still INCLUDES the employee if they worked any day of the period
  // — `OD-PAY-0010` ruled that `BR-HR-0004` bars new obligations, not the settlement of obligations already
  // incurred, and final pay is a settlement.
  DateTimeOffset? TerminationDateUtc);

public interface IEmployeeRoster
{
  // Every employee of a company whose employment overlaps the window at all. HR applies no payroll rule
  // here: WHO IS INCLUDED IS PAYROLL'S DECISION (`PayrollPeriod.Includes`), and this returns the candidates
  // that decision is made over.
  //
  // The split matters. If HR filtered by "employed during the period", the `BR-HR-0004` reading would be
  // implemented in two modules and could drift — and the module that owns the ruling would not be the module
  // enforcing it.
  Task<IReadOnlyList<EmploymentRecord>> GetEmploymentAsync(
    Guid companyId,
    DateTimeOffset fromUtc,
    DateTimeOffset toUtc,
    CancellationToken cancellationToken = default);
}
