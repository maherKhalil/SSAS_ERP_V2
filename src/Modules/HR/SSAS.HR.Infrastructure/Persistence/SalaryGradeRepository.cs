using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Positions;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Infrastructure.Persistence;

// Salary Grade persistence over the shared tenant ERP context (ADR-010, ADR-017, FP-008 Phase 2).
//
// Identical in shape to `JobGradeRepository` and separate from it on purpose: the two ladders' uniqueness
// scopes are independent, so a rank of 70 may exist once in each, and one repository over both would make
// that a runtime argument rather than a compile-time fact.
//
// It asks no dependent question. Nothing references a salary grade except a job grade, and that count is
// taken by the repository that owns the rows — `JobGradeRepository.HasActiveJobGradesForSalaryGradeAsync`.
internal sealed class SalaryGradeRepository(ITenantDbContextAccessor contextAccessor)
  : ISalaryGradeRepository
{
  public async Task<SalaryGrade?> GetByIdAsync(
    Guid salaryGradeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<SalaryGrade>()
      .SingleOrDefaultAsync(grade => grade.Id == salaryGradeId, cancellationToken);
  }

  public async Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<SalaryGrade>()
      .AsNoTracking()
      .AnyAsync(
        grade => grade.CompanyId == companyId && grade.NormalizedCode == normalizedCode,
        cancellationToken);
  }

  public async Task<bool> CodeExistsForAnotherAsync(
    Guid companyId,
    string normalizedCode,
    Guid excludedSalaryGradeId,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<SalaryGrade>()
      .AsNoTracking()
      .AnyAsync(
        grade => grade.CompanyId == companyId &&
          grade.NormalizedCode == normalizedCode &&
          grade.Id != excludedSalaryGradeId,
        cancellationToken);
  }

  public async Task<bool> RankOrderExistsAsync(
    Guid companyId, int rankOrder, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<SalaryGrade>()
      .AsNoTracking()
      .AnyAsync(
        grade => grade.CompanyId == companyId && grade.RankOrder == rankOrder,
        cancellationToken);
  }

  public async Task<bool> RankOrderExistsForAnotherAsync(
    Guid companyId,
    int rankOrder,
    Guid excludedSalaryGradeId,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<SalaryGrade>()
      .AsNoTracking()
      .AnyAsync(
        grade => grade.CompanyId == companyId &&
          grade.RankOrder == rankOrder &&
          grade.Id != excludedSalaryGradeId,
        cancellationToken);
  }

  public async Task AddAsync(SalaryGrade salaryGrade, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(salaryGrade);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    await context.Set<SalaryGrade>().AddAsync(salaryGrade, cancellationToken);
  }
}
