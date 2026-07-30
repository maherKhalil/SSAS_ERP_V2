using Serilog;

namespace SSAS.Host.API.Configuration;

public static class HostSerilogExtensions
{
  public static WebApplicationBuilder ConfigureHostSerilog(this WebApplicationBuilder builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
      .ReadFrom.Configuration(context.Configuration)
      .ReadFrom.Services(services)
      .Enrich.FromLogContext()
      .Enrich.WithProperty("Application", context.Configuration["Application:Name"] ?? context.HostingEnvironment.ApplicationName)
      .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName));

    return builder;
  }
}
