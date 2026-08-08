using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;
using SSAS.Platform.API.Localization;
using SSAS.Platform.Application.Localization;

namespace SSAS.Host.API.Authentication;

public sealed class LocalizationOpenApiOperationFilter : IOperationFilter
{
  private const string Prefix = "/api/platform/localization";
  private const string RowVersionDescription = "Canonical padded RFC 4648 Base64 SQL rowversion.";

  public void Apply(OpenApiOperation operation, OperationFilterContext context)
  {
    var path = ("/" + context.ApiDescription.RelativePath?.TrimStart('/')).TrimEnd('/');
    if (!path.StartsWith(Prefix, StringComparison.Ordinal))
    {
      return;
    }

    operation.Security.Add(BearerRequirement());
    operation.Description ??= AuthorizationDescription(path);
    AddProblemResponses(operation, path);
    AddProblemSchemas(operation, context);
    ConfigureCultureParameters(operation);

    switch (path)
    {
      case "/api/platform/localization/effective":
        AddQueryParameter(operation, "culture", "Requested culture. Supported values: en, ar.", required: true, culture: true);
        AddQueryParameter(operation, "module", "Exact bounded localization module identity.", required: true);
        AddQueryParameter(operation, "group", "Exact bounded localization group identity; responses contain at most 250 active resources.", required: true);
        SetSuccessSchema<EffectiveLocalizationResponse>(operation, context);
        break;
      case "/api/platform/localization/effective/batch":
        operation.Description = "Resolves at most 100 unique resource keys for the trusted active Tenant.";
        SetRequestSchema<EffectiveLocalizationBatchRequest>(operation, context);
        SetSuccessSchema<EffectiveLocalizationResponse>(operation, context);
        break;
      case "/api/platform/localization/preview":
        SetRequestSchema<PreviewLocalizationRequest>(operation, context);
        SetSuccessSchema<LocalizationPreviewResponse>(operation, context);
        break;
      case "/api/platform/localization/resources":
        AddQueryParameter(operation, "culture", "Requested culture.", required: true, culture: true);
        AddQueryParameter(operation, "pageNumber", "One-based page number.", required: false, type: "integer", minimum: 1);
        AddQueryParameter(operation, "pageSize", "Page size; maximum 100.", required: false, maximum: 100);
        AddQueryParameter(operation, "search", "Optional ResourceKey search.", required: false);
        AddQueryParameter(operation, "module", "Optional exact module filter.", required: false);
        AddQueryParameter(operation, "group", "Optional exact group filter.", required: false);
        AddQueryParameter(operation, "category", "Optional category filter.", required: false,
          allowed: ["Action", "Label", "Validation", "Message", "Help"]);
        AddQueryParameter(operation, "lifecycle", "Lifecycle filter; defaults to Active.", required: false,
          allowed: ["Active", "Retired", "All"]);
        AddQueryParameter(operation, "overriddenOnly", "Return only resources with retained Tenant overrides.", required: false, type: "boolean");
        AddQueryParameter(operation, "incompatibleOnly", "Return only incompatible Tenant overrides.", required: false, type: "boolean");
        AddQueryParameter(operation, "securityClassification", "Optional security-classification filter.", required: false,
          allowed: ["Ordinary", "SecuritySensitiveNonOverridable"]);
        SetSuccessSchema<LocalizationResourcePageResponse>(operation, context);
        break;
      default:
        ConfigureResourceOperation(operation, context, path);
        break;
    }

    ConfigureSchemas(context.SchemaRepository.Schemas);
    ConfigureProblemDetailsSchema(context.SchemaRepository.Schemas);
  }

  private static void ConfigureResourceOperation(OpenApiOperation operation, OperationFilterContext context, string path)
  {
    if (path.EndsWith("/history", StringComparison.Ordinal))
    {
      AddQueryParameter(operation, "culture", "Requested culture.", required: true, culture: true);
      AddQueryParameter(operation, "pageNumber", "One-based page number.", required: false, type: "integer", minimum: 1);
      AddQueryParameter(operation, "pageSize", "Page size; maximum 100.", required: false, maximum: 100);
      SetSuccessSchema<LocalizationHistoryResponse>(operation, context);
    }
    else if (path.EndsWith("/undo", StringComparison.Ordinal))
    {
      SetRequestSchema<UndoLocalizationOverrideRequest>(operation, context);
      SetSuccessSchema<LocalizationMutationResponse>(operation, context);
    }
    else if (path.EndsWith("/restore-default", StringComparison.Ordinal))
    {
      SetRequestSchema<RestoreLocalizationOverrideDefaultRequest>(operation, context);
      SetSuccessSchema<LocalizationMutationResponse>(operation, context);
    }
    else if (path.Contains("/overrides/", StringComparison.Ordinal))
    {
      SetRequestSchema<PutLocalizationOverrideRequest>(operation, context);
      SetSuccessSchema<LocalizationMutationResponse>(operation, context);
      EnsureResponse(operation, "201", "Created.");
    }
    else
    {
      AddQueryParameter(operation, "culture", "Requested culture.", required: true, culture: true);
      SetSuccessSchema<LocalizationResourceDetailResponse>(operation, context);
    }
  }

  private static void AddProblemResponses(OpenApiOperation operation, string path)
  {
    EnsureResponse(operation, "400", "Invalid request.");
    EnsureResponse(operation, "401", "Authentication is required.");
    EnsureResponse(operation, "403", "A trusted active Tenant and required authorization are required.");
    if (!path.EndsWith("/effective", StringComparison.Ordinal) &&
      !path.EndsWith("/effective/batch", StringComparison.Ordinal) &&
      !path.EndsWith("/resources", StringComparison.Ordinal))
    {
      EnsureResponse(operation, "404", "The requested localization resource was not found.");
    }

    if (path.Contains("/overrides/", StringComparison.Ordinal))
    {
      EnsureResponse(operation, "409", "A concurrency or localization-state conflict occurred.");
      EnsureResponse(operation, "422", "The localization policy rejected the request.");
      EnsureResponse(operation, "503", "Localization management audit readiness is unavailable.");
    }
    else if (path.EndsWith("/preview", StringComparison.Ordinal))
    {
      EnsureResponse(operation, "422", "The localization policy rejected the preview.");
    }
  }

  private static string AuthorizationDescription(string path) => path switch
  {
    "/api/platform/localization/effective" or "/api/platform/localization/effective/batch" =>
      "Requires bearer authentication, a trusted current Tenant, and live Active eligibility. No localization administrative permission is required.",
    "/api/platform/localization/resources/{resourceKey}/history" =>
      "Requires Platform.Localization.ViewHistory and a trusted live Tenant.",
    "/api/platform/localization/preview" =>
      "Requires Platform.Localization.Manage and a trusted live Tenant.",
    var value when value.Contains("/overrides/", StringComparison.Ordinal) =>
      "Requires Platform.Localization.Manage and a trusted live Tenant.",
    _ => "Requires Platform.Localization.View and a trusted live Tenant."
  };

  private static void AddProblemSchemas(OpenApiOperation operation, OperationFilterContext context)
  {
    foreach (var (status, response) in operation.Responses.Where(pair => pair.Key is not "200" and not "201"))
    {
      response.Content["application/problem+json"] = new OpenApiMediaType
      {
        Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository)
      };
    }
  }

  private static void ConfigureCultureParameters(OpenApiOperation operation)
  {
    foreach (var parameter in operation.Parameters.Where(parameter => parameter.Name == "culture"))
    {
      parameter.Schema = CultureSchema();
    }
  }

  private static void AddQueryParameter(
    OpenApiOperation operation,
    string name,
    string description,
    bool required,
    bool culture = false,
    int? maximum = null,
    int? minimum = null,
    string type = "string",
    IReadOnlyCollection<string>? allowed = null)
  {
    if (operation.Parameters.Any(parameter => parameter.Name == name && parameter.In == ParameterLocation.Query))
    {
      return;
    }

    operation.Parameters.Add(new OpenApiParameter
    {
      Name = name,
      In = ParameterLocation.Query,
      Required = required,
      Description = description,
      Schema = culture
        ? CultureSchema()
        : new OpenApiSchema
        {
          Type = maximum is null ? type : "integer",
          Maximum = maximum,
          Minimum = minimum,
          Enum = allowed?.Select(value => (IOpenApiAny)new OpenApiString(value)).ToList() ?? []
        }
    });
  }

  private static void SetRequestSchema<T>(OpenApiOperation operation, OperationFilterContext context)
  {
    operation.RequestBody = new OpenApiRequestBody
    {
      Required = true,
      Content =
      {
        ["application/json"] = new OpenApiMediaType
        {
          Schema = context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository)
        }
      }
    };
  }

  private static void SetSuccessSchema<T>(OpenApiOperation operation, OperationFilterContext context)
  {
    var success = EnsureResponse(operation, "200", "Successful response.");
    success.Content["application/json"] = new OpenApiMediaType
    {
      Schema = context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository)
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

  private static OpenApiSchema CultureSchema() => new()
  {
    Type = "string",
    Enum = [new OpenApiString("en"), new OpenApiString("ar")]
  };

  private static void ConfigureSchemas(IDictionary<string, OpenApiSchema> schemas)
  {
    foreach (var (name, schema) in schemas.Where(pair => pair.Key.Contains("Localization", StringComparison.Ordinal)))
    {
      if (name is nameof(PutLocalizationOverrideRequest)
        or nameof(UndoLocalizationOverrideRequest)
        or nameof(RestoreLocalizationOverrideDefaultRequest)
        or nameof(PreviewLocalizationRequest)
        or nameof(EffectiveLocalizationBatchRequest))
      {
        schema.AdditionalPropertiesAllowed = false;
        foreach (var propertyName in schema.Properties.Keys)
        {
          schema.Required.Add(propertyName);
        }
      }

      foreach (var (propertyName, property) in schema.Properties)
      {
        if (propertyName is "culture" or "requestedCulture" or "resolvedCulture")
        {
          property.Type = "string";
          property.Enum = [new OpenApiString("en"), new OpenApiString("ar")];
        }

        if (propertyName == "expectedRowVersion" || propertyName == "rowVersion")
        {
          property.Type = "string";
          property.Description = RowVersionDescription;
        }

        if (propertyName == "resourceKeys")
        {
          property.MaxItems = LocalizationTextResolver.MaximumExplicitBatchSize;
          property.UniqueItems = true;
        }

        if (name == nameof(EffectiveLocalizationResponse) && propertyName == "items")
        {
          property.MaxItems = LocalizationTextResolver.MaximumGroupBatchSize;
        }
      }
    }
  }

  private static void ConfigureProblemDetailsSchema(Dictionary<string, OpenApiSchema> schemas)
  {
    if (!schemas.TryGetValue(nameof(ProblemDetails), out var schema))
    {
      return;
    }

    schema.Properties["code"] = new OpenApiSchema { Type = "string" };
    schema.Properties["correlationId"] = new OpenApiSchema { Type = "string" };
    schema.Properties["resourceKey"] = new OpenApiSchema { Type = "string" };
    foreach (var propertyName in new[] { "type", "status", "code", "correlationId", "resourceKey" })
    {
      schema.Required.Add(propertyName);
    }
  }
}
