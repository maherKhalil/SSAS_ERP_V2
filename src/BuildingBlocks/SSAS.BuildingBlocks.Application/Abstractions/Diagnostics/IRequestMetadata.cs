namespace SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;

public interface IRequestMetadata
{
  string? RequestId { get; }
}
