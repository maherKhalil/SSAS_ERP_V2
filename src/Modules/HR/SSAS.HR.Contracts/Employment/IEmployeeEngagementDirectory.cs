namespace SSAS.HR.Contracts.Employment;

// ================================================================================================
// HOW AN EMPLOYEE IS ENGAGED, ASKED FOR BY NAME (T-153).
// ================================================================================================
//
// ---- WHY THIS IS NOT A FIFTH FIELD ON `EmploymentRecord`.
//
// `DEC-PAY-0017` pins the roster projection to four fields and says the list never widens. That decision
// gives its own reason, and the reason is not tidiness: a widened projection is read by every future
// Payroll feature **with no call site changing for anyone to review**.
//
// A purpose-named method is the opposite. Every consumer that wants an employment type must name this
// interface in its constructor, and that dependency stays visible at each new call site forever. **The
// widening is refused; the fact is still available.**
//
// ---- ⚠ AND `DEC-POS-0023` IS NOT ENGAGED HERE. THE ARGUMENT MATTERS BECAUSE THE GUARD IS REAL.
//
// `DEC-POS-0023` bars a compensation field on `Employee`, and it has been invoked correctly before — so a
// reader who finds this crossing without the argument will assume it was missed.
//
// **The test is whether knowing the value tells you what someone is PAID. Employment type does not — it
// tells you HOW they are engaged.** Full-time, part-time and contract are facts about the shape of the
// engagement, and two full-time employees may be paid anything at all. HR has always owned status, dates,
// branch and department on exactly that basis.
//
// **The value is a discriminator on the PAIRING RULE, not an input to any amount.** Payroll reads it to
// decide whether a compensation record may exist at all, and never to compute one.
public interface IEmployeeEngagementDirectory
{
  // The employee's employment type, or null when no such employee exists in the current tenant.
  //
  // **Null is an ordinary answer, not a fault** — the same reading `IEmployeePlacementDirectory` gives it,
  // for the same reason (`ADR-030` Decision 4 makes a cross-database foreign key impossible).
  //
  // ⚠ **A null here must never be read as "not a contract employee".** It says the employee could not be
  // found, which is a different fact from any employment type, and a caller that collapses the two turns a
  // missing row into a silent grant.
  Task<EmploymentType?> GetEmploymentTypeAsync(
    Guid employeeId, CancellationToken cancellationToken = default);
}
