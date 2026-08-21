using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Infrastructure.Persistence;

// THE TWO GRADE LADDERS' READ PATHS (FP-008 Phase 2).
//
// Both state the same two dimensions in every predicate for the same reasons `PositionReadService` gives,
// and neither carries a branch dimension. What differs is the KEY the scope type carries: a
// `JobGradeReadScope` is producible only by the resolver method that checked `HR.JobGrades.View`, and a
// `SalaryGradeReadScope` only by the one that checked `HR.SalaryGrades.View`. The signatures below are
// therefore the enforcement of `DEC-POS-0018`'s separation, not a restatement of it.
internal sealed class JobGradeReadService(ITenantDbContextAccessor contextAccessor) : IJobGradeReadService
{
  public async Task<Result<JobGradeDetail>> GetAsync(
    JobGradeReadScope scope, Guid jobGradeId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var grade = await Scoped(context, scope)
      .Where(item => item.Id == jobGradeId)
      .Select(item => new JobGradeDetail(
        item.Id,
        item.CompanyId,
        item.Code.Value,
        item.Name.Value,
        item.RankOrder,
        item.SalaryGradeId,
        item.Status,
        item.RowVersion))
      .SingleOrDefaultAsync(cancellationToken);

    return grade is null
      ? Result.Failure<JobGradeDetail>(PositionErrors.JobGradeNotFound)
      : Result.Success(grade);
  }

  public async Task<Result<PagedResult<JobGradeListItem>>> SearchAsync(
    JobGradeReadScope scope, SearchJobGradesQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentNullException.ThrowIfNull(query);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var filtered = Scoped(context, scope);

    if (query.Status is { } status)
    {
      filtered = filtered.Where(item => item.Status == status);
    }

    if (!string.IsNullOrWhiteSpace(query.SearchText))
    {
      // THE CODE HALF ONLY — see `PositionReadService.SearchAsync` for why the name half cannot be
      // translated over a value-converted property, and for the identical unfixed defect in
      // `DepartmentReadService`. Not implemented here either, and awaiting the same ruling.
      var normalized = query.SearchText.Trim().ToUpperInvariant();

      filtered = filtered.Where(item => item.NormalizedCode.StartsWith(normalized));
    }

    var total = await filtered.CountAsync(cancellationToken);

    var page = new PageRequest(query.Page, query.PageSize);

    // ---- ORDERED BY RANK, NOT BY CODE, AND THAT IS THE DIFFERENCE FROM EVERY OTHER SEARCH HERE.
    //
    // A grade ladder has an inherent order and `RankOrder` is authoritative data rather than a derived
    // label (`DEC-POS-0006`). Listing grades alphabetically would present G10 between G1 and G2 and make
    // the ladder unreadable in the one view whose purpose is to show it.
    //
    // Rank is unique within a company, so Id breaks only the remaining tie across companies — which is what
    // keeps paging stable when a search spans several.
    var items = await filtered
      .OrderBy(item => item.RankOrder)
      .ThenBy(item => item.Id)
      .Skip(page.Skip)
      .Take(page.PageSize)
      .Select(item => new JobGradeListItem(
        item.Id,
        item.CompanyId,
        item.Code.Value,
        item.Name.Value,
        item.RankOrder,
        item.SalaryGradeId,
        item.Status,
        item.RowVersion))
      .ToArrayAsync(cancellationToken);

    return Result.Success(
      new PagedResult<JobGradeListItem>(items, page.PageNumber, page.PageSize, total));
  }

  private static IQueryable<JobGrade> Scoped(DbContext context, JobGradeReadScope scope) =>
    context.Set<JobGrade>()
      .AsNoTracking()
      .Where(grade =>
        grade.TenantId == scope.TenantId &&
        scope.Companies.CompanyIds.Contains(grade.CompanyId));
}

// THE PAY STRUCTURE. Reachable only through a `SalaryGradeReadScope`, and the only thing that produces one
// is the resolver method that checked `HR.SalaryGrades.View` (`DEC-POS-0018`, `DEC-EMP-0030` precedent).
internal sealed class SalaryGradeReadService(ITenantDbContextAccessor contextAccessor)
  : ISalaryGradeReadService
{
  public async Task<Result<SalaryGradeDetail>> GetAsync(
    SalaryGradeReadScope scope, Guid salaryGradeId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The band is projected COLUMN BY COLUMN rather than materialized as the value object. Under
    // `DEC-POS-0027` the three columns are all-null or all-present together, so the nulls travel as a set —
    // and projecting the optional owned type here would make this read depend on EF's null-detection for a
    // shape the read model flattens anyway.
    var grade = await Scoped(context, scope)
      .Where(item => item.Id == salaryGradeId)
      .Select(item => new SalaryGradeDetail(
        item.Id,
        item.CompanyId,
        item.Code.Value,
        item.Name.Value,
        item.RankOrder,
        item.Band == null ? null : item.Band.MinimumAmount,
        item.Band == null ? null : item.Band.MidpointAmount,
        item.Band == null ? null : item.Band.MaximumAmount,
        item.Status,
        item.RowVersion))
      .SingleOrDefaultAsync(cancellationToken);

    return grade is null
      ? Result.Failure<SalaryGradeDetail>(PositionErrors.SalaryGradeNotFound)
      : Result.Success(grade);
  }

  public async Task<Result<PagedResult<SalaryGradeListItem>>> SearchAsync(
    SalaryGradeReadScope scope,
    SearchSalaryGradesQuery query,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentNullException.ThrowIfNull(query);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var filtered = Scoped(context, scope);

    if (query.Status is { } status)
    {
      filtered = filtered.Where(item => item.Status == status);
    }

    if (!string.IsNullOrWhiteSpace(query.SearchText))
    {
      // THE CODE HALF ONLY — see `PositionReadService.SearchAsync` for why the name half cannot be
      // translated over a value-converted property, and for the identical unfixed defect in
      // `DepartmentReadService`. Not implemented here either, and awaiting the same ruling.
      var normalized = query.SearchText.Trim().ToUpperInvariant();

      filtered = filtered.Where(item => item.NormalizedCode.StartsWith(normalized));
    }

    var total = await filtered.CountAsync(cancellationToken);

    var page = new PageRequest(query.Page, query.PageSize);

    // By rank, for the same reason the job grade ladder is: the order is the ladder.
    var items = await filtered
      .OrderBy(item => item.RankOrder)
      .ThenBy(item => item.Id)
      .Skip(page.Skip)
      .Take(page.PageSize)
      .Select(item => new SalaryGradeListItem(
        item.Id,
        item.CompanyId,
        item.Code.Value,
        item.Name.Value,
        item.RankOrder,
        item.Band == null ? null : item.Band.MinimumAmount,
        item.Band == null ? null : item.Band.MidpointAmount,
        item.Band == null ? null : item.Band.MaximumAmount,
        item.Status,
        item.RowVersion))
      .ToArrayAsync(cancellationToken);

    return Result.Success(
      new PagedResult<SalaryGradeListItem>(items, page.PageNumber, page.PageSize, total));
  }

  private static IQueryable<SalaryGrade> Scoped(DbContext context, SalaryGradeReadScope scope) =>
    context.Set<SalaryGrade>()
      .AsNoTracking()
      .Where(grade =>
        grade.TenantId == scope.TenantId &&
        scope.Companies.CompanyIds.Contains(grade.CompanyId));
}
