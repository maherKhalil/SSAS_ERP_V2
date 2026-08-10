using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using SSAS.Platform.API.Companies;
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
    else if (path == "/api/platform/companies" && context.ApiDescription.HttpMethod == "POST")
    {
      operation.Security.Add(BearerRequirement());
      operation.Description ??= "Requires bearer authentication, a trusted current tenant, and Platform.Companies.Manage.";
      SetStrictRequestSchema<CreateCompanyRequest>(operation, context);
      SetResponseSchema<CompanyResponse>(operation, context, "201", "The company was created (Inactive).");
      EnsureProblemResponse(operation, context, "400", "The request body was malformed or contained unknown fields.");
      EnsureProblemResponse(operation, context, "401", "Authentication is required.");
      EnsureProblemResponse(operation, context, "403", "A trusted active tenant and the required permission are required.");
      EnsureProblemResponse(operation, context, "409", "The normalized company code already exists within the tenant.");
      EnsureProblemResponse(operation, context, "500", "The company could not be persisted.");
    }
    else if (path == "/api/platform/companies" && context.ApiDescription.HttpMethod == "GET")
    {
      operation.Security.Add(BearerRequirement());
      operation.Description ??= "Requires bearer authentication, a trusted current tenant, and Platform.Companies.View.";
      AddQueryParameter(operation, "pageNumber", "One-based page number.", minimum: 1);
      AddQueryParameter(operation, "pageSize", "Page size; minimum 1, maximum 200.", minimum: 1, maximum: 200);
      AddEnumQueryParameter(operation, "status", "Optional lifecycle status filter.", ["Active", "Inactive", "Archived"]);
      SetSuccessSchema<CompanyPageResponse>(operation, context);
      EnsureProblemResponse(operation, context, "400", "The paging values or status filter were invalid.");
      EnsureProblemResponse(operation, context, "401", "Authentication is required.");
      EnsureProblemResponse(operation, context, "403", "A trusted active tenant and the required permission are required.");
    }
    else if (path == "/api/platform/companies/{companyId}" && context.ApiDescription.HttpMethod == "GET")
    {
      operation.Security.Add(BearerRequirement());
      operation.Description ??= "Requires bearer authentication, a trusted current tenant, and Platform.Companies.View.";
      SetSuccessSchema<CompanyResponse>(operation, context);
      EnsureProblemResponse(operation, context, "401", "Authentication is required.");
      EnsureProblemResponse(operation, context, "403", "A trusted active tenant and the required permission are required.");
      EnsureProblemResponse(operation, context, "404", "The company is unknown to the current tenant.");
    }
  }

  private static void AddEnumQueryParameter(OpenApiOperation operation, string name, string description, IReadOnlyCollection<string> allowed)
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
      Schema = new OpenApiSchema { Type = "string", Enum = allowed.Select(value => (IOpenApiAny)new OpenApiString(value)).ToList() }
    });
  }

  private static void SetStrictRequestSchema<T>(OpenApiOperation operation, OperationFilterContext context)
  {
    var schema = context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository);
    operation.RequestBody = new OpenApiRequestBody
    {
      Required = true,
      Content = { ["application/json"] = new OpenApiMediaType { Schema = schema } }
    };

    if (context.SchemaRepository.Schemas.TryGetValue(typeof(T).Name, out var definition))
    {
      definition.AdditionalPropertiesAllowed = false;
      foreach (var propertyName in definition.Properties.Keys)
      {
        definition.Required.Add(propertyName);
      }
    }
  }

  private static void SetResponseSchema<T>(OpenApiOperation operation, OperationFilterContext context, string status, string description)
  {
    var response = EnsureResponse(operation, status, description);
    response.Content["application/json"] = new OpenApiMediaType
    {
      Schema = context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository)
    };
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
