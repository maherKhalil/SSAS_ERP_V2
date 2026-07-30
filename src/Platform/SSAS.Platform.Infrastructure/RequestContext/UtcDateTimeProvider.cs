using SSAS.BuildingBlocks.Application.Abstractions.Time;

namespace SSAS.Platform.Infrastructure.RequestContext;

public sealed class UtcDateTimeProvider : IDateTimeProvider
{
  public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
