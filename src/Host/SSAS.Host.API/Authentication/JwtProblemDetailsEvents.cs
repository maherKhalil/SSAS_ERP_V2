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
      OnChallenge = async context =>
      {
        if (context.Response.HasStarted)
        {
          return;
        }

        context.HandleResponse();
        context.Response.Headers[HeaderNames.WWWAuthenticate] = JwtBearerDefaults.AuthenticationScheme;
        await ProblemDetailsWriter.WriteAsync(
          context.HttpContext,
          StatusCodes.Status401Unauthorized,
          "Authentication is required.",
          "https://httpstatuses.com/401");
      },
      OnForbidden = context => ProblemDetailsWriter.WriteAsync(
        context.HttpContext,
        StatusCodes.Status403Forbidden,
        "You are not permitted to perform this action.",
        "https://httpstatuses.com/403")
    };
  }
}
