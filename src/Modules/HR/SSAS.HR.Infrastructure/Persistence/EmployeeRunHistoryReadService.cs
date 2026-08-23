using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.ImportExport;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Infrastructure.Persistence;

// THE RUN-HISTORY READS (FR-DOC-0103, FR-DOC-0202).
//
// ---- NEWEST FIRST, WITH A DETERMINISTIC TIE-BREAK.
//
// `ExecutedUtc` descending, then the identifier. Two runs can share a timestamp — a `datetimeoffset` is not
// a unique key and two operators can submit in the same instant — and an unstable sort silently drops and
// duplicates rows across page boundaries, which is precisely the defect a paged audit listing must not have.
//
// ---- SCOPED BY TENANT AND COMPANY, WITH THE PREDICATES WRITTEN OUT.
//
// The tenant predicate is explicit rather than left to the global query filter, for the reason
// `EmployeeReadScope` carries the tenant at all: a query should state the invariant it depends on instead of
// inheriting it. The company predicate is the scope's own materialized set, so a caller sees the runs of the
// companies they are authorized for and no others.
//
// THERE IS NO BRANCH PREDICATE, and its absence is a fact about the rows rather than an omission: neither
// run record carries a branch, because an import or an export is performed within a company and branch is a
// sibling dimension. The scope's branch set is genuinely unused here.
//
// ---- AsNoTracking THROUGHOUT. These are facts being read, never aggregates being mutated — and both types
// are append-only, so a tracked instance could only ever be a liability.
internal sealed class EmployeeRunHistoryReadService(ITenantDbContextAccessor contextAccessor)
  : IEmployeeRunHistoryReadService
{
  public async Task<PagedResult<EmployeeImportRunListItem>> SearchImportRunsAsync(
    EmployeeReadScope scope,
    EmployeeRunHistoryCriteria criteria,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentNullException.ThrowIfNull(criteria);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var query = context.Set<EmployeeImportRun>()
      .AsNoTracking()
      .Where(run => run.TenantId == scope.TenantId)
      .Where(run => scope.Companies.CompanyIds.Contains(run.CompanyId));

    // COUNTED THROUGH THE SAME SCOPED QUERY. A total from a wider one would disclose how many runs exist
    // outside the caller's companies even though none of those rows were returned.
    var totalCount = await query.CountAsync(cancellationToken);

    var items = await query
      .OrderByDescending(run => run.ExecutedUtc)
      .ThenBy(run => run.Id)
      .Skip((criteria.PageNumber - 1) * criteria.PageSize)
      .Take(criteria.PageSize)
      .Select(run => new EmployeeImportRunListItem(
        run.Id,
        // The DISPLAY value, not the normalized one. The caller supplied this casing and gets it back; the
        // normalized column exists for the unique index and never leaves the database.
        run.ImportKey.Value,
        run.FileName,
        run.ByteCount,
        run.RowCount,
        run.AcceptedCount,
        run.RejectedCount,
        run.Outcome,
        run.ExecutedUtc,
        run.ExecutedBy))
      .ToArrayAsync(cancellationToken);

    return new PagedResult<EmployeeImportRunListItem>(
      items, criteria.PageNumber, criteria.PageSize, totalCount);
  }

  public async Task<PagedResult<EmployeeExportRunListItem>> SearchExportRunsAsync(
    EmployeeReadScope scope,
    EmployeeRunHistoryCriteria criteria,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(scope);
    ArgumentNullException.ThrowIfNull(criteria);

    var context = await contextAccessor.GetRequiredAsync(cancellationToken);

    var query = context.Set<EmployeeExportRun>()
      .AsNoTracking()
      .Where(run => run.TenantId == scope.TenantId)
      .Where(run => scope.Companies.CompanyIds.Contains(run.CompanyId));

    var totalCount = await query.CountAsync(cancellationToken);

    // ---- THE PROJECTION NAMES FIVE COLUMNS AND THE SCOPE SNAPSHOT IS NOT AMONG THEM (DEC-DOC-0016).
    //
    // `ScopeCompanyIds` and `ScopeBranchIds` are never selected. The list item has no property to hold them,
    // so this is not a filter that could be relaxed — there is nowhere for the values to go.
    //
    // The column set is split back into a list in memory rather than in SQL: `ColumnSet` is stored
    // comma-joined as a denormalization decision, and translating a split into SQL Server would be work for
    // a bounded page of rows that the client does trivially.
    var rows = await query
      .OrderByDescending(run => run.ExecutedUtc)
      .ThenBy(run => run.Id)
      .Skip((criteria.PageNumber - 1) * criteria.PageSize)
      .Take(criteria.PageSize)
      .Select(run => new
      {
        run.Id,
        run.RowCount,
        run.ColumnSet,
        run.ExecutedUtc,
        run.ExecutedBy
      })
      .ToArrayAsync(cancellationToken);

    var items = rows
      .Select(row => new EmployeeExportRunListItem(
        row.Id,
        row.RowCount,
        row.ColumnSet.Split(',', StringSplitOptions.RemoveEmptyEntries),
        row.ExecutedUtc,
        row.ExecutedBy))
      .ToArray();

    return new PagedResult<EmployeeExportRunListItem>(
      items, criteria.PageNumber, criteria.PageSize, totalCount);
  }
}
