using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions;

// The Job Grade aggregate's own repository (ADR-010, FP-008 Phase 2).
//
// NO DELETE, for the same reason as everywhere else in HR: `BRULE-POS-0012` prohibits physical deletion, and
// the absence of a method is the first protection. The RESTRICTED foreign key from `Positions` is the second.
//
// ---- TWO UNIQUENESS QUESTIONS, NOT ONE.
//
// A job grade's `Code` and its `RankOrder` are each unique within the company (`BRULE-POS-0004`,
// `BRULE-POS-0007`), and they are enforced by two SEPARATE unique indexes. `api-contracts.md` records this
// as a case the Department precedent does not cover — Department had exactly one unique index per operation
// — so both are asked explicitly rather than one being allowed to stand for the other.
public interface IJobGradeRepository
{
  Task<JobGrade?> GetByIdAsync(Guid jobGradeId, CancellationToken cancellationToken = default);

  Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default);

  Task<bool> CodeExistsForAnotherAsync(
    Guid companyId,
    string normalizedCode,
    Guid excludedJobGradeId,
    CancellationToken cancellationToken = default);

  Task<bool> RankOrderExistsAsync(
    Guid companyId, int rankOrder, CancellationToken cancellationToken = default);

  Task<bool> RankOrderExistsForAnotherAsync(
    Guid companyId,
    int rankOrder,
    Guid excludedJobGradeId,
    CancellationToken cancellationToken = default);

  Task AddAsync(JobGrade jobGrade, CancellationToken cancellationToken = default);

  // ---- THE DEPENDENT CHECK SALARY GRADE DEACTIVATION ASKS (DEC-POS-0013, BRULE-POS-0015).
  //
  // Whether any ACTIVE job grade references this salary grade. The reference runs one way only
  // (`BRULE-POS-0010`), so this is the entire dependent set for a salary grade — a position never points at
  // one directly.
  Task<bool> HasActiveJobGradesForSalaryGradeAsync(
    Guid salaryGradeId, CancellationToken cancellationToken = default);
}
