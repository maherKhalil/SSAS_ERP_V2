namespace SSAS.BuildingBlocks.Tenancy;

// ==================================================================================================
// WHETHER AN EMPLOYMENT IS STILL CURRENT, ASKED BY PLATFORM AND ANSWERED BY HR (T-090, AC-SS-0012).
// ==================================================================================================
//
// ---- IT EXISTS BECAUSE THE SEAM NEEDS A FACT IT CANNOT SEE.
//
// `IUserEmployeeResolver` answers *which employee is this tenant user*, from `UserEmployeeLink` in the
// PLATFORM database. Whether that employment has ended lives on `Employee` in the TENANT database, and
// `ADR-030` Decision 4 makes a cross-database foreign key impossible — so the seam cannot read the status,
// it has to ask.
//
// **This is the FIRST contract on this seam pointing Platform -> module.** `IUserEmployeeResolver` and
// `ICurrentTenantUser` both point module -> Platform. The direction is new and the reason is narrow: the
// authority over employment status is HR's, and a copy of it on the Platform side would be a second
// source of truth that drifts the first time a status rule changes.
//
// ---- WHY HERE AND NOT IN `SSAS.HR.Contracts`.
//
// `SSAS.Platform.Infrastructure` cannot reference an HR project — no Platform project references any
// module, and `ADR-012` is why. Declaring it here keeps that true: HR implements a `BuildingBlocks.Tenancy`
// interface, Platform consumes it, and neither project learns about the other. It is the same edge
// `IUserEmployeeResolver` already uses, travelled in the other direction.
//
// ---- THE ENUM IS THREE-VALUED AND `Unknown` IS THE DEFAULT ON PURPOSE.
//
// `default(EmploymentStanding)` is `Unknown`, and every caller must treat `Unknown` the way it treats
// `Ended`. A two-valued `bool` would make the safe answer to "I could not find that employee" depend on
// which way the implementor happened to lean, and a `bool?` would put that decision at every call site.
//
// **The failure direction is chosen once, here, and it is closed.**
public enum EmploymentStanding
{
  // No employee with that identifier is visible to this tenant. **Not an error, and not an invitation to
  // proceed:** a dangling `UserEmployeeLink` is reachable (`ADR-030` Decision 4), and the caller cannot
  // tell that from a probe for an identifier that never existed. Treated as `Ended` by every caller.
  Unknown = 0,

  // Employed. Includes `Inactive` — unpaid leave, suspension — because the employment relationship
  // PERSISTS through those and is fully reversible (`EmployeeStatus.Inactive`). An employee on unpaid
  // leave reading their own payslip is the ordinary case, not the one this contract exists to refuse.
  Current = 1,

  // Employment has ended. `EmployeeStatus.Terminated`, which is terminal in V1.
  Ended = 2
}

public interface IEmploymentStandingDirectory
{
  // Takes the employee EXPLICITLY and answers about exactly that one. It resolves no tenant of its own:
  // the tenant comes from the trusted request context inside the implementation, so a caller cannot ask
  // about an employee in another tenant.
  //
  // Never throws for an unknown employee. `Unknown` and `Ended` are separate values for the implementor's
  // clarity, not so a caller can act on the difference — see `AC-SS-0012` and `BR-PLT-0002`.
  Task<EmploymentStanding> GetStandingAsync(Guid employeeId, CancellationToken cancellationToken = default);
}
