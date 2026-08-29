using System.Text.Json.Serialization;
using SSAS.Platform.Application.Tenants;

namespace SSAS.Platform.API.Tenants;

// ==================================================================================================
// THE WIRE SHAPE OF THE TENANT REGISTRY (T-155).
// ==================================================================================================
//
// Deliberately parallel to `CompanyTransportContracts` — same request/response split, same
// `expectedRowVersion` as an opaque base64 string, same `From` projection. **Two admin resources with two
// transport idioms would be the thing nobody could later explain.**
public sealed record CreateTenantRequest(
  [property: JsonPropertyName("tenantCode")] string? TenantCode,
  [property: JsonPropertyName("tenantName")] string? TenantName);

// ---- ⚠ ACTIVATION CARRIES NO REASON, AND THAT IS THE DOMAIN'S SHAPE, NOT AN OMISSION.
//
// `ActivateTenantCommand` takes only an id and a row version. A tenant activates out of `Provisioning`
// exactly once, and the reason for it is `ProvisioningCompleted` — **a fact about what happened, not a
// choice the caller makes.** `Company` differs here (its activate DOES take a reason) because a company
// can be deactivated and activated repeatedly.
//
// **So this request has no `reasonCode` field, and sending one is refused** by the strict reader rather
// than ignored — an accepted-and-discarded field is a caller believing they set something they did not.
public sealed record ActivateTenantRequest(
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record TenantLifecycleRequest(
  [property: JsonPropertyName("reasonCode")] string? ReasonCode,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

public sealed record TenantResponse(
  Guid TenantId,
  string TenantCode,
  string TenantName,
  string Status,
  string StatusChangeReasonCode,
  DateTimeOffset StatusChangedUtc,
  string StatusChangedBy,
  DateTimeOffset CreatedUtc,
  string CreatedBy,
  DateTimeOffset? ModifiedUtc,
  string? ModifiedBy,
  string RowVersion)
{
  public static TenantResponse From(TenantDto dto, string rowVersion)
  {
    ArgumentNullException.ThrowIfNull(dto);

    return new TenantResponse(
      dto.TenantId,
      dto.TenantCode,
      dto.TenantName,

      // Enums cross the wire as names. A client reading `2` would break the day a member is inserted.
      dto.Status.ToString(),
      dto.StatusChangeReasonCode.ToString(),
      dto.StatusChangedUtc,
      dto.StatusChangedBy,
      dto.CreatedUtc,
      dto.CreatedBy,
      dto.ModifiedUtc,
      dto.ModifiedBy,
      rowVersion);
  }
}

public sealed record TenantPageResponse(
  IReadOnlyCollection<TenantResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);
