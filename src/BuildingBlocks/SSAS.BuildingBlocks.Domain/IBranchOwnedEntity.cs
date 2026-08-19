namespace SSAS.BuildingBlocks.Domain;

// THE SECOND OWNERSHIP DIMENSION (Branch foundation B0/B1).
//
// Tenant ownership answers "whose data is this"; branch ownership answers "which operating location inside
// that tenant produced it". They are INDEPENDENT: every branch-owned entity is also tenant-owned, but most
// tenant-owned data is tenant-global and must NOT carry a branch — a Branch cannot belong to a branch, and
// a Company is not located at one.
//
// IMPLEMENTING THIS IS A DELIBERATE CLASSIFICATION, NOT A DEFAULT. The architecture guard requires every
// tenant entity to be explicitly classified as tenant-global or branch-owned, because the failure mode is
// silent: an entity that should have been branch-scoped and was not is readable by every branch in the
// tenant, and nothing about it looks wrong.
public interface IBranchOwnedEntity
{
  Guid BranchId { get; set; }
}
