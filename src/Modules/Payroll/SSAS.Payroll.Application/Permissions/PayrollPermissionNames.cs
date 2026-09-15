namespace SSAS.Payroll.Application.Permissions;

// THE CODE-OWNED PAYROLL PERMISSION SET (OD-PAY-0016, FP-012 authorization-model).
//
// `<Plane>.<Resource>.<Action>`, three ASCII-identifier segments, matching the platform convention.
//
// ---- NAMING THEM IS NOT REGISTERING THEM (FP-006P).
//
// This file is the single source of the names; it is not a catalog. A role may only be granted a permission
// the composed `IPermissionCatalog` DEFINES, and FP-006P records what happens otherwise: HR's constants
// existed, no catalog defined them, no role could hold one, and **every Employee endpoint refused every
// caller**. `PayrollPermissionCatalogContributor` turns these into definitions and the Host registers it.
//
// ================================================================================================
// PAY DATA IS THE MOST SENSITIVE READ SURFACE IN THIS PRODUCT, AND THE SPLITS BELOW SAY SO.
// ================================================================================================
//
// `DEC-POS-0018` separated `HR.SalaryGrades.View` from ordinary HR reads when the data was merely
// STRUCTURAL — a band attached to a job, disclosing pay policy but no person's pay. Individual compensation
// is personal data, so the precedent applies with MORE force, not less: **no HR permission grants sight of
// what an individual is paid** (`BR-PAY-0010`), and an API test asserts exactly that bleed does not happen.
//
// The counter-discipline is `DEC-DOC-0015`, which DECLINED an additive permission because export read
// exactly what search read — no new boundary, no new permission. Applied here: a permission exists when it
// exposes something a caller could not otherwise see, and not otherwise.
public static class PayrollPermissionNames
{
  // ---- COMPENSATION. The sensitive pair.
  //
  // Reading what someone is paid and setting what someone is paid are different acts by different people in
  // every organisation that has both, so they are different grants.
  public const string ViewCompensation = "Payroll.Compensation.View";
  public const string ManageCompensation = "Payroll.Compensation.Manage";

  // ---- ELEMENTS. Structural, and deliberately WEAKER than the compensation pair.
  //
  // An element definition says the company pays a housing allowance; it says nothing about who receives one
  // or how much. That is the same distinction `DEC-POS-0018` drew for salary bands, so it gets the same
  // treatment: its own permission, but not the personal-data one.
  public const string ViewElements = "Payroll.Elements.View";
  public const string ManageElements = "Payroll.Elements.Manage";

  // ---- RUNS.
  public const string ViewRuns = "Payroll.Runs.View";
  public const string ManageRuns = "Payroll.Runs.Manage";

  // ---- THE SENSITIVE ACT (BR-PLT-0103, OD-PAY-0009).
  //
  // `BR-PLT-0103` names Payroll Processing a sensitive operation requiring elevated permissions. The authored
  // rule does not say WHICH act is the sensitive one; `OD-PAY-0009` placed it at APPROVAL and this constant
  // is that ruling made grantable.
  //
  // Calculation commits nothing — it can be run, found wrong, and run again. Approval is the assertion
  // *these are the amounts these people will be paid*, and it is the gate the ledger posting passes through.
  // Separated from `ManageRuns` on the `GL.Drafts.Manage` / `GL.Journals.Post` precedent, so preparing work
  // and authorizing it can be different people.
  public const string ApproveRuns = "Payroll.Runs.Approve";

  // Separate from Approve because posting touches ANOTHER module's ledger under GL's own sensitivity regime.
  // If an owner merges the two, that should be a decision rather than a default.
  public const string PostRuns = "Payroll.Runs.Post";

  // ---- PAYSLIPS. The other personal read surface (OD-PAY-0015).
  //
  // Deliberately NOT folded into `ViewRuns`: a run's existence, status and totals are operational, but the
  // lines beneath them are an individual's pay.
  //
  // **Self-service is NOT here, and its absence is still deliberate — but the reason has changed (T-083).**
  //
  // `OD-PAY-0016` deferred it because it would depend on a mapping from the authenticated identity to an
  // employee record, and this build did not assert such a mapping exists. **It does now:**
  // `UserEmployeeLink` (`ADR-030`, T-082), asserted against a real database.
  //
  // **So the dependency was satisfied, and FP-015 added it (T-089).** `Payroll.Payslips.ViewOwn` is
  // declared below, catalogued, and routed at `GET /me/payslips`. Adding one once rested on an unverified
  // assumption — *"exactly the shape of the FP-011 near-miss"* — and the mapping is what removed that.
  // **Nothing here is waiting on an input, and nothing is waiting on FP-015 either.**
  public const string ViewPayslips = "Payroll.Payslips.View";

  // ---- SELF-SERVICE (FP-015, `OD-SS-0001`, T-088). A DISTINCT PERMISSION, NOT A SCOPE.
  //
  // `OD-SS-0001` expressed the ruling as `payroll.payslip.view.self`, which cannot exist: a permission name
  // is exactly three segments (`PermissionName.cs:36`) and that string is four. **The ruling's substance is
  // untouched** — a distinct permission rather than a scope on the administrative one, because a scope
  // applied at the handler relies on every handler remembering, and the architecture guards assert
  // permissions rather than scopes.
  //
  // **It shares a prefix with `ViewPayslips` and shares nothing else.** The resemblance is for a human
  // reading a role screen; the authorization stack compares claim values ordinally and cannot see it
  // (`AC-SS-0006`). Holding one grants nothing of the other in either direction.
  public const string ViewOwnPayslips = "Payroll.Payslips.ViewOwn";
}
