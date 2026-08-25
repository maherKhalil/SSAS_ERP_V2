namespace SSAS.HR.Contracts.Employment;

// ================================================================================================
// THE HR SIDE OF THE ATTENDANCE BOUNDARY — THE APPROVAL CHAIN, AND NOTHING ELSE.
// ================================================================================================
//
// The FOURTH use of the mechanism `OD-PAY-0013` ruled: `IEmployeeRoster` (HR to Payroll), `IJournalPoster`
// (GL to Payroll), `InspectPostingWindowAsync` (consumer-shaped addition to an existing contract), and now
// this. A published contract, shaped by its consumer, never an assembly reference (`ADR-012`).
//
// ---- WHY ATTENDANCE CANNOT ANSWER THIS ITSELF.
//
// `OD-ATT-0007` ruled DEPARTMENT-MANAGER approval with parent-chain escalation. Departments, their nesting
// and their managers are all HR's facts, and Attendance may not reach `SSAS.HR.Domain` to walk them.
//
// ---- WHAT THE CODE ACTUALLY SUPPORTS, CHECKED RATHER THAN ASSUMED.
//
// **There is no employee-to-manager edge.** `Employee` carries `CompanyId`, `BranchId`, `DepartmentId` and
// `PositionId` — and no `ManagerId`.
//
// What exists is `DepartmentManager`: one seat per department, keyed on `DepartmentId`, which is why a
// second row is unrepresentable and why the assign route pre-translates a unique-constraint violation. And
// `Department.ParentDepartmentId` with `ChangeParent()`, so a chain upward exists.
//
// So a reporting line IS derivable — employee, department, parent chain, that department's manager — but it
// is **indirect, department-mediated and single-seated**, which is a materially different thing from a
// direct manager edge and is why the ruling needed three sub-answers rather than one.
//
// ---- THE DIVISION OF LABOUR, WHICH IS THE POINT OF THE SHAPE BELOW.
//
// **HR walks the tree. ATTENDANCE decides the policy.**
//
// This returns the chain of department managers above an employee, nearest first. It applies HR's own facts
// — a department with no manager contributes nothing (`ManagerNotAssigned` is a modelled error, so the state
// is reachable), and a terminated manager is excluded (`ManagerTerminated` is modelled too, and leave
// requests do not stop arriving because a manager left).
//
// It does **NOT** apply the self-approval bar. That is Attendance's rule (`BR-ATT-0007`), and if HR filtered
// the requester out here the rule would live in two modules and could drift — with the module that owns it
// not being the module enforcing it. The same split `IEmployeeRoster` drew when it refused to filter by
// "employed during the period".
//
// An EMPTY list is a legitimate answer meaning the chain is exhausted, and it is what triggers the ruling's
// permission-holder fallback at the root.
//
// ---- AND WHAT IT DELIBERATELY DOES NOT EXPOSE.
//
// No name, no national identifier, no position, no branch, no contact detail. An approver is an
// `EmployeeId` and the department whose seat they hold. A contract that returned `EmployeeDetail` would let
// every future Attendance feature read HR's personal data with no call-site change for anyone to review —
// the argument that keeps `IEmployeeRoster` at four fields.
public sealed record ApproverCandidate(
  // The employee holding the department's manager seat.
  Guid EmployeeId,

  // The department whose seat it is. Carried so a decision can record WHICH authority was used, which stops
  // being derivable the moment somebody reorganises the tree.
  Guid DepartmentId,

  // 0 is the employee's own department, 1 its parent, and so on. Attendance uses it to prefer the nearest
  // eligible approver after applying its own bars.
  int Depth);

public interface IEmployeeApproverDirectory
{
  // Nearest first. Departments with no manager, and departments whose manager is terminated, are absent
  // rather than present-and-null: an absent candidate cannot be accidentally selected, whereas a null one
  // has to be remembered about at every call site.
  //
  // Bounded internally against a cycle in the parent chain. `Department.ChangeParent` refuses self-parenting
  // but a longer cycle is not structurally impossible, and an approval walk is not the place to discover it
  // by hanging.
  Task<IReadOnlyList<ApproverCandidate>> GetApproverChainAsync(
    Guid companyId,
    Guid employeeId,
    CancellationToken cancellationToken = default);
}
