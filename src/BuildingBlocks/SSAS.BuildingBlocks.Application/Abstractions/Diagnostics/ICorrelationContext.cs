namespace SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;

public interface ICorrelationContext
{
  string CorrelationId { get; }
}
