using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Tenants;

public sealed record TenantAuthenticationEligibilityResult
{
  private TenantAuthenticationEligibilityResult(
    Guid tenantId,
    bool exists,
    TenantStatus? tenantStatus,
    bool isAuthenticationEligible,
    TenantAuthenticationIneligibilityReason tenantAuthenticationIneligibilityReason)
  {
    TenantId = tenantId;
    Exists = exists;
    TenantStatus = tenantStatus;
    IsAuthenticationEligible = isAuthenticationEligible;
    TenantAuthenticationIneligibilityReason = tenantAuthenticationIneligibilityReason;
  }

  public Guid TenantId { get; }

  public bool Exists { get; }

  public TenantStatus? TenantStatus { get; }

  public bool IsAuthenticationEligible { get; }

  public TenantAuthenticationIneligibilityReason TenantAuthenticationIneligibilityReason { get; }

  public static TenantAuthenticationEligibilityResult FromStatus(Guid tenantId, TenantStatus? tenantStatus) => tenantStatus switch
  {
    null => new(tenantId, false, null, false, TenantAuthenticationIneligibilityReason.TenantNotFound),
    Domain.Enums.TenantStatus.Active => new(tenantId, true, tenantStatus, true, TenantAuthenticationIneligibilityReason.None),
    Domain.Enums.TenantStatus.Provisioning => new(tenantId, true, tenantStatus, false, TenantAuthenticationIneligibilityReason.Provisioning),
    Domain.Enums.TenantStatus.Suspended => new(tenantId, true, tenantStatus, false, TenantAuthenticationIneligibilityReason.Suspended),
    Domain.Enums.TenantStatus.Archived => new(tenantId, true, tenantStatus, false, TenantAuthenticationIneligibilityReason.Archived),
    _ => throw new ArgumentOutOfRangeException(nameof(tenantStatus), tenantStatus, "Unsupported tenant status.")
  };
}
