namespace SSAS.BuildingBlocks.Domain;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
  "Naming",
  "CA1716:Identifiers should not match keywords",
  Justification = "Error is an approved BuildingBlocks primitive in the Sprint-00 specification.")]
// ⚠ `Field` NAMES THE INPUT THIS ERROR CONCERNS, AND IT IS OPTIONAL BY DESIGN (T-269).
//
// 129 domain codes collapse into the single wire code `request.invalid`. Since T-261 the message
// travels, so a human can read what went wrong -- but **a form cannot: it has to know which input to
// mark, and it should not be parsing prose to find out.** That is the whole remaining cost of the
// collapse, and `Field` is the machine-readable half the message cannot supply.
//
// **It does not compete with `Code`. The code says which RULE was broken; the field says which INPUT
// broke it**, and a form needs both -- `AddHolidayCommand.Name` and `CreateWorkingCalendarCommand.Name`
// both serialize `name`, and only the code distinguishes them.
//
// ---- WHY A DOMAIN TYPE MAY CARRY A SERIALIZED NAME.
//
// The 16 errors that set it are raised inside domain VALUE OBJECTS, and **a value object represents
// exactly one input by construction** -- an `AccountCode` is only ever an account's code. Every command
// that feeds one already names the property identically. **So this does not create a coupling between
// the domain and a transport shape; it writes down a convention the codebase already keeps everywhere
// and nothing verified.** `FieldAttributionArchitectureTests` is what now verifies it.
//
// Optional because **most errors have no single input**: a precondition, a conflict, a server fault and
// a structurally incomplete payload all leave it null, and null means *do not mark any field*.
public sealed record Error(string Code, string Message, string? Field = null)
{
  public static readonly Error None = new(string.Empty, string.Empty);
}
