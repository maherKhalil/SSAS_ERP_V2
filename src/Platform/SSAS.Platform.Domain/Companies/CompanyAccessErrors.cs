using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Companies;

// NOTHING HERE NAMES DATABASE TOPOLOGY (FP-006C1, ADR-025 decision 3).
//
// Company lives in the tenant database and UserCompanyAccess in the platform one, and a caller told which is
// which learns the shape of the estate from an error message.
//
// ---- ONE GENERIC REFUSAL FOR EVERY BAD COMPANY REFERENCE.
//
// `InvalidSelection` answers "does not exist", "belongs to another tenant", "is not active" and "you are not
// authorized for it" IDENTICALLY, on purpose: distinguishing them would let a caller probe another tenant's
// company identifiers for existence. The same reasoning BranchErrors records for InvalidSelection.
public static class CompanyAccessErrors
{
  // A trusted company context is required for company-owned data and none could be established. Distinct
  // from InvalidSelection because it says nothing about any particular company: no company was selected at
  // all, or no trusted tenant exists to select one within.
  public static readonly Error ContextRequired =
    new("Company.ContextRequired", "A trusted company context is required for company-owned data.");

  // A company must be selected before company-scoped operations. Separate from ContextRequired so a caller
  // that simply has not chosen yet is told to choose, rather than told its context is broken.
  public static readonly Error SelectionRequired =
    new("Company.SelectionRequired", "An active company must be selected before company-scoped operations.");

  // THE ONE GENERIC REFUSAL. Nonexistent, cross-tenant, inactive and unauthorized are indistinguishable.
  public static readonly Error InvalidSelection =
    new("Company.InvalidSelection", "The selected company is not available to this user.");

  // The requested company identifier was not a usable identifier at all — malformed, empty, or absent where
  // one was required. A syntax failure, not an authorization outcome, so it is safe to distinguish.
  public static readonly Error InvalidSelectionFormat =
    new("Company.InvalidSelectionFormat", "The requested company identifier is not a valid identifier.");

  // Assignment invariants. One generic error for every bad company reference, for the same reason
  // BranchErrors.AssignmentInvalid exists.
  public static readonly Error AssignmentInvalid =
    new("Company.AssignmentInvalid", "One or more requested companies are not assignable.");
}
