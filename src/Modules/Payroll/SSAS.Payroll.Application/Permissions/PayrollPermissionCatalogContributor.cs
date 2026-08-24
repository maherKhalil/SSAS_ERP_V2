using SSAS.BuildingBlocks.Tenancy.Permissions;

namespace SSAS.Payroll.Application.Permissions;

// PAYROLL'S PERMISSION DEFINITIONS (OD-PAY-0016, ADR-012 r1.2, FP-006P).
//
// A role may only be granted a permission the COMPOSED catalog defines. A module that is not registered with
// the Host contributes nothing here, and its endpoints then refuse every caller — a loud, reviewable
// omission rather than a silent one.
//
// The descriptions are written for the person GRANTING the permission, not for a developer reading code:
// they say what the holder can do and, where it matters, what they still cannot. On this surface the second
// half matters more than anywhere else in the product, because someone granting "view runs" needs to know it
// does not hand over everyone's pay.
public sealed class PayrollPermissionCatalogContributor : IPermissionCatalogContributor
{
  private static readonly ModulePermissionDefinition[] Definitions =
  [
    new(PayrollPermissionNames.ViewCompensation,
      "View what individual employees are paid, within the caller's authorized company scope. This is " +
      "personal data and no HR permission grants it"),
    new(PayrollPermissionNames.ManageCompensation,
      "Record a new dated compensation record for an employee. Compensation is never edited in place: a " +
      "change is a new record, and the previous one remains"),

    new(PayrollPermissionNames.ViewElements,
      "View the company's pay element definitions. Shows what the company pays in general, not what any " +
      "individual receives"),
    new(PayrollPermissionNames.ManageElements,
      "Define pay elements and map them to ledger accounts. Changes what every future run calculates and " +
      "where it posts"),

    new(PayrollPermissionNames.ViewRuns,
      "View payroll runs, their status and their totals. Does not permit viewing any individual's payslip"),
    new(PayrollPermissionNames.ManageRuns,
      "Create and calculate payroll runs. Calculation commits nothing and may be repeated; it does NOT " +
      "permit approving a run"),

    new(PayrollPermissionNames.ApproveRuns,
      "Approve a payroll run, asserting that these are the amounts these people will be paid. A sensitive " +
      "operation under BR-PLT-0103, and the point after which the run's lines can never be changed"),
    new(PayrollPermissionNames.PostRuns,
      "Post an approved payroll run to the general ledger. Requires the run to have been approved by " +
      "someone holding the approval permission"),

    new(PayrollPermissionNames.ViewPayslips,
      "View the pay lines of individual employees for a run. This is personal data")
  ];

  // Enumerated once at composition and never re-read, so this is a property over a static array rather than
  // a method that could be tempted to compute something per call. The contract requires determinism for the
  // same reason the tenant-model contributors do.
  public IReadOnlyCollection<ModulePermissionDefinition> Permissions => Definitions;
}
