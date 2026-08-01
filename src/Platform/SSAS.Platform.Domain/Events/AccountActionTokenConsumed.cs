using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Domain.Events;

public sealed record AccountActionTokenConsumed(
  Guid EventId,
  DateTimeOffset OccurredUtc,
  Guid PublicId,
  AccountActionTokenPurpose Purpose,
  long IdentityId) : DomainEvent(EventId, OccurredUtc);
