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

    if (path is "/api/platform/auth/refresh" or "/api/platform/auth/logout")
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

    if (path == "/api/platform/auth/logout")
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
