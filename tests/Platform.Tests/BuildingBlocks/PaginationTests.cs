using SSAS.BuildingBlocks.Application.Pagination;

namespace SSAS.Platform.Tests.BuildingBlocks;

public sealed class PaginationTests
{
  [Fact]
  public void Page_request_calculates_the_number_of_items_to_skip()
  {
    var request = new PageRequest(3, 25);

    Assert.Equal(50, request.Skip);
  }

  [Fact]
  public void Paged_result_calculates_the_total_number_of_pages()
  {
    var result = new PagedResult<int>([1, 2], 1, 2, 5);

    Assert.Equal(3, result.TotalPages);
  }
}
