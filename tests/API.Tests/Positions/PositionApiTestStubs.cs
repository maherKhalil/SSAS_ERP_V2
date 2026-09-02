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

// ==================================================================================================
// ⚠ THE THREE READ STUBS MATCH ON ID, AND THAT IS A TRADE RATHER THAN AN OVERSIGHT (274)
// ==================================================================================================
//
// `GetAsync` answers `NotFound` unless `Detail`'s id equals the id asked for, and `Reset` leaves `Detail`
// null. **REFUSAL IS THE DEFAULT AND SUCCESS IS OPT-IN.** Both halves of that have consequences, and
// neither is written anywhere else, so a reader finding only the cost will remove the benefit with it.
//
// ---- WHAT IT BUYS: EVERY 200 IN THIS SUITE PROVES THE ROUTE PASSED THE RIGHT ID.
//
// A route that bound its path segment correctly and then handed a DIFFERENT id to the query would 404
// here. That is route→handler→read propagation, asserted as a side effect of every successful read test,
// and it is not asserted anywhere else in the API suite for any family.
//
// ---- WHAT IT COSTS: THIS HARNESS CANNOT EXPRESS A SUCCESSFUL CREATE. AT ALL.
//
// Every write route reads back through the scoped path. Update, activate, deactivate and `change-position`
// read back by the ROUTE's id, which a test knows in advance and can seed — those succeed fine. **The three
// CREATE routes read back by `created.Value`, an id the aggregate mints**, which no test can predict and
// this stub can therefore never match. A valid create reaches `AddAsync`, writes, and then answers 500.
//
// So `Results.Created`, the `Location` header and the created representation are unasserted for positions,
// job grades and salary grades. ⚠ **MEASURED: that blocks ZERO acceptance criteria** — the criteria naming
// creation are domain and schema claims carried elsewhere, and the API-layer criteria are two refusals and
// two reads. **The hole is real and no criterion stands on it, which is why this is annotated and not fixed.**
//
// ---- ⚠⚠ DO NOT "FIX" IT BY COPYING `StubDepartmentReads`. IT MAKES THE OPPOSITE TRADE.
//
// That stub ignores its `departmentId` entirely and defaults `Detail` to a sample, so success is free and
// creates work — and nothing in that suite can tell whether the route passed the right id. **Copying it
// closes this hole and silently opens that one.** The mechanism worth borrowing is *let a test express a
// success*; the id-matching is this file's own strength and should survive any change.
//
// The form that keeps both is id-matching by default with an explicit opt-in for the create case. It is
// not built because nothing yet needs it.
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
//
// ==================================================================================================
// ⚠⚠⚠ `Existing` IS SEEDED BY NO TEST IN THIS ASSEMBLY, AND EVERY UPDATE ROUTE THEREFORE ANSWERS 404
// ==================================================================================================
//
// Measured 2026-09-02. `Existing` is declared here, set to null by `Reset()`, read by `GetByIdAsync` —
// **and assigned nowhere.** So `GetByIdAsync` always returns null, every handler stops at its not-found
// branch, and **every `PUT`, `activate` and `deactivate` in `PositionEndpointTests.Fp008Routes()` answers
// `404`.** The same holds for `StubJobGradeRepository` and `StubSalaryGradeRepository`.
//
// ⚠⚠ THE CONSEQUENCE IS NOT MERELY *those routes are untested*. **THE POSITION PERMISSION MATRIX CANNOT
// DISTINGUISH *the caller is authorised* FROM *the route is broken*:** it asserts `403` versus not-`403`,
// and `404` satisfies not-`403`. **A position update route that had stopped working entirely would pass
// that matrix unchanged** — an observable shared between the intended behaviour and a failure.
//
// **So nothing gated reaches any post-load rule on these aggregates**: the concurrency pre-check at
// `PositionCommandHandlers:245`, the uniqueness probes on update, the dependent checks. `AC-POS-0047`'s
// behavioural half is unassertable here for that reason and not because the property needs a database.
//
// ---- HOW IT WAS FOUND, BECAUSE READING WOULD NOT HAVE FOUND IT.
//
// ⚠ By writing a stale-`RowVersion` test and watching all three cases return `404` instead of `409`.
// **No artefact was wrong: no comment stale, no name misleading, no search that would have named it. The
// defect is the ABSENCE OF AN ASSIGNMENT, which nothing describes.** A reader checking this file sees a
// field declared and nulled and has no reason to ask whether anyone ever sets it.
//
// ---- WHAT FIXING IT COSTS, SO THE NEXT READER DOES NOT UNDER-ESTIMATE IT AS I DID.
//
// Seeding needs a real `Position`, `JobGrade` and `SalaryGrade` built through their own factories with
// `RowVersion` set by reflection — `EmployeeApiTestStubs.SetRowVersion` at `:285` is the precedent, and
// that harness DOES seed its employee, which is exactly why the employee-side equivalent works.
// **That is establishing seeding this harness has never had, not applying a pattern.**
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
