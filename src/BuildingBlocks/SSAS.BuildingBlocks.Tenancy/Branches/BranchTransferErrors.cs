using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Tenancy.Branches;

// THE ERROR VOCABULARY OF THE SANCTIONED TRANSFER CHANNEL (FP-006C2, ADR-024 decisions 3 and 12).
//
// It lives beside the contracts that return it rather than with the rest of the branch errors, because a
// business module calling the channel has to be able to name what came back — and under ADR-012 it cannot
// reach into Platform's Domain to do so.
//
// NOTHING HERE NAMES DATABASE TOPOLOGY. Branch lives in the tenant database and the assignment rows in the
// platform one, and a caller told which is which learns the shape of the estate from an error message.
public static class BranchTransferErrors
{
  // A malformed declaration: no entity, an empty branch identifier, or a source equal to the destination.
  // Safe to distinguish because it describes the REQUEST rather than any branch's existence or state.
  public static readonly Error TransferInvalid =
    new("Branch.TransferInvalid", "The branch transfer declaration is not valid.");

  // Two open declarations would make "which transfer is in force" ambiguous at the write boundary, and the
  // safe reading of an ambiguous authorization is none.
  public static readonly Error TransferAlreadyInProgress =
    new("Branch.TransferAlreadyInProgress", "A branch transfer is already in progress for this operation.");

  // ONE GENERIC REFUSAL FOR EVERY WAY A TRANSFER CAN BE UNAUTHORIZED at save time — authority withdrawn,
  // the source no longer qualifying for recovery, or the declaration no longer matching live state.
  // Destination failures deliberately surface as the branch resolver's own generic selection error instead,
  // unchanged, so a destination identifier cannot be probed for existence through the transfer path either.
  public static readonly Error TransferNotPermitted =
    new("Branch.TransferNotPermitted", "The branch transfer is not permitted.");
}
