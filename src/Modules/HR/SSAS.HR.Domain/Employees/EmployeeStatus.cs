namespace SSAS.HR.Domain.Employees;

// THE V1 EMPLOYMENT STATES (FP-006 lifecycle-model, DEC-EMP-0014).
//
// Exactly three, and no rehire: `Terminated` is terminal in V1 because no source requirement establishes a
// rehire operation, and inventing one would fix a shape later requirements may contradict.
public enum EmployeeStatus
{
  // The state of every created Employee. An employee is hired INTO employment, so existence and
  // availability coincide and no separate activation step is meaningful. This deliberately differs from
  // Company, which is created Inactive because it may exist before its configuration prerequisites.
  Active,

  // Currently employed but temporarily not in service — unpaid leave, suspension. The employment
  // relationship persists: company, branch, number and history are all retained, and it is fully reversible.
  Inactive,

  // Employment has ended. Retained for history and reporting, and unable to transition again.
  Terminated
}
