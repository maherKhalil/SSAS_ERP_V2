namespace SSAS.BuildingBlocks.Application.Pagination;

public sealed record PageRequest
{
  public PageRequest(int pageNumber, int pageSize)
  {
    if (pageNumber < 1)
    {
      throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than zero.");
    }

    if (pageSize < 1)
    {
      throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
    }

    PageNumber = pageNumber;
    PageSize = pageSize;
  }

  public int PageNumber { get; }

  public int PageSize { get; }

  public int Skip => checked((PageNumber - 1) * PageSize);
}
