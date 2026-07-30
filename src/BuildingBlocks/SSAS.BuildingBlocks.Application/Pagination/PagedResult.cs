namespace SSAS.BuildingBlocks.Application.Pagination;

public sealed class PagedResult<T>
{
  public PagedResult(IReadOnlyCollection<T> items, int pageNumber, int pageSize, int totalCount)
  {
    ArgumentNullException.ThrowIfNull(items);

    if (pageNumber < 1)
    {
      throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than zero.");
    }

    if (pageSize < 1)
    {
      throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
    }

    if (totalCount < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(totalCount), "Total count cannot be negative.");
    }

    Items = items.ToArray();
    PageNumber = pageNumber;
    PageSize = pageSize;
    TotalCount = totalCount;
  }

  public IReadOnlyCollection<T> Items { get; }

  public int PageNumber { get; }

  public int PageSize { get; }

  public int TotalCount { get; }

  public int TotalPages => TotalCount == 0
    ? 0
    : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
