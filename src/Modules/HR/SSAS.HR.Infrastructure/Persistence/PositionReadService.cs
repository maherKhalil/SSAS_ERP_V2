using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Infrastructure.Persistence;

// ==================================================================================================
// THE PLACE WHERE THE POSITION SCOPE BECOMES SQL (FP-008 Phase 2, ADR-025 decision 10)
// ==================================================================================================
//
// ---- EVERY QUERY STATES BOTH DIMENSIONS, EXPLICITLY, IN ITS OWN PREDICATE.
//
// `TenantId = @tenant AND CompanyId IN (@companies)`, written out at every call site below through the one
// `Scoped` helper. Neither is inherited: tenant has a global query filter and the predicate restates it
// anyway, so the query declares the invariant it depends on; company has no filter at all, deliberately and
// permanently, so the explicit predicate is the only thing scoping these reads.
//
// ---- TWO DIMENSIONS, AND THE THIRD IS ABSENT BY DESIGN (DEC-POS-0020, BRULE-POS-0003).
//
// `EmployeeReadService` adds `BranchId IN (@branches)`. This does not, because a Position is not
// branch-owned: its VISIBILITY is a company question. A caller authorized for Riyadh only still reads the
// company-wide Senior Accountant position, including when every current holder works in Jeddah — what they
// must not see is WHO holds it, and that stays behind the untouched Employee read path.
//
// ---- IT AUTHORIZES NOTHING.
//
// Holding a `PositionReadScope` IS the authorization, already performed against live state. Re-deciding it
// here would be a second opinion that could disagree with the one the write path uses.
internal sealed class PositionReadService(ITenantDbContextAccessor contextAccessor) : IPositionReadService
{
  public async Task<Result<PositionDetail>> GetAsync(
    PositionReadScope scope, Guid positionId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    // The identifier is the LAST condition, not the first. A position outside the scope simply does not
    // match, so it is reported as absent rather than as forbidden — which is what stops this read being
    // used to probe for positions in companies the caller cannot see (BR-PLT-0002).
    var position = await Scoped(context, scope)
      .Where(item => item.Id == positionId)
      .Select(item => new
      {
        item.Id,
        item.CompanyId,
        Code = item.Code.Value,
        Title = item.Title.Value,
        item.JobGradeId,
        item.Status,
        item.RowVersion
      })
      .SingleOrDefaultAsync(cancellationToken);

    if (position is null)
    {
      return Result.Failure<PositionDetail>(PositionErrors.PositionNotFound);
    }

    // ---- THE GRADE BLOCK, RESOLVED UNDER THE SAME SCOPE AND NO OTHER.
    //
    // The grade is company-owned exactly as the position is, so this crosses no authorization boundary —
    // which is precisely why it is joined here while `DepartmentReadService` refuses to join its manager.
    // The predicate is stated again rather than trusted from the position that led here: a grade in another
    // company would be a broken reference, and this read reports it as absent rather than disclosing it.
    var jobGrade = position.JobGradeId is not { } gradeId
      ? null
      : await context.Set<JobGrade>()
        .AsNoTracking()
        .Where(item =>
          item.TenantId == scope.TenantId &&
          scope.Companies.CompanyIds.Contains(item.CompanyId) &&
          item.Id == gradeId)
        .Select(item => new PositionJobGradeSummary(
          item.Id, item.Code.Value, item.Name.Value, item.RankOrder))
        .SingleOrDefaultAsync(cancellationToken);

    return Result.Success(new PositionDetail(
      position.Id,
      position.CompanyId,
      position.Code,
      position.Title,
      position.JobGradeId,
      jobGrade,
      position.Status,
      position.RowVersion));
  }

  public async Task<Result<PagedResult<PositionListItem>>> SearchAsync(
    PositionReadScope scope, SearchPositionsQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentNullException.ThrowIfNull(query);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var filtered = Scoped(context, scope);

    if (query.Status is { } status)
    {
      filtered = filtered.Where(item => item.Status == status);
    }

    if (query.JobGradeId is { } jobGradeId)
    {
      filtered = filtered.Where(item => item.JobGradeId == jobGradeId);
    }

    if (!string.IsNullOrWhiteSpace(query.SearchText))
    {
      // ================================================================================================
      // BOTH HALVES, OVER NORMALIZED COLUMNS (FR-POS-0203, DEC-POS-0030)
      // ================================================================================================
      //
      // The CODE half is a prefix match and the TITLE half is a contains — the code is an identifier a user
      // types from the beginning, while a title is a phrase they remember part of.
      //
      // Both run against plain normalized columns rather than against `Code` and `Title`, which are mapped
      // through value converters. EF Core translates a converted member in a PROJECTION but not in a
      // PREDICATE, and the title half of this filter did not exist at all until `DEC-POS-0030` added the
      // column. Both patterns escape the caller's wildcards, so a literal `%` in the search text finds
      // records containing a percent sign rather than every record in scope.
      var codePattern = SearchPattern.StartsWith(query.SearchText);
      var titlePattern = SearchPattern.Contains(query.SearchText);

      filtered = filtered.Where(item =>
        EF.Functions.Like(item.NormalizedCode, codePattern, "\\") ||
        EF.Functions.Like(item.NormalizedTitle, titlePattern, "\\"));
    }

    var total = await filtered.CountAsync(cancellationToken);

    var page = new PageRequest(query.Page, query.PageSize);

    // ---- DETERMINISTIC ORDER, WITH A TIE-BREAK THAT CANNOT TIE.
    //
    // NormalizedCode is unique within a company, but a search may span several companies, so two rows can
    // share a code. Id breaks that remaining tie, which is what makes paging stable: without it, two pages
    // could return the same row or skip one entirely.
    var items = await filtered
      .OrderBy(item => item.NormalizedCode)
      .ThenBy(item => item.Id)
      .Skip(page.Skip)
      .Take(page.PageSize)
      .Select(item => new PositionListItem(
        item.Id,
        item.CompanyId,
        item.Code.Value,
        item.Title.Value,
        item.JobGradeId,
        item.Status,
        item.RowVersion))
      .ToArrayAsync(cancellationToken);

    return Result.Success(
      new PagedResult<PositionListItem>(items, page.PageNumber, page.PageSize, total));
  }

  // THE ONE PLACE THE SCOPE PREDICATE IS WRITTEN. Every query above starts here, so none of them can start
  // anywhere else — and the scope sets are non-empty by construction, so the IN list is never empty.
  private static IQueryable<Position> Scoped(DbContext context, PositionReadScope scope) =>
    context.Set<Position>()
      .AsNoTracking()
      .Where(position =>
        position.TenantId == scope.TenantId &&
        scope.Companies.CompanyIds.Contains(position.CompanyId));
}
