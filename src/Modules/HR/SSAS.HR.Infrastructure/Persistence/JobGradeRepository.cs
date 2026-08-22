using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Positions;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Infrastructure.Persistence;

// Job Grade persistence over the shared tenant ERP context (ADR-010, ADR-017, FP-008 Phase 2).
//
// Same contract as `PositionRepository`: it tracks and reads, it authorizes nothing, every predicate states
// the company dimension explicitly because no global filter supplies it, and there is no delete.
//
// The two uniqueness questions are asked separately because two separate unique indexes enforce them, and
// they are not interchangeable — `UX_JobGrades_TenantId_CompanyId_NormalizedCode` and
// `UX_JobGrades_TenantId_CompanyId_RankOrder`.
internal sealed class JobGradeRepository(ITenantDbContextAccessor contextAccessor) : IJobGradeRepository
{
  public async Task<JobGrade?> GetByIdAsync(
    Guid jobGradeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<JobGrade>()
      .SingleOrDefaultAsync(grade => grade.Id == jobGradeId, cancellationToken);
  }

  public async Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<JobGrade>()
      .AsNoTracking()
      .AnyAsync(
        grade => grade.CompanyId == companyId && grade.NormalizedCode == normalizedCode,
        cancellationToken);
  }

  public async Task<bool> CodeExistsForAnotherAsync(
    Guid companyId,
    string normalizedCode,
    Guid excludedJobGradeId,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<JobGrade>()
      .AsNoTracking()
      .AnyAsync(
        grade => grade.CompanyId == companyId &&
          grade.NormalizedCode == normalizedCode &&
          grade.Id != excludedJobGradeId,
        cancellationToken);
  }

  public async Task<bool> RankOrderExistsAsync(
    Guid companyId, int rankOrder, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<JobGrade>()
      .AsNoTracking()
      .AnyAsync(
        grade => grade.CompanyId == companyId && grade.RankOrder == rankOrder,
        cancellationToken);
  }

  public async Task<bool> RankOrderExistsForAnotherAsync(
    Guid companyId,
    int rankOrder,
    Guid excludedJobGradeId,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<JobGrade>()
      .AsNoTracking()
      .AnyAsync(
        grade => grade.CompanyId == companyId &&
          grade.RankOrder == rankOrder &&
          grade.Id != excludedJobGradeId,
        cancellationToken);
  }

  public async Task AddAsync(JobGrade jobGrade, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(jobGrade);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    await context.Set<JobGrade>().AddAsync(jobGrade, cancellationToken);
  }

  // The whole dependent set of a salary grade, because the reference runs one way (`BRULE-POS-0010`).
  public async Task<bool> HasActiveJobGradesForSalaryGradeAsync(
    Guid salaryGradeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<JobGrade>()
      .AsNoTracking()
      .AnyAsync(
        grade => grade.SalaryGradeId == salaryGradeId && grade.Status == JobGradeStatus.Active,
        cancellationToken);
  }
}
