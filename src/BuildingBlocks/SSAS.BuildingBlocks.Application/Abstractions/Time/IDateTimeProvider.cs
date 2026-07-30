namespace SSAS.BuildingBlocks.Application.Abstractions.Time;

public interface IDateTimeProvider
{
  DateTimeOffset UtcNow { get; }
}
