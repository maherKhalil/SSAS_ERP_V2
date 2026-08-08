using Asp.Versioning;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SSAS.Host.API.Errors;
using SSAS.Host.API.Authentication;
using SSAS.Platform.Infrastructure.Localization;

namespace SSAS.Host.API.Configuration;

public static class HostServiceCollectionExtensions
{
  public static IServiceCollection AddHostApplicationOptions(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services
      .AddOptions<ApplicationOptions>()
      .BindConfiguration(ApplicationOptions.SectionName)
      .ValidateDataAnnotations()
      .ValidateOnStart();

    return services;
  }

  public static IServiceCollection AddHostProblemDetails(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
      context.ProblemDetails.Extensions[ProblemDetailsWriter.CorrelationIdExtensionName] =
        ProblemDetailsWriter.GetCorrelationId(context.HttpContext));
    services.AddExceptionHandler<GlobalExceptionHandler>();

    return services;
  }

  public static IServiceCollection AddHostApiInfrastructure(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services
      .AddApiVersioning(options =>
      {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
      })
      .AddApiExplorer(options =>
      {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
      });

    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(options =>
    {
      options.SwaggerDoc("v1", new OpenApiInfo { Title = "SSAS ERP API", Version = "v1" });
      options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
      {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "RS256 access token."
      });
      options.OperationFilter<AuthenticationOpenApiOperationFilter>();
      options.OperationFilter<LocalizationOpenApiOperationFilter>();
    });

    services
      .AddHealthChecks()
      .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"])
      .AddCheck<LocalizationManagementAuditReadinessHealthCheck>(
        "localization_management_audit_readiness",
        tags: ["ready"]);

    return services;
  }
}
