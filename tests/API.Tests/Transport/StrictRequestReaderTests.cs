using SSAS.BuildingBlocks.Api.Transport;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SSAS.Platform.API.Transport;

namespace SSAS.API.Tests.Transport;

public sealed class StrictRequestReaderTests
{
  private static readonly Dictionary<string, JsonValueKind[]> Fields = new()
  {
    ["name"] = [JsonValueKind.String],
    ["count"] = [JsonValueKind.Number]
  };

  [Fact]
  public async Task Reads_a_valid_object_with_only_declared_members()
  {
    var value = await ReadAsync("{\"name\":\"acme\",\"count\":3}");
    Assert.NotNull(value);
    Assert.Equal("acme", value!.Name);
    Assert.Equal(3, value.Count);
  }

  [Fact]
  public async Task Rejects_unknown_members()
  {
    Assert.Null(await ReadAsync("{\"name\":\"acme\",\"count\":3,\"extra\":true}"));
  }

  [Fact]
  public async Task Rejects_duplicate_members()
  {
    Assert.Null(await ReadAsync("{\"name\":\"acme\",\"name\":\"beta\",\"count\":3}"));
  }

  [Fact]
  public async Task Rejects_a_wrong_value_kind()
  {
    Assert.Null(await ReadAsync("{\"name\":\"acme\",\"count\":\"3\"}"));
  }

  [Fact]
  public async Task Rejects_a_missing_required_member()
  {
    Assert.Null(await ReadAsync("{\"name\":\"acme\"}"));
  }

  [Fact]
  public async Task Rejects_a_non_object_root()
  {
    Assert.Null(await ReadAsync("[]"));
  }

  [Fact]
  public async Task Rejects_malformed_json()
  {
    Assert.Null(await ReadAsync("{\"name\":"));
  }

  [Fact]
  public async Task Rejects_a_non_json_content_type()
  {
    Assert.Null(await ReadAsync("{\"name\":\"acme\",\"count\":3}", contentType: "text/plain"));
  }

  [Fact]
  public void Query_strictness_rejects_unknown_and_multi_valued_keys()
  {
    Assert.True(StrictRequestReader.HasOnly(Query(("pageNumber", "1")), ["pageNumber", "pageSize"]));
    Assert.False(StrictRequestReader.HasOnly(Query(("unknown", "1")), ["pageNumber", "pageSize"]));
    Assert.False(StrictRequestReader.HasOnly(
      new QueryCollection(new Dictionary<string, StringValues> { ["pageNumber"] = new StringValues(["1", "2"]) }),
      ["pageNumber"]));
  }

  [Fact]
  public void TryInt_defaults_when_absent_and_rejects_non_numeric()
  {
    Assert.True(StrictRequestReader.TryInt(Query(), "pageSize", 50, out var absent));
    Assert.Equal(50, absent);
    Assert.True(StrictRequestReader.TryInt(Query(("pageSize", "10")), "pageSize", 50, out var supplied));
    Assert.Equal(10, supplied);
    Assert.False(StrictRequestReader.TryInt(Query(("pageSize", "abc")), "pageSize", 50, out _));
    Assert.False(StrictRequestReader.TryInt(Query(("pageSize", "-1")), "pageSize", 50, out _));
  }

  private static Task<StrictModel?> ReadAsync(string body, string contentType = "application/json")
  {
    var context = new DefaultHttpContext();
    context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
    context.Request.ContentType = contentType;
    context.Request.ContentLength = context.Request.Body.Length;
    return StrictRequestReader.ReadStrictJsonAsync<StrictModel>(context, Fields, CancellationToken.None);
  }

  private static QueryCollection Query(params (string Key, string Value)[] pairs) =>
    new(pairs.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value)));

  // Consumer request DTOs annotate members exactly like the real transport contracts, so the
  // shared reader's default (case-sensitive) deserialization binds them.
  private sealed record StrictModel(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("count")] int Count);
}
