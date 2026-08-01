using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Host.API.Authentication;

public static class JwtAuthenticationServiceCollectionExtensions
{
  public static IServiceCollection AddHostJwtAuthentication(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
  {
    ArgumentNullException.ThrowIfNull(services);
    var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

    services.AddSingleton<IValidateOptions<JwtOptions>>(new JwtOptionsValidator(environment));
    services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateOnStart();
    services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
    services.AddHostedService<SigningKeyStartupValidator>();
    services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
    services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
      .Configure<ISigningKeyProvider>((options, keyProvider) =>
      {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = jwtOptions.Issuer,
          ValidateAudience = true,
          ValidAudience = jwtOptions.Audience,
          ValidateIssuerSigningKey = true,
          RequireSignedTokens = true,
          RequireExpirationTime = true,
          ValidateLifetime = true,
          ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
          ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
          IssuerSigningKeyResolver = (_, _, kid, _) =>
            !string.IsNullOrWhiteSpace(kid) && keyProvider.Snapshot.EnabledVerificationKeys.TryGetValue(kid, out var key)
              ? [key]
              : []
        };
        options.Events = JwtProblemDetailsEvents.Create();
      });

    return services;
  }
}
