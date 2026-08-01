using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Net.Http.Headers;
using SSAS.Host.API.Errors;

namespace SSAS.Host.API.Authentication;

public static class JwtProblemDetailsEvents
{
  public static JwtBearerEvents Create()
  {
    return new JwtBearerEvents
    {
      OnTokenValidated = StrictAccessTokenValidator.ValidateAsync,
      OnChallenge = async context =>
      {
        if (context.Response.HasStarted)
        {
          return;
        }

        context.HandleResponse();
        ApplyAuthenticationResponseSecurity(context.Response);
        context.Response.Headers[HeaderNames.WWWAuthenticate] = JwtBearerDefaults.AuthenticationScheme;
        await ProblemDetailsWriter.WriteAsync(
          context.HttpContext,
          StatusCodes.Status401Unauthorized,
          "Authentication is required.",
          "https://httpstatuses.com/401",
          "authentication.failed");
      },
      OnForbidden = context =>
      {
        ApplyAuthenticationResponseSecurity(context.Response);
        return ProblemDetailsWriter.WriteAsync(
          context.HttpContext,
          StatusCodes.Status403Forbidden,
          "You are not permitted to perform this action.",
          "https://httpstatuses.com/403",
          "authorization.forbidden");
      }
    };
  }

  private static void ApplyAuthenticationResponseSecurity(HttpResponse response)
  {
    response.Headers.CacheControl = "no-store";
    response.Headers.Pragma = "no-cache";
    response.Headers["Referrer-Policy"] = "no-referrer";
    response.Headers["X-Content-Type-Options"] = "nosniff";
  }
}
