using SSAS.BuildingBlocks.Api.Transport;
using Microsoft.AspNetCore.Http;
using SSAS.Platform.API.Transport;

namespace SSAS.Platform.API.Localization;

internal static class LocalizationResponseSecurity
{
  // Delegates to the shared admin response-security helper so header values live in one place.
  public static void Apply(HttpContext context) => ApiResponseSecurity.Apply(context);
}
