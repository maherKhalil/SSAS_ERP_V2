using Microsoft.Extensions.Options;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Platform.Infrastructure.Identity;

public sealed class AuthenticationClientRegistry(IOptions<AuthenticationClientOptions> options) : IAuthenticationClientRegistry
{
  private readonly HashSet<string> allowedClientIds = new(options.Value.AllowedClientIds, StringComparer.Ordinal);

  public bool IsAllowed(AuthenticationClientId clientId)
  {
    ArgumentNullException.ThrowIfNull(clientId);
    return allowedClientIds.Contains(clientId.Value);
  }
}
