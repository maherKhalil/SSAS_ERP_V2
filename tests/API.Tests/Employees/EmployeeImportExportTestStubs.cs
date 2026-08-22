using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.ImportExport;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.API.Tests.Employees;

// THE FP-009 STUBS FOR THE API TEST HOST (R12).
//
// The host's established pattern is REAL command handlers over stubbed persistence: the import handler runs
// for real, composing the real `CreateEmployeeCommandHandler` over `StubEmployeeRepository`, so a transport
// test exercises the genuine pipeline and only the database is doubled. These supply the three seams FP-009
// added.
//
// They are deliberately DUMB. Every rule — all-or-nothing, create-only, code resolution, the caps — is
// proven against real SQL Server in `Integration.Tests`, and re-proving it here against a double would test
// the double. What these enable is transport: content types, status codes, headers, the report's wire shape.

// The import run store. A list rather than a dictionary because the replay lookup is by (company, key) and
// modelling that as a composite key would hide which of the two the lookup actually filters on.
public sealed class StubImportRunRepository : IEmployeeImportRunRepository
{
  public List<EmployeeImportRun> Runs { get; } = [];

  // Set by a test that wants the REPLAY path without performing a first import.
  public EmployeeImportRun? Existing { get; set; }

  public void Reset()
  {
    Runs.Clear();
    Existing = null;
  }

  public Task<EmployeeImportRun?> FindByKeyAsync(
    Guid companyId, string normalizedImportKey, CancellationToken cancellationToken = default)
  {
    if (Existing is not null)
    {
      return Task.FromResult<EmployeeImportRun?>(Existing);
    }

    // The same predicate the real repository applies: company AND normalized key. A stub that matched on the
    // key alone would let a test pass that the production query would fail.
    return Task.FromResult(Runs.Find(run =>
      run.CompanyId == companyId && run.NormalizedImportKey == normalizedImportKey));
  }

  public Task AddAsync(EmployeeImportRun run, CancellationToken cancellationToken = default)
  {
    Runs.Add(run);

    return Task.CompletedTask;
  }
}

public sealed class StubExportRunRepository : IEmployeeExportRunRepository
{
  public List<EmployeeExportRun> Runs { get; } = [];

  public void Reset() => Runs.Clear();

  public Task AddAsync(EmployeeExportRun run, CancellationToken cancellationToken = default)
  {
    Runs.Add(run);

    return Task.CompletedTask;
  }
}

// The run-history reads. Records the scope it was called with, like every other read stub here, so a test
// can prove the listing went through a resolved scope rather than around one.
public sealed class StubRunHistoryReads : IEmployeeRunHistoryReadService
{
  public EmployeeReadScope? LastScope { get; private set; }

  public List<EmployeeImportRunListItem> ImportRuns { get; } = [];

  public List<EmployeeExportRunListItem> ExportRuns { get; } = [];

  public void Reset()
  {
    LastScope = null;
    ImportRuns.Clear();
    ExportRuns.Clear();
  }

  public Task<PagedResult<EmployeeImportRunListItem>> SearchImportRunsAsync(
    EmployeeReadScope scope,
    EmployeeRunHistoryCriteria criteria,
    CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(new PagedResult<EmployeeImportRunListItem>(
      ImportRuns, criteria.PageNumber, criteria.PageSize, ImportRuns.Count));
  }

  public Task<PagedResult<EmployeeExportRunListItem>> SearchExportRunsAsync(
    EmployeeReadScope scope,
    EmployeeRunHistoryCriteria criteria,
    CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(new PagedResult<EmployeeExportRunListItem>(
      ExportRuns, criteria.PageNumber, criteria.PageSize, ExportRuns.Count));
  }
}
