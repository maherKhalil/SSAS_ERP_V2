using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Positions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.API.Tests.Positions;

// ==================================================================================================
// THE COLLABORATORS THE POSITION SURFACE WOULD OTHERWISE NEED SQL SERVER FOR.
// ==================================================================================================
//
// Each stub answers from a seeded field, so a test states the world it wants and then asserts what the HTTP
// layer did with it. What is NOT stubbed is anything that decides: the scope resolvers, the handlers, the
// error mapper and the composition are all production code in this harness.
//
// ---- THE READ STUBS TAKE A SCOPE AND RECORD IT.
//
// They cannot be called without one — the interfaces make that a compile error — and recording it lets a
// test assert that the scope the route obtained is the scope the read received, which is the property the
// whole read-scope design exists to guarantee.

public sealed class StubPositionReads : IPositionReadService
{
  public PositionDetail? Detail { get; set; }

  public IReadOnlyList<PositionListItem> Page { get; set; } = [];

  public PositionReadScope? LastScope { get; private set; }

  public void Reset()
  {
    Detail = null;
    Page = [];
    LastScope = null;
  }

  public Task<Result<PositionDetail>> GetAsync(
    PositionReadScope scope, Guid positionId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(Detail is null || Detail.PositionId != positionId
      ? Result.Failure<PositionDetail>(PositionErrors.PositionNotFound)
      : Result.Success(Detail));
  }

  public Task<Result<PagedResult<PositionListItem>>> SearchAsync(
    PositionReadScope scope, SearchPositionsQuery query, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(Result.Success(
      new PagedResult<PositionListItem>(Page, query.Page, query.PageSize, Page.Count)));
  }
}

public sealed class StubJobGradeReads : IJobGradeReadService
{
  public JobGradeDetail? Detail { get; set; }

  public IReadOnlyList<JobGradeListItem> Page { get; set; } = [];

  public JobGradeReadScope? LastScope { get; private set; }

  public void Reset()
  {
    Detail = null;
    Page = [];
    LastScope = null;
  }

  public Task<Result<JobGradeDetail>> GetAsync(
    JobGradeReadScope scope, Guid jobGradeId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(Detail is null || Detail.JobGradeId != jobGradeId
      ? Result.Failure<JobGradeDetail>(PositionErrors.JobGradeNotFound)
      : Result.Success(Detail));
  }

  public Task<Result<PagedResult<JobGradeListItem>>> SearchAsync(
    JobGradeReadScope scope, SearchJobGradesQuery query, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(Result.Success(
      new PagedResult<JobGradeListItem>(Page, query.Page, query.PageSize, Page.Count)));
  }
}

// The sensitive one. It can only be reached with a `SalaryGradeReadScope`, and the only thing that produces
// one is the resolver method that checked `HR.SalaryGrades.View` — so a test proving that a position-only
// caller never gets here is proving a compile-time property at runtime.
public sealed class StubSalaryGradeReads : ISalaryGradeReadService
{
  public SalaryGradeDetail? Detail { get; set; }

  public IReadOnlyList<SalaryGradeListItem> Page { get; set; } = [];

  public SalaryGradeReadScope? LastScope { get; private set; }

  public void Reset()
  {
    Detail = null;
    Page = [];
    LastScope = null;
  }

  public Task<Result<SalaryGradeDetail>> GetAsync(
    SalaryGradeReadScope scope, Guid salaryGradeId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(Detail is null || Detail.SalaryGradeId != salaryGradeId
      ? Result.Failure<SalaryGradeDetail>(PositionErrors.SalaryGradeNotFound)
      : Result.Success(Detail));
  }

  public Task<Result<PagedResult<SalaryGradeListItem>>> SearchAsync(
    SalaryGradeReadScope scope,
    SearchSalaryGradesQuery query,
    CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(Result.Success(
      new PagedResult<SalaryGradeListItem>(Page, query.Page, query.PageSize, Page.Count)));
  }
}

// ---- THE WRITE STUBS.
//
// `Existing` is the aggregate a load returns; the uniqueness probes answer from flags a test sets, so the
// conflict arms of the mapper are reachable without a database. `Added` records what a create handed over,
// which is how a test asserts that a refused write wrote nothing.
public sealed class StubPositionRepository : IPositionRepository
{
  public Position? Existing { get; set; }

  public bool CodeTaken { get; set; }

  public bool HasActiveDependents { get; set; }

  public Position? Added { get; private set; }

  public void Reset()
  {
    Existing = null;
    CodeTaken = false;
    HasActiveDependents = false;
    Added = null;
  }

  public Task<Position?> GetByIdAsync(Guid positionId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing?.Id == positionId ? Existing : null);

  public Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task<bool> CodeExistsForAnotherAsync(
    Guid companyId, string normalizedCode, Guid excludedPositionId,
    CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task AddAsync(Position position, CancellationToken cancellationToken = default)
  {
    Added = position;

    return Task.CompletedTask;
  }

  public Task<bool> HasActivePositionsForJobGradeAsync(
    Guid jobGradeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(HasActiveDependents);
}

public sealed class StubJobGradeRepository : IJobGradeRepository
{
  public JobGrade? Existing { get; set; }

  public bool CodeTaken { get; set; }

  public bool RankTaken { get; set; }

  public bool HasActiveDependents { get; set; }

  public JobGrade? Added { get; private set; }

  public void Reset()
  {
    Existing = null;
    CodeTaken = false;
    RankTaken = false;
    HasActiveDependents = false;
    Added = null;
  }

  public Task<JobGrade?> GetByIdAsync(Guid jobGradeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing?.Id == jobGradeId ? Existing : null);

  public Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task<bool> CodeExistsForAnotherAsync(
    Guid companyId, string normalizedCode, Guid excludedJobGradeId,
    CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task<bool> RankOrderExistsAsync(
    Guid companyId, int rankOrder, CancellationToken cancellationToken = default) =>
    Task.FromResult(RankTaken);

  public Task<bool> RankOrderExistsForAnotherAsync(
    Guid companyId, int rankOrder, Guid excludedJobGradeId,
    CancellationToken cancellationToken = default) =>
    Task.FromResult(RankTaken);

  public Task AddAsync(JobGrade jobGrade, CancellationToken cancellationToken = default)
  {
    Added = jobGrade;

    return Task.CompletedTask;
  }

  public Task<bool> HasActiveJobGradesForSalaryGradeAsync(
    Guid salaryGradeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(HasActiveDependents);
}

public sealed class StubSalaryGradeRepository : ISalaryGradeRepository
{
  public SalaryGrade? Existing { get; set; }

  public bool CodeTaken { get; set; }

  public bool RankTaken { get; set; }

  public SalaryGrade? Added { get; private set; }

  public void Reset()
  {
    Existing = null;
    CodeTaken = false;
    RankTaken = false;
    Added = null;
  }

  public Task<SalaryGrade?> GetByIdAsync(
    Guid salaryGradeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Existing?.Id == salaryGradeId ? Existing : null);

  public Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task<bool> CodeExistsForAnotherAsync(
    Guid companyId, string normalizedCode, Guid excludedSalaryGradeId,
    CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeTaken);

  public Task<bool> RankOrderExistsAsync(
    Guid companyId, int rankOrder, CancellationToken cancellationToken = default) =>
    Task.FromResult(RankTaken);

  public Task<bool> RankOrderExistsForAnotherAsync(
    Guid companyId, int rankOrder, Guid excludedSalaryGradeId,
    CancellationToken cancellationToken = default) =>
    Task.FromResult(RankTaken);

  public Task AddAsync(SalaryGrade salaryGrade, CancellationToken cancellationToken = default)
  {
    Added = salaryGrade;

    return Task.CompletedTask;
  }
}
