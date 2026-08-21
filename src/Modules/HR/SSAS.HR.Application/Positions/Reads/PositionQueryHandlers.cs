using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions.Reads;

// The bounds a position-family search must stay inside. Stated as constants rather than inline so the guard
// and the refusal cannot drift apart. Shared by all three families: the page-size ceiling is a property of
// the transport, not of what is being listed.
public static class PositionSearchCriteria
{
  public const int DefaultPageNumber = 1;

  public const int DefaultPageSize = 25;

  // A ceiling, not a clamp. See the refusal below.
  public const int MaxPageSize = 200;
}

public sealed record GetPositionQuery(Guid PositionId);

public sealed record GetJobGradeQuery(Guid JobGradeId);

public sealed record GetSalaryGradeQuery(Guid SalaryGradeId);

// EVERY QUERY RESOLVES A SCOPE FIRST, AND CANNOT REACH THE DATA WITHOUT ONE (FR-POS-0202).
//
// ---- ONE SCOPE, NOT TWO, AND THAT IS THE DIFFERENCE FROM `GetDepartmentQueryHandler`.
//
// The department read needs a second scope because its manager is an EMPLOYEE and employees are
// branch-scoped. A position's nested job grade is company-owned under the same scope this handler already
// holds, so there is no second authorization model to consult and no partial disclosure to represent.
public sealed class GetPositionQueryHandler(
  IPositionScopeResolver scopeResolver,
  IPositionReadService positions)
{
  public async Task<Result<PositionDetail>> HandleAsync(
    GetPositionQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    var scope = await scopeResolver.ResolvePositionsAsync(new PositionScopeRequest(), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<PositionDetail>(scope.Error)
      : await positions.GetAsync(scope.Value, query.PositionId, cancellationToken);
  }
}

public sealed class SearchPositionsQueryHandler(
  IPositionScopeResolver scopeResolver,
  IPositionReadService positions)
{
  public async Task<Result<PagedResult<PositionListItem>>> HandleAsync(
    SearchPositionsQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    // ---- PAGINATION IS REFUSED, NOT CLAMPED.
    //
    // Silently reducing a page size of 5000 to 200 would return a page the caller did not ask for and let
    // them believe they had seen the rest. An out-of-range request is a malformed request — the same rule
    // the employee and department searches already apply.
    var pagination = PositionPagination.Validate(query.Page, query.PageSize);
    if (pagination.IsFailure)
    {
      return Result.Failure<PagedResult<PositionListItem>>(pagination.Error);
    }

    var scope = await scopeResolver.ResolvePositionsAsync(
      new PositionScopeRequest(query.CompanyScope), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<PagedResult<PositionListItem>>(scope.Error)
      : await positions.SearchAsync(scope.Value, query, cancellationToken);
  }
}

public sealed class GetJobGradeQueryHandler(
  IPositionScopeResolver scopeResolver,
  IJobGradeReadService jobGrades)
{
  public async Task<Result<JobGradeDetail>> HandleAsync(
    GetJobGradeQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    var scope = await scopeResolver.ResolveJobGradesAsync(new PositionScopeRequest(), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<JobGradeDetail>(scope.Error)
      : await jobGrades.GetAsync(scope.Value, query.JobGradeId, cancellationToken);
  }
}

public sealed class SearchJobGradesQueryHandler(
  IPositionScopeResolver scopeResolver,
  IJobGradeReadService jobGrades)
{
  public async Task<Result<PagedResult<JobGradeListItem>>> HandleAsync(
    SearchJobGradesQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    var pagination = PositionPagination.Validate(query.Page, query.PageSize);
    if (pagination.IsFailure)
    {
      return Result.Failure<PagedResult<JobGradeListItem>>(pagination.Error);
    }

    var scope = await scopeResolver.ResolveJobGradesAsync(
      new PositionScopeRequest(query.CompanyScope), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<PagedResult<JobGradeListItem>>(scope.Error)
      : await jobGrades.SearchAsync(scope.Value, query, cancellationToken);
  }
}

// THE PAY STRUCTURE (FR-POS-0209, DEC-POS-0018).
//
// Reaching this handler's data needs `HR.SalaryGrades.View` and nothing else will do: the resolver method it
// calls is the only producer of a `SalaryGradeReadScope`, and the read service accepts no other type. A
// caller holding every position and job grade permission still reads no amounts.
public sealed class GetSalaryGradeQueryHandler(
  IPositionScopeResolver scopeResolver,
  ISalaryGradeReadService salaryGrades)
{
  public async Task<Result<SalaryGradeDetail>> HandleAsync(
    GetSalaryGradeQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    var scope = await scopeResolver.ResolveSalaryGradesAsync(new PositionScopeRequest(), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<SalaryGradeDetail>(scope.Error)
      : await salaryGrades.GetAsync(scope.Value, query.SalaryGradeId, cancellationToken);
  }
}

public sealed class SearchSalaryGradesQueryHandler(
  IPositionScopeResolver scopeResolver,
  ISalaryGradeReadService salaryGrades)
{
  public async Task<Result<PagedResult<SalaryGradeListItem>>> HandleAsync(
    SearchSalaryGradesQuery query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);

    var pagination = PositionPagination.Validate(query.Page, query.PageSize);
    if (pagination.IsFailure)
    {
      return Result.Failure<PagedResult<SalaryGradeListItem>>(pagination.Error);
    }

    var scope = await scopeResolver.ResolveSalaryGradesAsync(
      new PositionScopeRequest(query.CompanyScope), cancellationToken);

    return scope.IsFailure
      ? Result.Failure<PagedResult<SalaryGradeListItem>>(scope.Error)
      : await salaryGrades.SearchAsync(scope.Value, query, cancellationToken);
  }
}

// The one bounds check, written once. Six search handlers apply the same rule, and six copies of it is six
// chances for one to start clamping.
internal static class PositionPagination
{
  public static Result Validate(int page, int pageSize) =>
    page < 1 || pageSize < 1 || pageSize > PositionSearchCriteria.MaxPageSize
      ? Result.Failure(PositionErrors.InvalidPagination)
      : Result.Success();
}
