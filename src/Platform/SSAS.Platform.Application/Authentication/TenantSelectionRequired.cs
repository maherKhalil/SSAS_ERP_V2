namespace SSAS.Platform.Application.Authentication;

public sealed record TenantSelectionRequired(
  IReadOnlyList<TenantMembershipSelectionSummary> Memberships,
  SensitiveTenantSelectionProof SelectionProof) : BeginTenantAccessResult;
