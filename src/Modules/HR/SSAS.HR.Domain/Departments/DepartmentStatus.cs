namespace SSAS.HR.Domain.Departments;

// THE TWO DEPARTMENT STATES (ADR-026, FP-007 lifecycle-model).
//
// TWO, NOT THREE. Employee has a third — `Terminated` — which is genuinely terminal. A department's off
// state is not: organizations close a department and reopen it, so `Inactive` is reversible here even
// though the identically-named Employee state sits beside a terminal one. They are not modelled alike
// merely because they share a word.
//
// THERE IS NO `Deleted`. A department is never physically removed (`BR-PLT-0003`), so a lifecycle state
// standing for deletion would describe something that cannot happen.
public enum DepartmentStatus
{
  // The state of every created department. A department created inactive would be one nobody can use and
  // nobody asked for.
  Active,

  // Retained, readable, and still the department of every employee already assigned to it — but unable to
  // receive new ones (`BR-HR-0009`). Reversible.
  Inactive
}
