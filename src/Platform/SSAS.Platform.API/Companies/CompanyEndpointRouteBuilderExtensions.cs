using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Application.Permissions;

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
    return endpoints;
  }

  private static async Task<IResult> CreateAsync(
    HttpContext context,
    CreateCompanyCommandHandler handler,
    ICompanyReadService readService,
    CancellationToken cancellationToken)
  {
    AdminResponseSecurity.Apply(context);
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
}
