using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Application.Employees;

// The Employee aggregate's own repository (ADR-010).
//
// AGGREGATE-SPECIFIC, WITH NO GENERIC SURFACE: no deferred query type crosses this boundary, there is no
// generic repository, and there is deliberately NO DELETE METHOD — physical Employee deletion is prohibited
// and the absence of a method is the first of the two protections (the persistence guard is the second).
//
// Uniqueness is TESTED here and ENFORCED by the database. The per-company unique indexes are authoritative
// under concurrent creation; these checks exist so the common case returns a named conflict rather than a
// raw persistence failure, not because they are the rule.
public interface IEmployeeRepository
{
  // Within the trusted tenant and the caller's authorized company and branch scope. Returns null rather
  // than an error so the caller can decide whether "not found" is a refusal or an ordinary absence.
  Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken = default);

  Task<bool> EmployeeNumberExistsAsync(
    Guid companyId, string normalizedEmployeeNumber, CancellationToken cancellationToken = default);

  Task<bool> NationalIdExistsAsync(
    Guid companyId, string normalizedNationalId, CancellationToken cancellationToken = default);

  Task AddAsync(Employee employee, CancellationToken cancellationToken = default);

  // The appended branch-assignment record. Separate from AddAsync because a transfer appends history to an
  // Employee that already exists, and the history is append-only: there is no update and no remove.
  Task AppendBranchAssignmentAsync(
    EmployeeBranchAssignment assignment, CancellationToken cancellationToken = default);
}
