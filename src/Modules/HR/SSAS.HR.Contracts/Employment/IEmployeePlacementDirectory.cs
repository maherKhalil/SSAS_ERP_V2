namespace SSAS.HR.Contracts.Employment;

// ==================================================================================================
// WHERE AN EMPLOYEE SITS, ASKED EMPLOYEE-FIRST (FP-015, T-088; widened T-089).
// ==================================================================================================
//
// ---- IT RETURNS SCOPE DIMENSIONS, AND NOTHING ELSE, DELIBERATELY.
//
// Company and branch, because those are the two dimensions a module read scope has. **No department, no
// name, no status.** The moment this returns a third thing that is not a scope dimension it stops being a
// placement lookup and becomes a general employee reader — and this is a door with a deliberately weak
// lock (`EmployeeReadScopeArchitectureTests`, guard 16). **A narrow door is what makes the weak lock
// acceptable.**
//
// ---- ONE METHOD, NOT TWO, AND THAT IS THE WHOLE POINT.
//
// A separate company-only lookup would let the RECORDS path — which is branch-scoped under `OD-ATT-0011` —
// call it and forget the branch. **A silently unbranched records read is a widening that looks like a
// working feature**, so the omission is removed rather than documented.
//
// ---- HR ALREADY KNOWS THIS. IT WAS ONLY ANSWERABLE FROM THE OTHER DIRECTION.
//
// `IEmployeeRoster` and `IEmployeeApproverDirectory` are both COMPANY-FIRST: you must already know the
// company to ask them anything, and `EmploymentRecord` then hands the `CompanyId` back. So this contract
// adds no new fact about employees — **it answers an existing one keyed the way a self-service caller can
// actually ask it**, which is with an employee and nothing else.
//
// ---- WHY A NEW INTERFACE RATHER THAN A MEMBER ON `IEmployeeRoster`.
//
// `IEmployeeRoster` is about payroll-run candidate selection — *"every employee of a company whose
// employment overlaps the window"*, with who is included left to Payroll. A single-employee lookup is a
// different question with a different consumer.
//
// **And `IEmployeeRoster` is consumed by Attendance as well as Payroll.** Adding a member would oblige
// Attendance's implementations and every stub of it to grow a method neither needs. A narrow interface
// ripples nowhere.
//
// ---- IT DOES NOT AUTHORIZE, AND THAT IS THE POINT RATHER THAN AN OMISSION.
//
// `IEmployeeApproverDirectory` refuses a caller with no access to the company it is asked about. **This one
// must not**, because the caller it exists for is an ordinary employee who may hold no company-access grant
// at all — and requiring one would reintroduce exactly the dependency this contract was added to remove.
//
// **The isolation that holds is tenant isolation**: the implementation reads through the tenant database
// under its global tenant filter, so an employee identifier belonging to another tenant simply is not
// found. **Company isolation is not enforced here and does not need to be** — the caller's only reachable
// employee is the one `UserEmployeeLink` names, and that link is itself tenant-scoped.
//
// ---- ONE COMPANY, AND THAT IS TRUE TODAY RATHER THAN FOREVER.
//
// `Employee.CompanyId` is singular, so a single answer is honest. **If an employee ever belongs to many
// companies, this contract's shape changes and so does the read scope built from it** — they change
// together, which makes them one unit rather than a hidden assumption. Whoever makes `CompanyId` plural
// meets this paragraph here.
// The two scope dimensions an employee sits in. A record rather than a tuple so a caller cannot silently
// swap them: both are `Guid` and a positional mix-up would compile.
public sealed record EmployeePlacement(Guid CompanyId, Guid BranchId);

public interface IEmployeePlacementDirectory
{
  // The employee's placement, or null when no such employee exists in the current tenant.
  //
  // **Null is an ordinary answer, not a fault.** `ADR-030` Decision 4 makes a cross-database foreign key
  // impossible, so a `UserEmployeeLink` naming an employee that no longer exists is a state the application
  // must answer rather than assume away.
  Task<EmployeePlacement?> GetPlacementAsync(Guid employeeId, CancellationToken cancellationToken = default);
}
