using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SSAS.Host.API.Authentication;

public static class JwtAuthenticationServiceCollectionExtensions
{
  public static IServiceCollection AddHostJwtAuthentication(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);
    ArgumentNullException.ThrowIfNull(environment);

    var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

    services.AddSingleton<IValidateOptions<JwtOptions>>(new JwtOptionsValidator(environment));
    services.AddOptions<JwtOptions>()
      .BindConfiguration(JwtOptions.SectionName)
      .ValidateOnStart();

    services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = jwtOptions.Issuer,
          ValidateAudience = true,
          ValidAudience = jwtOptions.Audience,
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
          ValidateLifetime = true,
          ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds)
        };
        options.Events = JwtProblemDetailsEvents.Create();
      });

    return services;
  }
}
