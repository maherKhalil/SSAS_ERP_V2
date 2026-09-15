namespace SSAS.Attendance.Domain.Leave;

// ==================================================================================================
// WHO IS ACTING, WHEN THE ANSWER MAY LEGITIMATELY BE "NOBODY WE CAN NAME" (BR-ATT-0007, T-085).
// ==================================================================================================
//
// ---- WHAT THIS REPLACES AND WHY A `Guid?` WAS NOT ENOUGH.
//
// The root-path self-approval bar takes the acting user's employee, and `null` is a legitimate answer:
// `ADR-030` Decision 5 makes the link optional, and a platform-support holder with no employee record is a
// normal caller who cannot be the requester. So the bar must not refuse on absence.
//
// **That makes `null` mean two things the aggregate cannot tell apart:** *the user was unresolvable*, and
// *the caller never asked*. Both approve, and the aggregate is behaving correctly in each case. A test can
// pin the call sites that exist — `LeaveApprovalHandlerTests` does — **but it cannot pin the one nobody has
// written yet**, and that later addition is this repository's dominant failure mode: the route outside the
// inventory, the permission named but never contributed, the error declared with no arm.
//
// ---- WHAT THE TYPE BUYS, STATED HONESTLY: DECLARED, NOT UNREPRESENTABLE.
//
// It does not make the skip impossible. C# has no way to do that here — the parameter is a reference and a
// reference can be null. **What it does is make every route through either named or loud:**
//
//   ActingEmployee.Unresolved()   a named act — greppable, enumerable, one command answers
//                                 "did anyone skip the bar"
//   null                          CS8625 under `<Nullable>enable</Nullable>`, and the gate fails on any
//                                 warning — so the LANGUAGE permits it and the POLICY does not
//   null!                         a written suppression, conspicuous in review and in a grep
//
// `null` being unenumerable is the whole difference. `ModulePermissionDefinition` sets the higher standard
// — *"with no property there is nothing to review, and the escalation cannot be expressed"* — and this does
// not reach it. **Declared is the whole distance available when the alternative is a bare `null`.**
//
// ---- IT IS A CLASS, AND THAT IS FORCED.
//
// A `readonly record struct` cannot satisfy the rule that an unresolved instance must be written: C#
// guarantees a parameterless default for every struct, so `default(ActingEmployee)` would be reachable,
// constructible, and silently unresolved — the type reintroducing the defect while wearing a name.
//
// ---- AND THE COMPARISON LIVES HERE RATHER THAN AT THE CALLER.
//
// `Matches` means an unresolved actor can never equal anyone, by construction. Exposing the identifier and
// letting each caller compare would put that judgement at every call site, which is the arrangement this
// type exists to end.
public sealed class ActingEmployee
{
  private static readonly ActingEmployee UnresolvedActor = new(null);

  private readonly Guid? employeeId;

  private ActingEmployee(Guid? employeeId)
  {
    this.employeeId = employeeId;
  }

  // The acting user resolved to this employee. `Guid.Empty` is refused rather than coerced: an empty
  // identifier is not an employee, and accepting one would let a caller pass a value that silently matches
  // nothing while looking resolved.
  public static ActingEmployee Resolved(Guid employeeId) =>
    employeeId == Guid.Empty
      ? throw new ArgumentException("A resolved acting employee cannot be an empty identifier.", nameof(employeeId))
      : new ActingEmployee(employeeId);

  // The acting user has no linked employee. An ordinary answer (`ADR-030` Decision 5), and the ONLY way to
  // express it — there is no default constructor, no `Empty`, and no conversion from `Guid?`.
  public static ActingEmployee Unresolved() => UnresolvedActor;

  // True only when this actor is resolved AND is that employee. An unresolved actor matches nobody, which
  // is what keeps "we do not know who this is" from ever reading as "this is the requester".
  public bool Matches(Guid employeeId) =>
    this.employeeId is { } acting && acting == employeeId;
}
