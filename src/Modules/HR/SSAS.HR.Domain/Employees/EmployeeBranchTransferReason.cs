namespace SSAS.HR.Domain.Employees;

// WHY AN EMPLOYEE'S BRANCH ASSIGNMENT CHANGED (FP-006 domain-model, DEC-EMP-0024).
//
// Bounded for the same reason the lifecycle vocabulary is: it is the only reason value a domain event
// carries. The free-text ReasonText that accompanies it is persisted for the audit record alone and is
// never emitted, compared, or indexed.
public enum EmployeeBranchTransferReason
{
  // Recorded ONLY by the initial assignment written at creation, and invalid on a transfer. The pairing is
  // enforced both in the domain and by a check constraint, so the initial record and a transfer record can
  // never be confused for one another.
  InitialAssignment,

  Reorganisation,
  OperationalNeed,
  EmployeeRequest,

  // The expected code for an ADR-024 decision 12 recovery out of a deactivated branch.
  BranchClosure,

  // The expected code when a mistaken transfer is reversed by another transfer, since V1 has no
  // cancellation operation.
  Correction
}
