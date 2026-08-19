using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Tenancy.Branches;

// THE SANCTIONED BRANCH-TRANSFER CHANNEL (FP-006C2, ADR-024 decision 3).
//
// This is the ONLY way a BranchId modification is ever permitted. Everything else — an ordinary update, a
// repository call, a request that happens to carry a branch identifier — is refused by the write boundary
// exactly as before (ADR-024 decision 2).
//
// ---- IT IS OPENED BY TRUSTED ORCHESTRATION, AND BY NOTHING ELSE.
//
// A command handler performs dual authorization first — the source through the trusted execution context,
// the destination through ITenantBranchAccessResolver — and only then declares the transition. There is
// deliberately no way to reach this from a request DTO, a header, a form field, a token claim, a query
// string, an entity property, or a repository parameter forwarded from controller input: the declaration
// requires the TRACKED ENTITY INSTANCE, which only server-side orchestration that has already loaded and
// authorized the record can supply.
//
// ---- IT IS SCOPED, AND ITS LIFETIME IS THE POINT.
//
// Begin returns a disposable whose disposal clears the declaration, so the authorization lives exactly as
// long as the operation that earned it. A declaration cannot survive into a later SaveChanges, cannot be
// reused for a second entity, and is cleared on the exception path as well as the success path.
//
// Implementations are request-scoped instance state, never static or global: two concurrent operations must
// not be able to see or overwrite one another's declarations.
public interface IBranchTransferScope
{
  // The declaration currently in force, or null when no transfer is open. Reading this authorizes nothing
  // on its own — the write boundary re-validates it against live state through IBranchTransferAuthorizer
  // before it permits anything.
  BranchTransferDeclaration? Current { get; }

  // Declares one transition for the lifetime of the returned handle.
  //
  // NESTING IS REFUSED RATHER THAN STACKED. Two open declarations would make "which transfer is in force"
  // ambiguous at the boundary, and the safe reading of an ambiguous authorization is none.
  Result<IDisposable> Begin(BranchTransferDeclaration declaration);
}
