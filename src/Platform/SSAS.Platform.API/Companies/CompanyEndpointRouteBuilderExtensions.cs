using SSAS.BuildingBlocks.Api.Transport;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.API.Companies;

// Platform Company admin HTTP transport. Tenant-plane: the owning tenant is derived from the
// trusted current-tenant context inside the existing Application handlers; no route, query, or
// body accepts a caller-supplied owning-tenant field. This slice delivers only POST create.
public static class CompanyEndpointRouteBuilderExtensions
{
  private const string RoutePrefix = "/api/platform/companies";

  public static IEndpointRouteBuilder MapPlatformCompanyEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);
    var group = endpoints.MapGroup(RoutePrefix).WithTags("Platform Companies");
    group.MapPost("", CreateAsync)
      .RequirePermission(PlatformPermissionNames.ManageCompanies)
      .WithName("PlatformCompaniesCreate");
    group.MapGet("", ListAsync)
      .RequirePermission(PlatformPermissionNames.ViewCompanies)
      .WithName("PlatformCompaniesList");
    group.MapGet("/{companyId}", GetByIdAsync)
      .RequirePermission(PlatformPermissionNames.ViewCompanies)
      .WithName("PlatformCompaniesGetById");
    group.MapPut("/{companyId}", UpdateAsync)
      .RequirePermission(PlatformPermissionNames.ManageCompanies)
      .WithName("PlatformCompaniesUpdate");
    group.MapPost("/{companyId}/activate", ActivateAsync)
      .RequirePermission(PlatformPermissionNames.CompanyLifecycle)
      .WithName("PlatformCompaniesActivate");
    group.MapPost("/{companyId}/deactivate", DeactivateAsync)
      .RequirePermission(PlatformPermissionNames.CompanyLifecycle)
      .WithName("PlatformCompaniesDeactivate");
    group.MapPost("/{companyId}/archive", ArchiveAsync)
      .RequirePermission(PlatformPermissionNames.CompanyLifecycle)
      .WithName("PlatformCompaniesArchive");
    return endpoints;
  }

  private static async Task<IResult> CreateAsync(
    HttpContext context,
    CreateCompanyCommandHandler handler,
    ICompanyReadService readService,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateCompanyRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyCode"] = [JsonValueKind.String],
        ["companyName"] = [JsonValueKind.String],
        ["baseCurrencyCode"] = [JsonValueKind.String]
      },
      cancellationToken);
    if (request is null)
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await handler.HandleAsync(
      new CreateCompanyCommand(request.CompanyCode!, request.CompanyName!, request.BaseCurrencyCode!),
      cancellationToken);
    if (result.IsFailure)
    {
      return ProblemResults.Problem(context, CompanyApiErrorMapper.Map(result.Error));
    }

    var created = await readService.GetByIdAsync(result.Value, cancellationToken);
    if (created is null)
    {
      // The company was persisted but is not readable in the trusted tenant context; treat as a
      // safe internal failure rather than fabricating a projection.
      return ProblemResults.Problem(context, ProblemResults.WriteFailure);
    }

    var response = CompanyResponse.From(created, RowVersionCodec.Encode(created.RowVersion));
    return Results.Created($"{RoutePrefix}/{created.CompanyId}", response);
  }

  private static async Task<IResult> ListAsync(
    HttpContext context,
    ListCompaniesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    if (!TryListQuery(context.Request.Query, out var query))
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    var result = await handler.HandleAsync(query, cancellationToken);
    if (result.IsFailure)
    {
      return ProblemResults.Problem(context, CompanyApiErrorMapper.Map(result.Error));
    }

    var page = result.Value;
    return Results.Ok(new CompanyPageResponse(
      page.Items.Select(dto => CompanyResponse.From(dto, RowVersionCodec.Encode(dto.RowVersion))).ToArray(),
      page.PageNumber,
      page.PageSize,
      page.TotalCount,
      page.TotalPages));
  }

  private static async Task<IResult> GetByIdAsync(
    HttpContext context,
    Guid companyId,
    GetCompanyByIdQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetCompanyByIdQuery(companyId), cancellationToken);
    if (result.IsFailure)
    {
      // A cross-tenant or unknown company id is indistinguishable (Company.NotFound -> 404).
      return ProblemResults.Problem(context, CompanyApiErrorMapper.Map(result.Error));
    }

    var dto = result.Value;
    return Results.Ok(CompanyResponse.From(dto, RowVersionCodec.Encode(dto.RowVersion)));
  }

  private static bool TryListQuery(IQueryCollection values, out ListCompaniesQuery query)
  {
    query = default!;
    if (!StrictRequestReader.HasOnly(values, ["pageNumber", "pageSize", "status"]) ||
      !StrictRequestReader.TryInt(values, "pageNumber", 1, out var pageNumber) ||
      !StrictRequestReader.TryInt(values, "pageSize", 50, out var pageSize) ||
      !StrictRequestReader.TryOptional(values, "status", out var statusText) ||
      !StrictRequestReader.IsOneOf(statusText, ["Active", "Inactive", "Archived"]))
    {
      return false;
    }

    CompanyStatus? status = statusText is null ? null : Enum.Parse<CompanyStatus>(statusText);
    query = new ListCompaniesQuery(status, pageNumber, pageSize);
    return true;
  }

  private static async Task<IResult> UpdateAsync(
    HttpContext context,
    Guid companyId,
    UpdateCompanyProfileCommandHandler handler,
    ICompanyReadService readService,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateCompanyProfileRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["companyName"] = [JsonValueKind.String],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken);
    if (request is null)
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return ProblemResults.Problem(context, ProblemResults.RowVersionInvalid);
    }

    var result = await handler.HandleAsync(
      new UpdateCompanyProfileCommand(companyId, request.CompanyName!, rowVersion),
      cancellationToken);
    return result.IsFailure
      ? ProblemResults.Problem(context, CompanyApiErrorMapper.Map(result.Error))
      : await ReadBackAsync(context, readService, companyId, cancellationToken);
  }

  private static Task<IResult> ActivateAsync(HttpContext context, Guid companyId, ActivateCompanyCommandHandler handler, ICompanyReadService readService, CancellationToken cancellationToken) =>
    LifecycleAsync(context, companyId, readService,
      (id, reason, rowVersion) => handler.HandleAsync(new ActivateCompanyCommand(id, reason, rowVersion), cancellationToken), cancellationToken);

  private static Task<IResult> DeactivateAsync(HttpContext context, Guid companyId, DeactivateCompanyCommandHandler handler, ICompanyReadService readService, CancellationToken cancellationToken) =>
    LifecycleAsync(context, companyId, readService,
      (id, reason, rowVersion) => handler.HandleAsync(new DeactivateCompanyCommand(id, reason, rowVersion), cancellationToken), cancellationToken);

  private static Task<IResult> ArchiveAsync(HttpContext context, Guid companyId, ArchiveCompanyCommandHandler handler, ICompanyReadService readService, CancellationToken cancellationToken) =>
    LifecycleAsync(context, companyId, readService,
      (id, reason, rowVersion) => handler.HandleAsync(new ArchiveCompanyCommand(id, reason, rowVersion), cancellationToken), cancellationToken);

  private static async Task<IResult> LifecycleAsync(
    HttpContext context,
    Guid companyId,
    ICompanyReadService readService,
    Func<Guid, CompanyStatusChangeReason, byte[], Task<Result>> execute,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<CompanyLifecycleRequest>(
      context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["reasonCode"] = [JsonValueKind.String],
        ["expectedRowVersion"] = [JsonValueKind.String]
      },
      cancellationToken);
    if (request is null || !TryParseReason(request.ReasonCode, out var reason))
    {
      return ProblemResults.Problem(context, ProblemResults.RequestInvalid);
    }

    if (!RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion))
    {
      return ProblemResults.Problem(context, ProblemResults.RowVersionInvalid);
    }

    var result = await execute(companyId, reason, rowVersion);
    return result.IsFailure
      ? ProblemResults.Problem(context, CompanyApiErrorMapper.Map(result.Error))
      : await ReadBackAsync(context, readService, companyId, cancellationToken);
  }

  // Transport enforces the bounded, non-Created reason set (enum shape); the Domain remains
  // authoritative for whether a transition is permitted from the company's current status.
  private static bool TryParseReason(string? value, out CompanyStatusChangeReason reason)
  {
    reason = default;
    if (value is null ||
      !StrictRequestReader.IsOneOf(value, ["Administrative", "Operational", "Compliance", "CustomerRequest", "IssueResolved"]))
    {
      return false;
    }

    reason = Enum.Parse<CompanyStatusChangeReason>(value);
    return true;
  }

  private static async Task<IResult> ReadBackAsync(
    HttpContext context,
    ICompanyReadService readService,
    Guid companyId,
    CancellationToken cancellationToken)
  {
    var dto = await readService.GetByIdAsync(companyId, cancellationToken);
    return dto is null
      ? ProblemResults.Problem(context, ProblemResults.WriteFailure)
      : Results.Ok(CompanyResponse.From(dto, RowVersionCodec.Encode(dto.RowVersion)));
  }
}
