using System.Reflection;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY `Error.Field` NAMES A REAL PROPERTY ON A REAL REQUEST DTO, AND EVERY ONE IS ACCOUNTED FOR (T-269).
// ==================================================================================================
//
// ---- WHAT `Field` IS FOR.
//
// 129 domain codes collapse into the single wire code `request.invalid`. Since T-261 the message travels,
// so a human can read what went wrong — but **a form cannot: it must know which input to mark, and it
// should not be parsing prose to find out.** `Field` is the machine-readable half.
//
// ---- ⚠ WHY A DOMAIN CONSTANT MAY CARRY A SERIALIZED NAME, AND WHY THAT NEEDS THIS FILE.
//
// The sixteen are raised inside domain VALUE OBJECTS, and **a value object represents exactly one input by
// construction** — an `AccountCode` is only ever an account's code. Every command that feeds one already
// names the property identically. **So this does not create a coupling between the domain and a transport
// shape; it writes down a convention the codebase already keeps and nothing verified.**
//
// Nothing verified it, and that is the whole risk: rename `DecideLeaveRequestCommand.DecisionNote` and the
// field silently points at nothing. A form marks no input, or the wrong one, and no test notices.
//
// ---- AND THE GUARD RUNS IN BOTH DIRECTIONS, BECAUSE ONE DIRECTION IS SILENTLY PARTIAL.
//
// A curated map verified by reflection catches a **renamed property**. It cannot catch a **missing row**: a
// code gains a `Field`, nobody adds a map entry, and a one-directional guard simply does not check it —
// green, and quieter than before. So the second test enumerates every `Error` in the product whose `Field`
// is set and demands a row for each. **The map is manual; both checks are mechanical.**
public sealed class FieldAttributionArchitectureTests
{
  // code -> the field it names, the DTO that carries it, and that DTO's property.
  //
  // The DTO is named because a wrong one fails the property-exists check below — that is what reduces the
  // manual judgement to "which request carries this input", which a reader can verify by eye.
  private static readonly (string Code, string Field, string Dto, string Property)[] Attribution =
  [
    ("Attendance.HolidayNameInvalid", "name", "AddHolidayCommand", "Name"),
    ("Attendance.WorkingCalendarNameInvalid", "name", "CreateWorkingCalendarCommand", "Name"),
    ("Attendance.LeaveTypeCodeInvalid", "code", "CreateLeaveTypeCommand", "Code"),
    ("Attendance.LeaveTypeNameInvalid", "name", "CreateLeaveTypeCommand", "Name"),
    ("Attendance.LeaveDecisionNoteInvalid", "decisionNote", "DecideLeaveRequestCommand", "DecisionNote"),
    ("Attendance.PeriodNameInvalid", "name", "CreateAttendancePeriodCommand", "Name"),
    ("Attendance.RecordNoteInvalid", "note", "RecordAttendanceCommand", "Note"),
    ("Attendance.OvertimeTierInvalid", "overtimeTier", "RecordAttendanceCommand", "OvertimeTier"),
    ("Gl.AccountCodeInvalid", "code", "CreateAccountCommand", "Code"),
    ("Gl.AccountNameInvalid", "name", "CreateAccountCommand", "Name"),
    ("Gl.FiscalYearCodeInvalid", "code", "DefineFiscalYearCommand", "Code"),
    ("Gl.JournalDescriptionInvalid", "description", "CreateJournalDraftCommand", "Description"),
    ("Gl.JournalReferenceInvalid", "reference", "CreateJournalDraftCommand", "Reference"),
    ("Payroll.PayElementCodeInvalid", "code", "CreatePayElementCommand", "Code"),
    ("Payroll.PayElementNameInvalid", "name", "CreatePayElementCommand", "Name"),
    ("Payroll.PeriodNameInvalid", "name", "GeneratePayrollPeriodCommand", "Name"),
  ];

  // ---- DIRECTION ONE: every mapped field still exists on the DTO that carries it.
  [Fact]
  public void Every_attributed_field_names_a_property_that_still_exists_on_its_request()
  {
    var commands = ApplicationTypes();

    // ⚠ The floor reads the quantity the assertion reads: DTO types discovered, which is what the lookup
    // below searches. A walk that found none would make every "missing" message a lie about the map.
    Assert.True(commands.Length >= 50,
      $"only {commands.Length} command/query types were discovered across the Application assemblies; the " +
      "walk has degraded and every failure below would blame the map for a broken scan.");

    var broken = new List<string>();
    foreach (var (code, field, dto, property) in Attribution)
    {
      var type = commands.FirstOrDefault(candidate =>
        string.Equals(candidate.Name, dto, StringComparison.Ordinal));

      if (type is null)
      {
        broken.Add($"{code}: no type named {dto}");
        continue;
      }

      var member = type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
      if (member is null)
      {
        broken.Add($"{code}: {dto} has no property {property}");
        continue;
      }

      // The field is the SERIALIZED name, which is the property camel-cased. Asserted rather than assumed,
      // because that convention is the one thing standing between a domain constant and a form binding.
      var expected = char.ToLowerInvariant(member.Name[0]) + member.Name[1..];
      if (!string.Equals(field, expected, StringComparison.Ordinal))
      {
        broken.Add($"{code}: field \"{field}\" but {dto}.{member.Name} serializes as \"{expected}\"");
      }
    }

    Assert.True(broken.Count == 0,
      "a field attribution no longer matches the request that carries it, so a form would mark nothing or " +
      "mark the wrong input:\n  " + string.Join("\n  ", broken));
  }

  // ---- ⚠ DIRECTION TWO: no error carries a field that this map does not know about.
  //
  // This is the direction whose absence would be silent. Direction one only checks rows that EXIST; a code
  // that gains a `Field` with no row is simply never examined, and the suite stays green while an
  // unverified serialized name ships.
  [Fact]
  public void Every_error_that_carries_a_field_has_a_row_in_the_map_above()
  {
    var carrying = DeclaredErrors()
      .Where(error => error.Field is not null)
      .ToArray();

    Assert.True(carrying.Length >= 16,
      $"only {carrying.Length} errors carrying a field were discovered; sixteen set one, so the reflection " +
      "walk has degraded and 'all are mapped' would be a statement about nothing.");

    var unmapped = carrying
      .Select(error => error.Code)
      .Except(Attribution.Select(row => row.Code), StringComparer.Ordinal)
      .OrderBy(code => code, StringComparer.Ordinal)
      .ToArray();

    Assert.True(unmapped.Length == 0,
      "an Error names an input but nothing verifies that the name is real:\n  " +
      string.Join("\n  ", unmapped) +
      "\n\nAdd a row to `Attribution` naming the request DTO and property it refers to.");
  }

  // ---- AND THE MAP DESCRIBES ONLY ERRORS THAT EXIST.
  //
  // The reverse of direction two: a row whose code was deleted or renamed would sit here forever, passing
  // direction one against a DTO nobody raises an error from any more.
  [Fact]
  public void Every_row_in_the_map_refers_to_an_error_that_still_carries_a_field()
  {
    var carrying = DeclaredErrors()
      .Where(error => error.Field is not null)
      .Select(error => error.Code)
      .ToHashSet(StringComparer.Ordinal);

    var stale = Attribution
      .Select(row => row.Code)
      .Where(code => !carrying.Contains(code))
      .ToArray();

    Assert.True(stale.Length == 0,
      "a row maps a code that no longer carries a field, so it is verifying nothing:\n  " +
      string.Join("\n  ", stale));
  }

  private static Error[] DeclaredErrors() =>
    [.. DomainAssemblies()
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.Name.EndsWith("Errors", StringComparison.Ordinal))
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
      .Where(field => field.FieldType == typeof(Error))
      .Select(field => (Error)field.GetValue(null)!)
      .DistinctBy(error => error.Code, StringComparer.Ordinal)];

  private static Type[] ApplicationTypes() =>
    [.. LoadedAssemblies("SSAS.*.Application.dll").SelectMany(assembly => assembly.GetTypes())];

  private static Assembly[] DomainAssemblies() => LoadedAssemblies("SSAS.*.Domain.dll");

  private static Assembly[] LoadedAssemblies(string pattern) =>
    [.. Directory
      .EnumerateFiles(AppContext.BaseDirectory, pattern)
      // `RepositoryPaths.ProjectName` rather than `Path.GetFileNameWithoutExtension`, which
      // `RepositoryPathPortabilityTests` bans outright in this project.
      .Select(RepositoryPaths.ProjectName)
      .Where(name => name is not null)
      .Distinct(StringComparer.Ordinal)
      .Select(name => Assembly.Load(name!))];
}
