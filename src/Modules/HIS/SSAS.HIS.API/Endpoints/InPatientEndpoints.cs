using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MediatR;
using SSAS.HIS.Application;
using SSAS.HIS.Application.InPatient;
using Microsoft.AspNetCore.Mvc;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.HIS.Application.Permissions;

namespace SSAS.HIS.API.Endpoints;

public static class InPatientEndpoints
{
    public static void MapInPatientEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("api/his/inpatient")
            .RequireAuthorization()
            .WithTags("HIS InPatient")
            .RequireModule(HisModuleEnablement.Key)
            .AddEndpointFilter<HisCompanyContextEndpointFilter>();

        group.MapGet("admissions", async (IHisReadService readService, CancellationToken cancellationToken) =>
        {
            var result = await readService.GetAdmittedPatientsAsync(cancellationToken);
            return Results.Ok(result);
        }).RequirePermission(HisPermissionNames.ManageInpatient);

        group.MapPost("admissions", async ([FromBody] AdmitPatientCommand command, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        }).RequirePermission(HisPermissionNames.ManageInpatient);

        group.MapGet("beds", async (IHisReadService readService, CancellationToken cancellationToken) =>
        {
            var result = await readService.GetBedsAsync(cancellationToken);
            return Results.Ok(result);
        }).RequirePermission(HisPermissionNames.ManageInpatient);

        group.MapPost("beds/assign", async ([FromBody] AssignBedCommand command, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        }).RequirePermission(HisPermissionNames.ManageInpatient);

        group.MapGet("patients/{patientId}/observations", async ([FromRoute] int patientId, IHisReadService readService, CancellationToken cancellationToken) =>
        {
            var result = await readService.GetMedicalObservationsAsync(patientId, cancellationToken);
            return Results.Ok(result);
        }).RequirePermission(HisPermissionNames.ManageInpatient);

        group.MapPost("observations", async ([FromBody] CreateMedicalObservationCommand command, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(result);
        }).RequirePermission(HisPermissionNames.ManageInpatient);
    }
}
