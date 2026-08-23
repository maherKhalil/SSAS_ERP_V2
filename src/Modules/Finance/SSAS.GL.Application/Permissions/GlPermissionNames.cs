namespace SSAS.GL.Application.Permissions;

// THE CODE-OWNED GL PERMISSION SET (DEC-GL-0003, FP-011 authorization-model).
//
// `<Plane>.<Resource>.<Action>`, matching the established platform convention and satisfying the permission
// -name grammar of exactly three ASCII-identifier segments, so the names themselves need no framework
// change. `Requirement-Numbering.md`'s example already reserved `PER-GL-PostJournal`, so this shape is
// consistent with the reservation rather than a new invention.
//
// ---- NAMING THEM IS NOT REGISTERING THEM (FP-006P).
//
// This file is the single source of the names; it is not a catalog. A role may only be granted a permission
// the composed `IPermissionCatalog` DEFINES, and FP-006P records what happens otherwise: HR's constants
// existed, no catalog defined them, no role could hold one, and **every Employee endpoint refused every
// caller**. `GlPermissionCatalogContributor` turns these constants into definitions and the Host registers
// it. Adding a constant here without adding it there produces a permission that authorizes nothing.
//
// ---- THESE ARE FUNCTIONAL AUTHORITY, AND NOTHING ELSE.
//
// Holding one says which OPERATION is permitted. It says nothing about which companies are reachable, which
// remains the independent scope dimension resolved by `ITenantCompanyAccessResolver`. Conversely
// `Platform.Tenant.Administer` widens that scope and grants NONE of these: an administrator without
// `GL.Journals.View` cannot read a journal (`ADR-025` decision 8, `AC-GL-0017`).
public static class GlPermissionNames
{
  // ---- JOURNALS.
  //
  // Post and Reverse are SEPARATE from each other and from View, following the `DEC-EMP-0030` sensitivity
  // precedent that gave Terminate and Transfer their own permissions rather than folding them into Update.
  // A reversal is a posting, so the split only earns its place if the product may want someone to post but
  // not reverse — and for a ledger correction, that is exactly the kind of authority an owner separates.
  public const string ViewJournals = "GL.Journals.View";
  public const string PostJournals = "GL.Journals.Post";
  public const string ReverseJournals = "GL.Journals.Reverse";

  // Drafts are scratch space, not ledger entries, so editing one is not a posting act and carries its own
  // permission. A user who may prepare work for someone else to post is a real separation of duties, and it
  // is only expressible because `OD-GL-0007` made the draft a distinct aggregate.
  public const string ViewDrafts = "GL.Drafts.View";
  public const string ManageDrafts = "GL.Drafts.Manage";

  // ---- CHART OF ACCOUNTS. Tenant-level master data (`OD-GL-0003`).
  //
  // No `Delete`: `BR-GL-0004` makes deactivation the lifecycle, and an account is never deleted, so a
  // permission for it would authorize an operation that does not exist.
  public const string ViewAccounts = "GL.Accounts.View";
  public const string CreateAccounts = "GL.Accounts.Create";
  public const string UpdateAccounts = "GL.Accounts.Update";
  public const string DeactivateAccounts = "GL.Accounts.Deactivate";

  // ---- FISCAL CALENDAR. Company-level (`OD-GL-0004`), so these writes are company-scoped.
  //
  // Closing is separated from defining: defining next year's calendar is routine configuration, while
  // closing a period stops posting and is the operation an auditor asks who performed.
  public const string ViewPeriods = "GL.Periods.View";
  public const string ManagePeriods = "GL.Periods.Manage";
  public const string ClosePeriods = "GL.Periods.Close";

  // ---- REPORTING.
  //
  // ---- THIS LOOKS LIKE IT CONTRADICTS `DEC-DOC-0015`. IT APPLIES THE SAME RULE.
  //
  // FP-009 DECLINED an additive export permission for V1, reasoning that a separate permission is not
  // justified while both paths share one predicate and neither can read more than the other. Read quickly,
  // that says "no additive permissions" and this constant breaks it.
  //
  // Read properly, it says **no permission without a boundary** — and export had none, because it read
  // exactly what search read. A trial balance does have one: it AGGREGATES across every account in the
  // caller's company scope, so it can reveal a total for accounts an individual enquiry would surface one
  // at a time. Aggregation reads MORE than enumeration, and that difference is the boundary.
  //
  // Same rule, different fact, opposite outcome. Stated here because the two decisions otherwise read as
  // inconsistent to whoever finds them next.
  public const string ViewReports = "GL.Reports.View";
}
