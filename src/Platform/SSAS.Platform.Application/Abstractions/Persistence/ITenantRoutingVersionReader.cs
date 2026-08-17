using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// The narrow authoritative read behind version-aware routing (ADR-020 "Resolver cache").
//
// DELIBERATELY NOT `FindActiveAssignmentAsync`. That read joins the registry and projects sixteen columns
// because a caller needs a whole route; this one answers a single question — "has routing moved?" — and is
// on the path of every tenant resolution. Reading the wide row just to compare one number would make the
// cache pointless.
//
// A FAILURE IS A FAILURE, NEVER A DEFAULT. There is no "assume unchanged" result: a caller that cannot
// establish the current version must refuse to route rather than serve what it remembers.
public interface ITenantRoutingVersionReader
{
  Task<Result<long>> ReadCurrentRoutingVersionAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default);
}
