namespace SSAS.BuildingBlocks.Application.Abstractions.Persistence;

public sealed record DomainEventDispatchMetadata(
  string CorrelationId,
  string? ActorId,
  string? RequestId,
  string? TraceId);
