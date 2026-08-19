namespace SSAS.HR.Domain.Employees;

// THE BOUNDED LIFECYCLE REASON VOCABULARY (FP-006 domain-model, DEC-EMP-0024 precedent).
//
// Bounded rather than free text because this is the only reason value carried in a domain event: a code
// cannot accidentally contain a name, a note, or anything else that should not leave the aggregate.
public enum EmployeeStatusChangeReason
{
  // Recorded only at creation, and invalid on every later transition.
  Created,

  Administrative,
  Operational,
  Compliance,

  // Employment-specific termination reasons.
  Resignation,
  Dismissal,
  EndOfContract
}
