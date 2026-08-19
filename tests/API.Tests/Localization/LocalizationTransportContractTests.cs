using SSAS.BuildingBlocks.Api.Transport;
using System.Reflection;
using System.Text.Json;
using SSAS.Platform.API.Localization;
using SSAS.Platform.Domain;

namespace SSAS.API.Tests.Localization;

public sealed class LocalizationTransportContractTests
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  [Fact]
  public void Transport_requests_expose_no_writable_tenant_or_actor_identity()
  {
    var requests = new[]
    {
      typeof(PutLocalizationOverrideRequest), typeof(UndoLocalizationOverrideRequest),
      typeof(RestoreLocalizationOverrideDefaultRequest), typeof(PreviewLocalizationRequest),
      typeof(EffectiveLocalizationBatchRequest)
    };

    Assert.Empty(requests.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
      .Where(property => property.Name is "TenantId" or "ActorId" or "UserId"));
  }

  [Fact]
  public void Transport_requests_use_the_approved_json_property_names()
  {
    var json = JsonSerializer.Serialize(new UndoLocalizationOverrideRequest(3, "AQIDBAUGBwg="));

    Assert.Equal("{\"targetVersionNumber\":3,\"expectedRowVersion\":\"AQIDBAUGBwg=\"}", json);
  }

  [Fact]
  public void Effective_batch_transport_uses_resource_scoped_plain_string_placeholder_values()
  {
    var request = new EffectiveLocalizationBatchRequest(
      "en",
      ["platform.common.validation.required"],
      new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
      {
        ["platform.common.validation.required"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
          ["fieldName"] = "Name"
        }
      });

    var json = JsonSerializer.Serialize(request, JsonOptions);

    Assert.Equal(
      "{\"culture\":\"en\",\"resourceKeys\":[\"platform.common.validation.required\"],\"placeholderValuesByResource\":{\"platform.common.validation.required\":{\"fieldName\":\"Name\"}}}",
      json);
  }

  [Fact]
  public void Error_mapper_maps_internal_concurrency_to_http_contract()
  {
    Assert.True(LocalizationApiErrorMapper.TryMap(IdentityAccessErrors.ConcurrencyConflict.Code, out var error));
    Assert.Equal(409, error.StatusCode);
    Assert.Equal("concurrency.conflict", error.Code);
  }

  [Fact]
  public void Error_mapper_exposes_invalid_rowversion_contract()
  {
    var error = LocalizationApiErrorMapper.InvalidRowVersion;

    Assert.Equal(400, error.StatusCode);
    Assert.Equal("localization.rowversion_invalid", error.Code);
  }

  [Fact]
  public void Audit_readiness_failure_maps_to_operational_503_without_internal_detail()
  {
    Assert.True(LocalizationApiErrorMapper.TryMap(
      "localization.audit_readiness_unavailable",
      out var error));

    Assert.Equal(503, error.StatusCode);
    Assert.Equal("localization.audit_readiness_unavailable", error.Code);
    Assert.DoesNotContain("provider", error.Code, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void Administration_read_contracts_do_not_expose_tenant_or_actor_identity_outside_history()
  {
    var nonHistoryContracts = new[]
    {
      typeof(LocalizationResourceResponse), typeof(LocalizationResourcePageResponse), typeof(LocalizationResourceDetailResponse)
    };

    Assert.Empty(nonHistoryContracts.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
      .Where(property => property.Name is "TenantId" or "ActorId" or "UserId"));
    Assert.Contains(typeof(LocalizationHistoryEntryResponse).GetProperties(), property => property.Name == "ChangedBy");
  }

  [Fact]
  public void Administration_read_contract_encodes_rowversion_as_a_string()
  {
    var response = new LocalizationResourceResponse(
      "platform.common.label.save", "Platform", "Common", "Label", "PlainText", "Active", "Ordinary", true, 1, 1,
      "en", "Save", "Save", null, null, true, null, null, "AQIDBAUGBwg=", null, [], null);

    var json = JsonSerializer.Serialize(response, JsonOptions);

    Assert.Contains("\"rowVersion\":\"AQIDBAUGBwg=\"", json, StringComparison.Ordinal);
  }

  [Fact]
  public void Phase_three_mutation_and_preview_contracts_do_not_expose_trusted_identity_or_binary_rowversions()
  {
    var contracts = new[] { typeof(LocalizationMutationResponse), typeof(LocalizationPreviewResponse) };

    Assert.Empty(contracts.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
      .Where(property => property.Name is "TenantId" or "ActorId" or "UserId" || property.PropertyType == typeof(byte[])));
  }

  [Theory]
  [InlineData("localization.text_invalid", 422)]
  [InlineData("localization.placeholder_mismatch", 422)]
  [InlineData("localization.resource_retired", 422)]
  public void Phase_three_policy_errors_use_approved_unprocessable_contracts(string technicalCode, int statusCode)
  {
    Assert.True(LocalizationApiErrorMapper.TryMap(technicalCode, out var error));
    Assert.Equal(statusCode, error.StatusCode);
  }
}
