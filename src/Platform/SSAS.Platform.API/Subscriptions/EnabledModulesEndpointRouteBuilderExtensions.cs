using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Platform.Application.Subscriptions.EnabledModules;

namespace SSAS.Platform.API.Subscriptions;

public static class EnabledModulesEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPlatformEnabledModulesEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/platform/modules")
            .RequireAuthorization();

        group.MapGet("enabled", async (
            IHttpContextAccessor httpContextAccessor,
            GetEnabledModulesQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetEnabledModulesQuery();
            var result = await handler.Handle(query, cancellationToken);
            
            if (result.IsFailure)
            {
                return Results.Problem(
                    detail: result.Error.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Ok(result.Value);
        });

        return builder;
    }
}
