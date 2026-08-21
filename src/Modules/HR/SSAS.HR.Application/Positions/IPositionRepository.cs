using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions;

// The Position aggregate's own repository (ADR-010, FP-008 Phase 2).
//
// AGGREGATE-SPECIFIC, WITH NO GENERIC SURFACE: no deferred query type crosses this boundary, there is no
// generic repository, and there is deliberately NO DELETE METHOD — physical Position deletion is prohibited
// (`BRULE-POS-0012`) and the absence of a method is the first of the two protections. The second is the
// RESTRICTED foreign keys from `EmployeePositionAssignments` and, from Phase 3, from `Employees`.
//
// ---- THREE REPOSITORIES, NOT ONE, BECAUSE THERE ARE THREE AGGREGATE ROOTS.
//
// `OD-POS-002` ruled `Position`, `JobGrade` and `SalaryGrade` separate aggregates. A single
// `IPositionCatalogRepository` spanning all three would be a generic repository wearing a domain name, and
// it would let a handler load a grade while holding a position's transaction boundary in mind. The grade
// repositories are `IJobGradeRepository` and `ISalaryGradeRepository`.
//
// ---- WHAT IS ABSENT, AND WHY.
//
// There is no employee query here. `Employee.PositionId` does not exist until Phase 3, so "who holds this
// position" cannot be asked yet — and under `OD-POS-005` deactivation never needs to ask it. There is no
// `AppendPositionAssignmentAsync` either: the append happens in the Phase 3 operation that changes an
// employee's position, and its signature belongs beside that handler rather than guessed here.
public interface IPositionRepository
{
  // Tracked, because the caller is about to mutate it. Returns null rather than an error so the caller
  // decides whether absence is a refusal or an ordinary miss — the same contract `IDepartmentRepository`
  // states, and the reason a cross-company load can answer NotFound without a second error type.
  Task<Position?> GetByIdAsync(Guid positionId, CancellationToken cancellationToken = default);

  Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default);

  // The same question, excluding one position — needed when RECODING, where the position's own existing
  // code must not count as a conflict with itself.
  Task<bool> CodeExistsForAnotherAsync(
    Guid companyId,
    string normalizedCode,
    Guid excludedPositionId,
    CancellationToken cancellationToken = default);

  Task AddAsync(Position position, CancellationToken cancellationToken = default);

  // ---- THE DEPENDENT CHECK JOB GRADE DEACTIVATION ASKS (DEC-POS-0013, BRULE-POS-0015).
  //
  // Whether any ACTIVE position references this job grade. Inactive positions do not block it — they are
  // already in the state that makes the dangling reference harmless.
  //
  // It lives on the POSITION repository rather than the job grade's because the rows it counts are
  // positions. A repository that answered questions about another aggregate's table would be the first step
  // toward the generic repository this interface exists to avoid.
  Task<bool> HasActivePositionsForJobGradeAsync(
    Guid jobGradeId, CancellationToken cancellationToken = default);
}
