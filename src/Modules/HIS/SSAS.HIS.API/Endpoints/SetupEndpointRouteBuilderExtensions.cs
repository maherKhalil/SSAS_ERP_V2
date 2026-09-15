using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetDoctorsLookup;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetSpecialtiesLookup;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetClinicsLookup;
using SSAS.HIS.Application.Permissions;

namespace SSAS.HIS.API.Endpoints;

public static class SetupEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapHisSetupEndpoints(this IEndpointRouteBuilder builder)
    {
        var routeGroup = builder.MapGroup("api/his/setup")
            .RequireAuthorization()
            .WithTags("HIS Setup")
            .RequireModule(HisModuleEnablement.Key)
            .AddEndpointFilter<HisCompanyContextEndpointFilter>();

        routeGroup.MapGet("doctors", async (
            GetDoctorsLookupQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetDoctorsLookupQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).RequirePermission(HisPermissionNames.ViewSetup).WithName("GetDoctorsLookup");

        routeGroup.MapGet("specialties", async (
            GetSpecialtiesLookupQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetSpecialtiesLookupQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).RequirePermission(HisPermissionNames.ViewSetup).WithName("GetSpecialtiesLookup");

        routeGroup.MapGet("clinics", async (
            GetClinicsLookupQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetClinicsLookupQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).RequirePermission(HisPermissionNames.ViewSetup).WithName("GetClinicsLookup");

        return builder;
    }
}
