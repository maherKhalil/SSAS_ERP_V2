using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.HIS.Application.Registration.Patients.Commands.RegisterPatient;
using SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;
using SSAS.HIS.Application.Registration.Patients.Queries.SearchPatients;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.HIS.Application.Permissions;

namespace SSAS.HIS.API.Endpoints;

public static class RegistrationEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapHisRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/his/patients")
            .RequireAuthorization()
            .WithTags("HIS Registration");

        group.MapPost("/", async (
            RegisterPatientCommand command,
            RegisterPatientCommandHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .RequirePermission(HisPermissionNames.ManageRegistration)
        .WithName("RegisterPatient");

        group.MapGet("/{id:guid}", async (
            Guid id,
            GetPatientByIdQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetPatientByIdQuery(id), cancellationToken);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .RequirePermission(HisPermissionNames.ManageRegistration)
        .WithName("GetPatientById");

        group.MapGet("/search", async (
            string? term,
            SearchPatientsQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new SearchPatientsQuery(term), cancellationToken);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.BadRequest(result.Error);
        })
        .RequirePermission(HisPermissionNames.ManageRegistration)
        .WithName("SearchPatients");

        return endpoints;
    }
}
