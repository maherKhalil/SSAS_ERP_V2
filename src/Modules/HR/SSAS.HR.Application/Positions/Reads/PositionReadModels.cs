using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Application.Positions.Reads;

// WHAT A POSITION LOOKS LIKE TO A READER (FP-008 Phase 2).
//
// Ownership identifiers are carried because the existing Employee and Department read models carry theirs,
// and a caller that cannot tell which company a position belongs to cannot render a multi-company list.
//
// ---- THE GRADE IS RESOLVED INSIDE THIS READ, AND THE DEPARTMENT MANAGER WAS NOT. THE DIFFERENCE MATTERS.
//
// `DepartmentDetail` deliberately carries only the manager's IDENTIFIER, because resolving it means reading
// an EMPLOYEE, and employees are branch-scoped while departments are company-scoped — so a join would have
// disclosed a person on the strength of department visibility alone.
//
// A job grade crosses no such boundary. It is company-owned exactly as the position is, it is visible under
// the same company scope, and `api-contracts.md` specifies the nested block in the Position representation.
// So it is joined here rather than resolved through a second scope.
//
// The line that IS drawn is one step further down: a job grade's SALARY GRADE is not resolved here at any
// depth. Pay bands need `HR.SalaryGrades.View`, and this read does not have it.
public sealed record PositionDetail(
  Guid PositionId,
  Guid CompanyId,
  string Code,
  string Title,
  Guid? JobGradeId,
  PositionJobGradeSummary? JobGrade,
  PositionStatus Status,
  byte[] RowVersion);

// The grade block in a position representation. Code, name and rank — never the salary grade it points at,
// and never any amount.
public sealed record PositionJobGradeSummary(
  Guid JobGradeId,
  string Code,
  string Name,
  int RankOrder);

// The lighter shape for lists. The grade is an IDENTIFIER only: resolving one per row would turn a paged
// list into N grade lookups, and a search result does not need the block.
public sealed record PositionListItem(
  Guid PositionId,
  Guid CompanyId,
  string Code,
  string Title,
  Guid? JobGradeId,
  PositionStatus Status,
  byte[] RowVersion);

// The caller's search intent (FR-POS-0203). Every filter is optional; the SCOPE is not, and comes from the
// resolver.
public sealed record SearchPositionsQuery(
  PositionCompanyScopeMode CompanyScope = PositionCompanyScopeMode.CurrentCompany,
  string? SearchText = null,
  PositionStatus? Status = null,
  Guid? JobGradeId = null,
  int Page = 1,
  int PageSize = 25);

// ---- THE JOB GRADE (FR-POS-0206).
//
// It carries `SalaryGradeId` and nothing else about the salary grade: the identifier is a structural fact
// about the ladder, while the code, name and amounts behind it are the pay structure and need
// `HR.SalaryGrades.View`.
public sealed record JobGradeDetail(
  Guid JobGradeId,
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  Guid? SalaryGradeId,
  JobGradeStatus Status,
  byte[] RowVersion);

public sealed record JobGradeListItem(
  Guid JobGradeId,
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  Guid? SalaryGradeId,
  JobGradeStatus Status,
  byte[] RowVersion);

public sealed record SearchJobGradesQuery(
  PositionCompanyScopeMode CompanyScope = PositionCompanyScopeMode.CurrentCompany,
  string? SearchText = null,
  JobGradeStatus? Status = null,
  int Page = 1,
  int PageSize = 25);

// ---- THE SALARY GRADE, WHICH IS THE ONE THAT CARRIES MONEY (FR-POS-0209, OD-POS-004).
//
// The three amounts are all-or-nothing (`DEC-POS-0027`): either all three are present or all three are
// null, and the model cannot express a half-priced band because `SalaryBand` cannot. They are carried
// flattened rather than as the value object so the read model stays a transport shape.
//
// ---- THERE IS NO CURRENCY FIELD HERE, AND THE OMISSION IS DELIBERATE.
//
// `api-contracts.md` puts `currencyCode` in the WIRE representation as a projection of the owning Company's
// immutable `BaseCurrencyCode` — echoed, never stored (`DEC-POS-0015`, `ADR-027` decision 2). Reading it
// means reading a Company, and `SSAS.HR.Application` cannot reference Platform's company model under
// `ADR-012`. The echo therefore belongs to the API composition in Phase 4, where the Host can see both, and
// putting a currency here would either duplicate a fact the Company owns or invite this layer to guess it.
public sealed record SalaryGradeDetail(
  Guid SalaryGradeId,
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  decimal? MinimumAmount,
  decimal? MidpointAmount,
  decimal? MaximumAmount,
  SalaryGradeStatus Status,
  byte[] RowVersion);

public sealed record SalaryGradeListItem(
  Guid SalaryGradeId,
  Guid CompanyId,
  string Code,
  string Name,
  int RankOrder,
  decimal? MinimumAmount,
  decimal? MidpointAmount,
  decimal? MaximumAmount,
  SalaryGradeStatus Status,
  byte[] RowVersion);

public sealed record SearchSalaryGradesQuery(
  PositionCompanyScopeMode CompanyScope = PositionCompanyScopeMode.CurrentCompany,
  string? SearchText = null,
  SalaryGradeStatus? Status = null,
  int Page = 1,
  int PageSize = 25);

// THE READ SIDE'S ENTRY POINTS (FP-008 Phase 2).
//
// EVERY METHOD REQUIRES A SCOPE, AND THE SCOPE TYPE IS THE PERMISSION. There is no overload without one, no
// default, and no way to fabricate a scope meaning "everything" — a read that omitted a scope predicate is
// not something a caller can express.
//
// Three interfaces rather than one, for the same reason there are three scope types: a salary grade read
// must be unable to accept a position scope, and the compiler is what makes that true.
public interface IPositionReadService
{
  Task<Result<PositionDetail>> GetAsync(
    PositionReadScope scope, Guid positionId, CancellationToken cancellationToken = default);

  Task<Result<PagedResult<PositionListItem>>> SearchAsync(
    PositionReadScope scope, SearchPositionsQuery query, CancellationToken cancellationToken = default);
}

public interface IJobGradeReadService
{
  Task<Result<JobGradeDetail>> GetAsync(
    JobGradeReadScope scope, Guid jobGradeId, CancellationToken cancellationToken = default);

  Task<Result<PagedResult<JobGradeListItem>>> SearchAsync(
    JobGradeReadScope scope, SearchJobGradesQuery query, CancellationToken cancellationToken = default);
}

public interface ISalaryGradeReadService
{
  Task<Result<SalaryGradeDetail>> GetAsync(
    SalaryGradeReadScope scope, Guid salaryGradeId, CancellationToken cancellationToken = default);

  Task<Result<PagedResult<SalaryGradeListItem>>> SearchAsync(
    SalaryGradeReadScope scope,
    SearchSalaryGradesQuery query,
    CancellationToken cancellationToken = default);
}
