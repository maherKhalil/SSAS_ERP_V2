namespace SSAS.BuildingBlocks.Domain;

// THE LEGAL-ENTITY OWNERSHIP DIMENSION (FP-006C1, ADR-025).
//
// Tenant ownership answers "whose data is this"; COMPANY ownership answers "which legal entity inside that
// tenant owns it"; branch ownership answers "which operating location produced it". Company and Branch are
// SIBLING dimensions beneath the tenant, not nested — a Company is not located at a branch and a Branch does
// not belong to a company — so an entity may carry either, both, or neither, and each is an independent
// classification (ADR-023, ADR-025).
//
// IMPLEMENTING THIS IS A DELIBERATE CLASSIFICATION, NOT A DEFAULT, for exactly the reason IBranchOwnedEntity
// says so: the failure mode is silent. An entity that should have been company-scoped and was not is
// readable by every company in the tenant, and nothing about it looks wrong.
//
// THE SETTER EXISTS SO THE SERVER CAN STAMP IT, and for no other reason. CompanyId is assigned by the write
// boundary from the trusted company execution context, is only ever CONFIRMED when a caller supplies one,
// and is refused if it changes after creation — the same shape as ITenantOwnedEntity.TenantId.
public interface ICompanyOwnedEntity
{
  Guid CompanyId { get; set; }
}
