using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Accounts;

// THE CHART OF ACCOUNTS' NAMED REFUSALS (REQ-GL-0005..0008, BR-GL-0004).
//
// Named rather than numbered, per the transport rules GL inherits: a client branches on a stable name, and a
// message is for a human. `api-contracts.md` fixes the wire codes and this file is where they originate.
public static class AccountErrors
{
  public static readonly Error InvalidCode = new(
    "Gl.AccountCodeInvalid",
    "An account code is required, must be at most 64 characters, and cannot contain control characters.",
    Field: "code");

  public static readonly Error InvalidName = new(
    "Gl.AccountNameInvalid",
    "An account name is required and must be at most 256 characters.",
    Field: "name");

  public static readonly Error DuplicateCode = new(
    "Gl.AccountCodeConflict",
    "An account with this code already exists.");

  public static readonly Error NotFound = new(
    "Gl.AccountNotFound",
    "The account does not exist.");

  // ---- WHY THIS EXISTS AND WHY ITS MESSAGE NAMES THE ACCOUNT (BR-GL-0004, DEC-GL-0009).
  //
  // The rule is "accounts marked as inactive cannot receive transactions", and a refusal that says only
  // "a line was invalid" makes the user hunt for which one. `lifecycle-model.md` states the standard:
  // "account 4100 is inactive" is the difference between a user fixing something and a user filing a ticket.
  public static Error Inactive(string accountCode) => new(
    "Gl.AccountInactive",
    $"Account '{accountCode}' is inactive and cannot receive transactions.");

  // The code is a business identifier that other records were posted against. Renaming an account is a
  // correction; re-coding one silently re-labels history, which is why it is refused rather than cascaded.
  public static readonly Error CodeIsImmutable = new(
    "Gl.AccountCodeImmutable",
    "An account's code cannot be changed after the account is created.");
}
