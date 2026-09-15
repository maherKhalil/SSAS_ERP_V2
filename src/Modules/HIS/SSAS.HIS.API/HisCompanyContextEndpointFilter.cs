using Microsoft.AspNetCore.Http;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using System;
using System.Threading.Tasks;

namespace SSAS.HIS.API;

public sealed class HisCompanyContextEndpointFilter(ICompanyContextEstablisher establisher) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var established = await establisher.EstablishAsync(context.HttpContext.RequestAborted);

        return established.IsFailure
            ? ApiProblems.Problem(context.HttpContext, HisApiErrorMapper.Map(established.Error), "his.errors.request_rejected")
            : await next(context);
    }
}
