using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MediatR;
using SSAS.HIS.Application;
using SSAS.HIS.Application.OutPatient;
using Microsoft.AspNetCore.Mvc;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.HIS.Application.Permissions;

namespace SSAS.HIS.API.Endpoints;

public static class OutPatientEndpoints
{
    public static void MapOutPatientEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("api/his/outpatient")
            .RequireAuthorization()
            .WithTags("HIS OutPatient")
            .RequireModule(HisModuleEnablement.Key)
            .AddEndpointFilter<HisCompanyContextEndpointFilter>();

        group.MapGet("clinics/{clinicId}/schedules", async ([FromRoute] int clinicId, IHisReadService readService, CancellationToken cancellationToken) =>
        {
            var result = await readService.GetClinicSchedulesAsync(clinicId, cancellationToken);
            return Results.Ok(result);
        }).RequirePermission(HisPermissionNames.ManageOutpatient);

        group.MapPost("clinics/schedules", async ([FromBody] CreateClinicScheduleCommand command, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        }).RequirePermission(HisPermissionNames.ManageOutpatient);
    }
}
