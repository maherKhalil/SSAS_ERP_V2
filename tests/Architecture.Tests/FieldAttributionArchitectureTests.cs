using System.Reflection;
using System.Text.Json.Serialization;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY `Error.Field` NAMES A PROPERTY THE WIRE CONTRACT ACTUALLY DECLARES (T-269, corrected T-270).
// ==================================================================================================
//
// 129 domain codes collapse into the single wire code `request.invalid`. The message travels, so a human
// can read what went wrong — but **a form cannot: it must know which input to mark, and it should not be
// parsing prose to find out.** `Field` is the machine-readable half.
//
// ---- ⚠ THIS VERIFIED THE WRONG TYPE AND AGREED BY LUCK.
//
// T-269 checked each field against the **Application command** and camel-cased the property name to derive
// the serialized one. That agreed for all sixteen rows — **by coincidence, not by construction.** The
// serialized name is not derived at all: every request record declares it outright with
// `[property: JsonPropertyName("...")]`, and the endpoint maps that record onto the command. The two can
// diverge at any time and the old check would not have noticed.
//
// **A guard that gets the right answer from the wrong type is worse than a missing guard, because it reads
// as coverage.** It now verifies against the transport record and reads the declared name rather than
// inventing one.
//
// That the declaration is what matters is not a style preference here — the header of
// `AttendanceTransportContracts` records two shipped defects caused by its absence: GL once shipped request
// records with no `JsonPropertyName` and **every GL write route answered 400** while routes, handlers,
// domain and mapper were all correct.
//
// ---- AND THE GUARD RUNS IN THREE DIRECTIONS, BECAUSE ONE ALONE IS SILENTLY PARTIAL.
//
// A map verified by reflection catches a **renamed property**. It cannot catch a **missing row**: a code
// gains a `Field`, nobody adds an entry, and a one-directional guard never checks it — green, and quieter
// than before. Nor a **stale row**, whose code stopped carrying a field and which now verifies nothing
// while looking like coverage.
public sealed class FieldAttributionArchitectureTests
{
  // code -> the serialized field, the REQUEST RECORD that declares it, and the property carrying it.
  //
  // The record is named because a wrong one fails the declaration check below — which is what reduces the
  // manual judgement to "which request carries this input", verifiable by eye against the contract file.
  private static readonly (string Code, string Field, string Request, string Property)[] Attribution =
  [
    // ---- Attendance
    ("Attendance.HolidayNameInvalid", "name", "AddHolidayRequest", "Name"),
    ("Attendance.WorkingCalendarNameInvalid", "name", "CreateWorkingCalendarRequest", "Name"),
    ("Attendance.WorkingCalendarCompanyRequired", "companyId", "CreateWorkingCalendarRequest", "CompanyId"),
    ("Attendance.WeekendPatternInvalid", "weekendDays", "CreateWorkingCalendarRequest", "WeekendDays"),
    ("Attendance.WeekendPatternCoversEveryDay", "weekendDays", "CreateWorkingCalendarRequest", "WeekendDays"),
    ("Attendance.PeriodNameInvalid", "name", "CreateAttendancePeriodRequest", "Name"),
    ("Attendance.PeriodCompanyRequired", "companyId", "CreateAttendancePeriodRequest", "CompanyId"),
    ("Attendance.RecordNoteInvalid", "note", "RecordAttendanceRequest", "Note"),
    ("Attendance.OvertimeTierInvalid", "overtimeTier", "RecordAttendanceRequest", "OvertimeTier"),
    ("Attendance.RecordCompanyRequired", "companyId", "RecordAttendanceRequest", "CompanyId"),
    ("Attendance.RecordEmployeeRequired", "employeeId", "RecordAttendanceRequest", "EmployeeId"),
    ("Attendance.AdjustmentNoteRequired", "note", "AdjustAttendanceRequest", "Note"),
    ("Attendance.LeaveTypeCodeInvalid", "code", "CreateLeaveTypeRequest", "Code"),
    ("Attendance.LeaveTypeNameInvalid", "name", "CreateLeaveTypeRequest", "Name"),
    ("Attendance.LeaveBehaviourInvalid", "behaviour", "CreateLeaveTypeRequest", "Behaviour"),
    ("Attendance.LeaveCompanyRequired", "companyId", "CreateLeaveTypeRequest", "CompanyId"),
    ("Attendance.LeaveDecisionNoteInvalid", "decisionNote", "DecideLeaveRequestRequest", "DecisionNote"),
    ("Attendance.LeaveBalanceYearInvalid", "periodYear", "SetLeaveEntitlementRequest", "PeriodYear"),
    ("Attendance.LeaveEntitlementNegative", "entitlementQuantity", "SetLeaveEntitlementRequest",
      "EntitlementQuantity"),

    // ---- General Ledger
    ("Gl.AccountCodeInvalid", "code", "CreateAccountRequest", "Code"),
    ("Gl.AccountNameInvalid", "name", "CreateAccountRequest", "Name"),
    ("Gl.FiscalYearCodeInvalid", "code", "DefineFiscalYearRequest", "Code"),
    ("Gl.JournalDescriptionInvalid", "description", "CreateJournalDraftRequest", "Description"),
    ("Gl.JournalReferenceInvalid", "reference", "CreateJournalDraftRequest", "Reference"),

    // ---- Payroll
    ("Payroll.PayElementCodeInvalid", "code", "CreatePayElementRequest", "Code"),
    ("Payroll.PayElementNameInvalid", "name", "CreatePayElementRequest", "Name"),
    ("Payroll.PeriodNameInvalid", "name", "GeneratePayrollPeriodRequest", "Name"),
  ];

  // ---- DIRECTION ONE: the wire contract still declares the name the field claims.
  [Fact]
  public void Every_attributed_field_matches_a_name_the_request_record_declares()
  {
    var requests = ApiTypes();

    // ⚠ The floor reads the quantity the assertion reads: request types discovered, which is what the
    // lookup below searches. A walk that found none would make every "no such record" message a lie.
    Assert.True(requests.Length >= 40,
      $"only {requests.Length} types were discovered across the module API assemblies; the walk has " +
      "degraded and every failure below would blame the map for a broken scan.");

    var broken = new List<string>();
    foreach (var (code, field, request, property) in Attribution)
    {
      var type = requests.FirstOrDefault(candidate =>
        string.Equals(candidate.Name, request, StringComparison.Ordinal));

      if (type is null)
      {
        broken.Add($"{code}: no request record named {request}");
        continue;
      }

      var member = type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
      if (member is null)
      {
        broken.Add($"{code}: {request} has no property {property}");
        continue;
      }

      // ⚠ THE DECLARED NAME, NOT A CAMEL-CASED GUESS. `StrictRequestReader` deserializes with
      // case-sensitive default options, so the attribute is what a caller must actually send.
      var declared = member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
      if (declared is null)
      {
        broken.Add($"{code}: {request}.{property} declares no JsonPropertyName, so nothing fixes its " +
          "wire name — see the AttendanceTransportContracts header for what that costs");
        continue;
      }

      if (!string.Equals(field, declared, StringComparison.Ordinal))
      {
        broken.Add($"{code}: field \"{field}\" but {request}.{property} is declared as \"{declared}\"");
      }
    }

    Assert.True(broken.Count == 0,
      "a field attribution no longer matches the wire contract that carries it, so a form would mark " +
      "nothing or mark the wrong input:\n  " + string.Join("\n  ", broken));
  }

  // ---- ⚠ DIRECTION TWO: no error carries a field this map does not know about.
  [Fact]
  public void Every_error_that_carries_a_field_has_a_row_in_the_map_above()
  {
    var carrying = DeclaredErrors().Where(error => error.Field is not null).ToArray();

    Assert.True(carrying.Length >= 27,
      $"only {carrying.Length} errors carrying a field were discovered; 27 set one, so the reflection " +
      "walk has degraded and 'all are mapped' would be a statement about nothing.");

    var unmapped = carrying
      .Select(error => error.Code)
      .Except(Attribution.Select(row => row.Code), StringComparer.Ordinal)
      .OrderBy(code => code, StringComparer.Ordinal)
      .ToArray();

    Assert.True(unmapped.Length == 0,
      "an Error names an input but nothing verifies that the name is real:\n  " +
      string.Join("\n  ", unmapped) +
      "\n\nAdd a row naming the request record and property it refers to.");
  }

  // ---- AND THE MAP DESCRIBES ONLY ERRORS THAT EXIST.
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
    [.. LoadedAssemblies("SSAS.*.Domain.dll")
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.Name.EndsWith("Errors", StringComparison.Ordinal))
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
      .Where(field => field.FieldType == typeof(Error))
      .Select(field => (Error)field.GetValue(null)!)
      .DistinctBy(error => error.Code, StringComparer.Ordinal)];

  private static Type[] ApiTypes() =>
    [.. LoadedAssemblies("SSAS.*.API.dll").SelectMany(assembly => assembly.GetTypes())];

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
