using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using SSAS.Platform.API.IdentityAccess;

namespace SSAS.Host.API.Authentication;

// Shared OpenAPI convention for Platform admin routes (bearer security + ProblemDetails
// responses). Feature-specific request/response schemas are declared per route below. This is a
// deliberately narrow convention, reused as further admin route families land.
public sealed class PlatformAdminOpenApiOperationFilter : IOperationFilter
{
  public void Apply(OpenApiOperation operation, OperationFilterContext context)
  {
    var path = ("/" + context.ApiDescription.RelativePath?.TrimStart('/')).TrimEnd('/');

    if (path == "/api/platform/roles")
    {
      operation.Security.Add(BearerRequirement());
      operation.Description ??= "Requires bearer authentication, a trusted current tenant, and Platform.Roles.View.";
      AddQueryParameter(operation, "pageNumber", "One-based page number.", minimum: 1);
      AddQueryParameter(operation, "pageSize", "Page size; minimum 1, maximum 100.", minimum: 1, maximum: 100);
      SetSuccessSchema<RolePageResponse>(operation, context);
      EnsureProblemResponse(operation, context, "400", "The request or paging values were invalid.");
      EnsureProblemResponse(operation, context, "401", "Authentication is required.");
      EnsureProblemResponse(operation, context, "403", "A trusted active tenant and the required permission are required.");
    }
  }

  private static void AddQueryParameter(OpenApiOperation operation, string name, string description, int? minimum = null, int? maximum = null)
  {
    if (operation.Parameters.Any(parameter => parameter.Name == name && parameter.In == ParameterLocation.Query))
    {
      return;
    }

    operation.Parameters.Add(new OpenApiParameter
    {
      Name = name,
      In = ParameterLocation.Query,
      Required = false,
      Description = description,
      Schema = new OpenApiSchema { Type = "integer", Minimum = minimum, Maximum = maximum }
    });
  }

  private static void SetSuccessSchema<T>(OpenApiOperation operation, OperationFilterContext context)
  {
    var response = EnsureResponse(operation, "200", "Successful response.");
    response.Content["application/json"] = new OpenApiMediaType
    {
      Schema = context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository)
    };
  }

  private static void EnsureProblemResponse(OpenApiOperation operation, OperationFilterContext context, string status, string description)
  {
    var response = EnsureResponse(operation, status, description);
    response.Content["application/problem+json"] = new OpenApiMediaType
    {
      Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository)
    };
  }

  private static OpenApiResponse EnsureResponse(OpenApiOperation operation, string status, string description)
  {
    if (!operation.Responses.TryGetValue(status, out var response))
    {
      response = new OpenApiResponse { Description = description };
      operation.Responses.Add(status, response);
    }

    return response;
  }

  private static OpenApiSecurityRequirement BearerRequirement() => new()
  {
    [new OpenApiSecurityScheme
    {
      Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    }] = []
  };
}
