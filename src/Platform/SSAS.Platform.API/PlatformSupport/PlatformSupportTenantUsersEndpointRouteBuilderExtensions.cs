using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.TenantUsers;
using SSAS.Platform.API.IdentityAccess;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Platform.API.PlatformSupport;

public static class PlatformSupportTenantUsersEndpointRouteBuilderExtensions
{
    public const string RoutePrefix = "/api/platform/support/tenants/{tenantId}/users";
    private const string Administer = PlatformPermissionNames.AdministerPlatformSupport;

    public static IEndpointRouteBuilder MapPlatformSupportTenantUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(RoutePrefix).WithTags("Platform Support Tenant Users");

        group.MapGet("", ListTenantUsersAsync)
            .RequirePlatformPermission(Administer)
            .WithName("PlatformSupportTenantUsersList");

        group.MapPost("/invitations", IssueInvitationAsync)
            .RequirePlatformPermission(Administer)
            .WithName("PlatformSupportTenantUsersIssueInvitation");

        group.MapPost("/{userId}/deactivate", DeactivateAsync)
            .RequirePlatformPermission(Administer)
            .WithName("PlatformSupportTenantUsersDeactivate");

        return endpoints;
    }

    private static async Task<IResult> ListTenantUsersAsync(
        HttpContext context,
        long tenantId,
        [FromServices] ListTenantUsersQueryHandler handler,
        CancellationToken cancellationToken)
    {
        ApiResponseSecurity.Apply(context);
        
        var query = new ListTenantUsersQuery(1, 50); // Hardcoded pagination for now
        if (StrictRequestReader.TryInt(context.Request.Query, "pageNumber", 1, out var pageNumber) &&
            StrictRequestReader.TryInt(context.Request.Query, "pageSize", 50, out var pageSize))
        {
            query = new ListTenantUsersQuery(pageNumber, pageSize);
        }

        var result = await handler.HandleAsync(query, cancellationToken);
        if (result.IsFailure) return ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error));
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> IssueInvitationAsync(
        HttpContext context,
        long tenantId,
        [FromServices] IssueTenantUserInvitationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        ApiResponseSecurity.Apply(context);
        var request = await context.Request.ReadFromJsonAsync<IssueTenantUserInvitationCommand>(cancellationToken);
        if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

        var result = await handler.HandleAsync(request, cancellationToken);
        return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.NoContent();
    }

    private static async Task<IResult> DeactivateAsync(
        HttpContext context,
        long tenantId,
        long userId,
        [FromServices] DeactivateTenantUserCommandHandler handler,
        CancellationToken cancellationToken)
    {
        ApiResponseSecurity.Apply(context);
        
        var request = await context.Request.ReadFromJsonAsync<DeactivateRequest>(cancellationToken);
        if (request is null || !RowVersionCodec.TryDecode(request.ExpectedRowVersion, out var rowVersion)) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

        var result = await handler.HandleAsync(new DeactivateTenantUserCommand(userId, rowVersion), cancellationToken);
        return result.IsFailure ? ProblemResults.Problem(context, IdentityAccessApiErrorMapper.Map(result.Error)) : Results.NoContent();
    }
    
    public record DeactivateRequest(string ExpectedRowVersion);
}
