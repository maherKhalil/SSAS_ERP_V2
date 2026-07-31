using Microsoft.AspNetCore.Http;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;

namespace SSAS.Platform.Infrastructure.RequestContext;

public sealed class RequestMetadata(IHttpContextAccessor httpContextAccessor) : IRequestMetadata
{
  public string? RequestId => httpContextAccessor.HttpContext?.TraceIdentifier;
}
