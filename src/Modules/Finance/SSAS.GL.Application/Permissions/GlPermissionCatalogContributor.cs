using SSAS.BuildingBlocks.Tenancy.Permissions;

namespace SSAS.GL.Application.Permissions;

// GL'S PERMISSION DEFINITIONS (DEC-GL-0003, ADR-012 r1.2, FP-006P).
//
// A role may only be granted a permission the COMPOSED catalog defines. A module that is not registered
// with the Host contributes nothing here, and its endpoints then refuse every caller — a loud, reviewable
// omission rather than a silent one, and registration rather than reflection-based discovery.
//
// The descriptions are written for the person granting the permission, not for a developer reading code:
// they say what the holder can DO and, where it matters, what they still cannot.
public sealed class GlPermissionCatalogContributor : IPermissionCatalogContributor
{
  private static readonly ModulePermissionDefinition[] Definitions =
  [
    new(GlPermissionNames.ViewJournals,
      "View posted journal entries and their lines within the caller's authorized company scope"),
    new(GlPermissionNames.PostJournals,
      "Post a journal draft to the ledger, creating a permanent entry that cannot afterwards be edited"),
    new(GlPermissionNames.ReverseJournals,
      "Correct a posted journal by posting a reversing entry. Does not permit editing the original"),

    new(GlPermissionNames.ViewDrafts, "View unposted journal drafts"),
    new(GlPermissionNames.ManageDrafts,
      "Create, edit and discard unposted journal drafts. Does not permit posting them"),

    new(GlPermissionNames.ViewAccounts, "View the chart of accounts"),
    new(GlPermissionNames.CreateAccounts, "Create accounts in the chart of accounts"),
    new(GlPermissionNames.UpdateAccounts,
      "Rename an account. An account's code cannot be changed once the account exists"),
    new(GlPermissionNames.DeactivateAccounts,
      "Deactivate and reactivate accounts. A deactivated account keeps its history and stops accepting " +
      "new postings"),

    new(GlPermissionNames.ViewPeriods, "View the fiscal calendar and the state of each period"),
    new(GlPermissionNames.ManagePeriods, "Define fiscal years and the periods within them"),
    new(GlPermissionNames.ClosePeriods,
      "Close and reopen fiscal periods. Closing a period stops all posting into it"),

    new(GlPermissionNames.ViewReports,
      "Produce account balance enquiries and trial balances across the caller's authorized company scope")
  ];

  // Enumerated once at composition and never re-read, so this is a property over a static array rather
  // than a method that could be tempted to compute something per call. The contract requires determinism
  // for the same reason the tenant-model contributors do: the composed set participates in decisions made
  // once at startup.
  public IReadOnlyCollection<ModulePermissionDefinition> Permissions => Definitions;
}
