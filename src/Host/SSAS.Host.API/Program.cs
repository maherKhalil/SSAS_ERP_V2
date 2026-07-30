using Asp.Versioning;
using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;
using SSAS.GL.API;
using SSAS.Host.API.Configuration;
using SSAS.HR.API;
using SSAS.Platform.API;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
  .CreateBootstrapLogger();

try
{
  var builder = WebApplication.CreateBuilder(args);

  builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

  builder.Services
    .AddOptions<ApplicationOptions>()
    .BindConfiguration(ApplicationOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

  builder.Services
    .AddPlatformModule()
    .AddHrModule()
    .AddGlModule();

  builder.Services.AddProblemDetails(options =>
  {
    options.CustomizeProblemDetails = context =>
      context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
  });

  builder.Services
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

  builder.Services.AddEndpointsApiExplorer();
  builder.Services.AddSwaggerGen(options =>
  {
    options.SwaggerDoc("v1", new OpenApiInfo
    {
      Title = "SSAS ERP API",
      Version = "v1"
    });
  });

  builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"]);

  var app = builder.Build();

  app.UseSerilogRequestLogging();
  app.UseExceptionHandler();
  app.UseHttpsRedirection();
  app.UseSwagger();
  app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "SSAS ERP API v1"));

  app.MapGet("/", (IOptions<ApplicationOptions> options, IHostEnvironment environment) => Results.Ok(new
    {
      application = options.Value.Name,
      version = options.Value.Version,
      environment = environment.EnvironmentName
    }))
    .WithName("ApplicationInformation");

  app.MapHealthChecks("/health");
  app.MapHealthChecks("/health/live", new() { Predicate = registration => registration.Tags.Contains("live") });
  app.MapHealthChecks("/health/ready", new() { Predicate = registration => registration.Tags.Contains("ready") });

  app.Run();
}
catch (Exception exception)
{
  Log.Fatal(exception, "Host terminated unexpectedly");
  throw;
}
finally
{
  Log.CloseAndFlush();
}
