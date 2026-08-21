using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Positions;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Infrastructure.Persistence;

// Position persistence over the shared tenant ERP context (ADR-010, ADR-017, FP-008 Phase 2).
//
// ---- WHAT IS AND IS NOT THIS REPOSITORY'S JOB.
//
// It tracks and reads. It does NOT authorize: the tenant and company boundaries live in the context's save
// pipeline and in the access resolvers, and a repository that re-checked them would be a second opinion
// that could disagree.
//
// Every query states TENANT and COMPANY explicitly where it has them. The tenant global query filter already
// restricts reads to the routed tenant, but the predicate says so anyway so it declares the invariant it
// depends on — and the COMPANY dimension has no global filter at all by deliberate design (ADR-025
// decision 10), so stating it is the only thing scoping these reads.
//
// THERE IS NO DELETE. Physical Position deletion is prohibited (`BRULE-POS-0012`), and the absence of a
// method here is the first of the two protections. The second is the RESTRICTED foreign key from
// `EmployeePositionAssignments`, joined in Phase 3 by the one from `Employees`.
internal sealed class PositionRepository(ITenantDbContextAccessor contextAccessor) : IPositionRepository
{
  public async Task<Position?> GetByIdAsync(
    Guid positionId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Tracked, not AsNoTracking: the caller is about to mutate it, and the change tracker carries the
    // original grade reference and status the guards compare against.
    return await context.Set<Position>()
      .SingleOrDefaultAsync(position => position.Id == positionId, cancellationToken);
  }

  public async Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // COMPANY-SCOPED (`BRULE-POS-0004`). The code is unique within the company, so a narrower predicate
    // would report "available" for a code already taken and leave the unique index to refuse the insert
    // with a raw persistence error.
    return await context.Set<Position>()
      .AsNoTracking()
      .AnyAsync(
        position => position.CompanyId == companyId && position.NormalizedCode == normalizedCode,
        cancellationToken);
  }

  public async Task<bool> CodeExistsForAnotherAsync(
    Guid companyId,
    string normalizedCode,
    Guid excludedPositionId,
    CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // Excluding the position itself, so recoding `ACC-SR` to `ACC-SR` is not a conflict with its own row.
    return await context.Set<Position>()
      .AsNoTracking()
      .AnyAsync(
        position => position.CompanyId == companyId &&
          position.NormalizedCode == normalizedCode &&
          position.Id != excludedPositionId,
        cancellationToken);
  }

  public async Task AddAsync(Position position, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(position);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    await context.Set<Position>().AddAsync(position, cancellationToken);
  }

  // ---- THE DEPENDENT CHECK (DEC-POS-0013, BRULE-POS-0015).
  //
  // Whether any ACTIVE position references this job grade. Inactive positions do not block deactivation —
  // they are already in the state that makes the reference harmless.
  //
  // It runs on the caller's context and therefore inside the caller's transaction, which is what makes the
  // check and the state change one unit rather than two observations of a moving target. Note what that
  // does NOT mean: a LINQ query is answered by the database, so unsaved changes pending in the change
  // tracker are invisible to it either way — the handler asks this question before it mutates anything,
  // which is why that is not a gap.
  public async Task<bool> HasActivePositionsForJobGradeAsync(
    Guid jobGradeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    return await context.Set<Position>()
      .AsNoTracking()
      .AnyAsync(
        position => position.JobGradeId == jobGradeId && position.Status == PositionStatus.Active,
        cancellationToken);
  }
}
