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

  // The same, for the department log (FP-007 Phase 3). Append-only in the identical sense: no update, no
  // remove, and no method here that could express one.
  Task AppendDepartmentAssignmentAsync(
    Domain.Departments.EmployeeDepartmentAssignment assignment,
    CancellationToken cancellationToken = default);

  // ---- WHAT THE APPLICATION NEEDS TO KNOW ABOUT A DESTINATION DEPARTMENT, AND NOTHING MORE.
  //
  // Not the Department aggregate — this repository owns Employee, and handing back a second aggregate root
  // would invite a caller to mutate it inside an employee operation. Just the two facts the validation
  // rules in FP-007 Phase 3 §5 and §8 turn on, resolved within the trusted tenant and the named company so
  // a department in another company is reported ABSENT rather than as a refusal.
  Task<DepartmentAssignmentTarget?> FindAssignableDepartmentAsync(
    Guid companyId, Guid departmentId, CancellationToken cancellationToken = default);

  // ---- THE POSITION HISTORY AND THE DESTINATION POSITION (FP-008 Phase 3).
  //
  // Both mirror their department counterparts above exactly, for the reasons stated there: append-only with
  // no update or remove counterpart, and a destination reduced to the two facts the rules turn on rather
  // than handed back as a second aggregate root.
  //
  // They live on the EMPLOYEE repository, not on `IPositionRepository`, because both are used inside an
  // employee operation and the rows they touch hang off an employee. `IPositionRepository` answers
  // questions about positions; this answers what an employee write needs to know.
  Task AppendPositionAssignmentAsync(
    Domain.Positions.EmployeePositionAssignment assignment,
    CancellationToken cancellationToken = default);

  Task<PositionAssignmentTarget?> FindAssignablePositionAsync(
    Guid companyId, Guid positionId, CancellationToken cancellationToken = default);

  // ---- THE SAME TWO QUESTIONS, ASKED BY CODE INSTEAD OF BY IDENTIFIER (FP-009, OD-DOC-004).
  //
  // SIBLINGS of the two above, not a generalization of them. A single method taking a discriminator or an
  // "either identifier or code" parameter would hide WHICH of the two a call site resolves by, and the two
  // have different exposure: an identifier is a GUID nobody types, while a code is a human-readable value a
  // file can enumerate. Keeping them separate keeps that difference visible where it matters.
  //
  // They exist because an import file names classifications BY CODE — nobody types a GUID into a
  // spreadsheet — and they apply the identical company predicate, which is what makes a code in another
  // company come back ABSENT rather than refused. Without that, a rejection message would confirm which
  // department codes exist in companies the caller cannot see, one row at a time.
  //
  // The argument is the NORMALIZED code, because that is the column the unique index is over and the only
  // one EF can put in a predicate (`DEC-POS-0030`).
  Task<DepartmentAssignmentTarget?> FindAssignableDepartmentByCodeAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default);

  Task<PositionAssignmentTarget?> FindAssignablePositionByCodeAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default);
}

// A department, as an employee operation is allowed to see it. `IsActive` is separate from existence
// because the two produce different answers: an inactive department is named plainly, a department outside
// the company is not acknowledged at all.
public sealed record DepartmentAssignmentTarget(Guid DepartmentId, bool IsActive);

// A position, as an employee operation is allowed to see it. The same two facts and the same separation:
// an inactive position is named plainly (`BRULE-POS-0013`), a position outside the company is not
// acknowledged at all (`BRULE-POS-0016`, `BR-PLT-0002`).
public sealed record PositionAssignmentTarget(Guid PositionId, bool IsActive);
