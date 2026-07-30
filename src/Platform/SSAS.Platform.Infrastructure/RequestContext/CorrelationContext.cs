using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;

namespace SSAS.Platform.Infrastructure.RequestContext;

public sealed class CorrelationContext : ICorrelationContext
{
  public string CorrelationId { get; private set; } = string.Empty;

  public void SetCorrelationId(string correlationId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

    CorrelationId = correlationId;
  }
}
