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
// ---- ⚠ `Field` IS A JSON PATH INTO THE REQUEST BODY, NOT A PROPERTY NAME (T-272).
//
// It began as a flat name, and a flat name cannot address an element of a collection. An error raised
// inside `foreach (var assignment in assignments)` concerns `assignments[].payElementId` -- a property
// of an ELEMENT, not of the body. **And that is where attribution is worth most: a caller editing a
// journal of twenty lines needs to know which line far more than a caller with one bad `name` needs the
// word `name`.** The flat form failed hardest exactly where it mattered.
//
// The semantics a client may rely on:
//
//   * segments are separated by `.` and name **serialized** properties -- what the caller sent, never a
//     CLR name; `FieldAttributionArchitectureTests` resolves each against its declared `JsonPropertyName`
//   * `[]` marks a collection segment: `assignments[].payElementId` is *that property of some element*
//   * **an index appears only when the raising code knows it.** No domain guard tracks a loop index
//     today, so `[]` is always empty -- and an empty `[]` is honest where a fabricated `[0]` would not be
//   * a single segment is a valid path and means what it always meant, so widening this cost nothing
//   * absent entirely means **no single input is at fault** -- mark nothing
public sealed record Error(string Code, string Message, string? Field = null)
{
  public static readonly Error None = new(string.Empty, string.Empty);
}
