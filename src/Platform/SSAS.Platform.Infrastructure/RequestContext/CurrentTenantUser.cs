using SSAS.BuildingBlocks.Tenancy;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Platform.Infrastructure.RequestContext;

// The module-facing view of the acting tenant user (FP-006C3, ADR-012).
//
// It reads the SAME durable authentication session everything else does, so a module and Platform can never
// disagree about who is acting. It exposes only the identifier: roles, permissions, claims and the session
// itself stay behind Platform's own contracts.
//
// THE SESSION IS OPTIONAL, AND ITS ABSENCE IS A NULL RATHER THAN A THROW. Background and maintenance
// compositions legitimately have none, and the boundary that requires an acting user is the one that
// refuses — not this accessor.
public sealed class CurrentTenantUser(ICurrentAuthenticationSession? currentSession = null) : ICurrentTenantUser
{
  public long? TenantUserId =>
    currentSession?.Value is { TenantUserId: > 0 } session ? session.TenantUserId : null;
}
