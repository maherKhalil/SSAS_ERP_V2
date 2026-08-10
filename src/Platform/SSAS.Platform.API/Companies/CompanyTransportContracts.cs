using System.Text.Json.Serialization;
using SSAS.Platform.Application.Companies;

namespace SSAS.Platform.API.Companies;

// Transport-only Company contracts. The request never carries a writable owning-tenant field,
// company id, status, normalized code, rowversion, or audit metadata; the owning tenant is the
// trusted current-tenant context. Every request member declares an explicit [JsonPropertyName]
// that exactly matches the strict-reader allow-map keys, so the shared reader's case-sensitive
// deserialization binds them.
public sealed record CreateCompanyRequest(
  [property: JsonPropertyName("companyCode")] string? CompanyCode,
  [property: JsonPropertyName("companyName")] string? CompanyName,
  [property: JsonPropertyName("baseCurrencyCode")] string? BaseCurrencyCode);

// Profile update accepts only the mutable display name and the concurrency version. Company code,
// base currency, tenant, status, and identity are never accepted (unknown fields -> 400).
public sealed record UpdateCompanyProfileRequest(
  [property: JsonPropertyName("companyName")] string? CompanyName,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// Lifecycle transitions accept only a bounded non-Created reason code and the concurrency version.
public sealed record CompanyLifecycleRequest(
  [property: JsonPropertyName("reasonCode")] string? ReasonCode,
  [property: JsonPropertyName("expectedRowVersion")] string? ExpectedRowVersion);

// Safe Company projection returned to the caller, including the concurrency version. Excludes the
// owning-tenant field and the normalized code; status/reason are the bounded string values.
public sealed record CompanyResponse(
  Guid CompanyId,
  string CompanyCode,
  string CompanyName,
  string BaseCurrencyCode,
  string Status,
  string StatusChangeReasonCode,
  DateTimeOffset StatusChangedUtc,
  string StatusChangedBy,
  DateTimeOffset CreatedUtc,
  string? CreatedBy,
  DateTimeOffset ModifiedUtc,
  string? ModifiedBy,
  string RowVersion)
{
  public static CompanyResponse From(CompanyDto dto, string rowVersion) => new(
    dto.CompanyId,
    dto.CompanyCode,
    dto.CompanyName,
    dto.BaseCurrencyCode,
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

// Bounded page of safe Company projections. Order is the backend's deterministic order
// (company name, then company id); the transport does not re-sort.
public sealed record CompanyPageResponse(
  IReadOnlyCollection<CompanyResponse> Items,
  int PageNumber,
  int PageSize,
  int TotalCount,
  int TotalPages);
