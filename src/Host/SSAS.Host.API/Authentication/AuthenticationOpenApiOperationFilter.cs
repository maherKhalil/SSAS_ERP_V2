using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using SSAS.Platform.API.Authentication;

namespace SSAS.Host.API.Authentication;

public sealed class AuthenticationOpenApiOperationFilter : IOperationFilter
{
  public void Apply(OpenApiOperation operation, OperationFilterContext context)
  {
    var path = "/" + context.ApiDescription.RelativePath?.TrimStart('/');
    if (path == "/api/platform/auth/login" && operation.Responses.TryGetValue("200", out var success) &&
      success.Content.TryGetValue("application/json", out var mediaType))
    {
      mediaType.Schema = new OpenApiSchema
      {
        OneOf =
        [
          context.SchemaGenerator.GenerateSchema(typeof(AuthenticatedResponse), context.SchemaRepository),
          context.SchemaGenerator.GenerateSchema(typeof(TenantSelectionRequiredResponse), context.SchemaRepository)
        ]
      };
    }

    // Platform-support login (Phase 4B) returns a single non-tenant success shape — no tenant-selection oneOf.
    if (path == "/api/platform/support/auth/login" && operation.Responses.TryGetValue("200", out var platformSuccess) &&
      platformSuccess.Content.TryGetValue("application/json", out var platformMediaType))
    {
      platformMediaType.Schema = context.SchemaGenerator.GenerateSchema(typeof(PlatformAuthenticatedResponse), context.SchemaRepository);
    }

    if (path is "/api/platform/auth/refresh" or "/api/platform/auth/logout"
      or "/api/platform/support/auth/refresh" or "/api/platform/support/auth/logout")
    {
      operation.Parameters.Add(new OpenApiParameter
      {
        Name = "X-XSRF-TOKEN",
        In = ParameterLocation.Header,
        Required = true,
        Description = "Must exactly match the protected __Secure-ssas-xsrf cookie.",
        Schema = new OpenApiSchema { Type = "string" }
      });
    }

    if (path is "/api/platform/auth/logout" or "/api/platform/support/auth/logout")
    {
      operation.Security.Add(new OpenApiSecurityRequirement
      {
        [new OpenApiSecurityScheme
        {
          Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
      });
    }
  }
}
