using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions;

// The Salary Grade aggregate's own repository (ADR-010, FP-008 Phase 2).
//
// Identical in shape to `IJobGradeRepository` and deliberately NOT shared with it. The two ladders are
// separate aggregates (`OD-POS-002`) whose uniqueness scopes are separate — a rank of 70 may exist once in
// each — and a shared repository over a generic grade type would make that separation a runtime argument
// instead of a compile-time fact.
//
// ---- IT ASKS NO DEPENDENT QUESTION.
//
// Nothing references a salary grade except a job grade, and that question is answered by
// `IJobGradeRepository.HasActiveJobGradesForSalaryGradeAsync` — on the repository that owns the rows being
// counted. This interface therefore has no dependent method at all, which is the asymmetry the one-way
// reference (`BRULE-POS-0010`, `DEC-POS-0002`) produces.
public interface ISalaryGradeRepository
{
  Task<SalaryGrade?> GetByIdAsync(Guid salaryGradeId, CancellationToken cancellationToken = default);

  Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default);

  Task<bool> CodeExistsForAnotherAsync(
    Guid companyId,
    string normalizedCode,
    Guid excludedSalaryGradeId,
    CancellationToken cancellationToken = default);

  Task<bool> RankOrderExistsAsync(
    Guid companyId, int rankOrder, CancellationToken cancellationToken = default);

  Task<bool> RankOrderExistsForAnotherAsync(
    Guid companyId,
    int rankOrder,
    Guid excludedSalaryGradeId,
    CancellationToken cancellationToken = default);

  Task AddAsync(SalaryGrade salaryGrade, CancellationToken cancellationToken = default);
}
