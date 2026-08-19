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

// APPEND-ONLY PERSISTENCE, ENFORCED BY THE WRITE BOUNDARY (FP-006C3, ADR-024 decision 5).
//
// Some records exist to say what happened, and a record of what happened that can be edited afterwards is
// not one. Employee branch history is the first: a correction is another transfer, never a rewrite.
//
// IMPLEMENTING THIS IS A DELIBERATE CLASSIFICATION, and it is enforced centrally rather than by convention.
// "There is no repository method for it" protects only the callers who go through the repository; the write
// boundary refuses a Modified or Deleted entry for any type marked here, whatever path tracked it.
public interface IAppendOnlyEntity
{
}
