using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY `Error.Field` RESOLVES AS A JSON PATH INTO THE REQUEST BODY (T-269, T-270, path form T-272).
// ==================================================================================================
//
// 129 domain codes collapse into the single wire code `request.invalid`. The message travels, so a human
// can read what went wrong — but **a form cannot: it must know which input to mark, and it should not be
// parsing prose to find out.** `Field` is the machine-readable half.
//
// ---- WHY A PATH RATHER THAN A NAME.
//
// A flat name cannot address an element of a collection. `CreateJournalDraftRequest` carries `lines`,
// `RecordCompensationRequest` carries `assignments`, and an error raised inside
// `foreach (var assignment in assignments)` concerns `assignments[].payElementId` — **a property of an
// element, not a property of the body.**
//
// **And that is where attribution is worth MOST, not least.** A caller editing a journal of twenty lines
// needs to know which line far more than a caller with one bad `name` needs the word `name`. The flat form
// failed hardest exactly where it mattered.
//
// ---- ⚠ AND A SINGLE SEGMENT IS ALREADY A VALID PATH, WHICH IS WHY THIS COST NOTHING TO ADOPT.
//
// `name` means today what it meant before. All 38 rows written under the flat form remain valid unchanged;
// this widened the contract rather than changing it.
//
// ---- THE SEMANTICS A CLIENT CAN RELY ON.
//
//   * segments are separated by `.` and name **serialized** properties, never CLR ones
//   * `[]` marks a collection segment: `assignments[].payElementId` is *that property of some element*
//   * **an index appears only when the raising code knows it.** Today no domain guard tracks a loop index,
//     so `[]` is always empty — and an empty `[]` is honest where a fabricated `[0]` would not be
//   * absent entirely means **no single input is at fault** — mark nothing
//
// ---- AND THE GUARD RUNS IN THREE DIRECTIONS, BECAUSE ONE ALONE IS SILENTLY PARTIAL.
//
// A map verified by reflection catches a **renamed property**. It cannot catch a **missing row** — a code
// gains a `Field`, nobody adds an entry, and a one-directional guard never checks it. Nor a **stale row**,
// whose code stopped carrying a field and which now verifies nothing while looking like coverage.
public sealed class FieldAttributionArchitectureTests
{
  // code -> the JSON path, and the request record it is a path INTO.
  //
  // There is no separate property column any more: the path IS the property reference, resolved segment by
  // segment against the declared `JsonPropertyName` of each type along the way. A column restating the last
  // segment could only drift from it.
  private static readonly (string Code, string Field, string Request)[] Attribution =
  [
    // ---- Attendance
    ("Attendance.HolidayNameInvalid", "name", "AddHolidayRequest"),
    ("Attendance.WorkingCalendarNameInvalid", "name", "CreateWorkingCalendarRequest"),
    ("Attendance.WorkingCalendarCompanyRequired", "companyId", "CreateWorkingCalendarRequest"),
    ("Attendance.WeekendPatternInvalid", "weekendDays", "CreateWorkingCalendarRequest"),
    ("Attendance.WeekendPatternCoversEveryDay", "weekendDays", "CreateWorkingCalendarRequest"),
    ("Attendance.PeriodNameInvalid", "name", "CreateAttendancePeriodRequest"),
    ("Attendance.PeriodCompanyRequired", "companyId", "CreateAttendancePeriodRequest"),
    ("Attendance.RecordNoteInvalid", "note", "RecordAttendanceRequest"),
    ("Attendance.OvertimeTierInvalid", "overtimeTier", "RecordAttendanceRequest"),
    ("Attendance.RecordCompanyRequired", "companyId", "RecordAttendanceRequest"),
    ("Attendance.RecordEmployeeRequired", "employeeId", "RecordAttendanceRequest"),
    ("Attendance.AdjustmentNoteRequired", "note", "AdjustAttendanceRequest"),
    ("Attendance.LeaveTypeCodeInvalid", "code", "CreateLeaveTypeRequest"),
    ("Attendance.LeaveTypeNameInvalid", "name", "CreateLeaveTypeRequest"),
    ("Attendance.LeaveBehaviourInvalid", "behaviour", "CreateLeaveTypeRequest"),
    ("Attendance.LeaveCompanyRequired", "companyId", "CreateLeaveTypeRequest"),
    ("Attendance.LeaveDecisionNoteInvalid", "decisionNote", "DecideLeaveRequestRequest"),
    ("Attendance.LeaveBalanceYearInvalid", "periodYear", "SetLeaveEntitlementRequest"),
    ("Attendance.LeaveEntitlementNegative", "entitlementQuantity", "SetLeaveEntitlementRequest"),

    // ---- General Ledger
    ("Gl.AccountCodeInvalid", "code", "CreateAccountRequest"),
    ("Gl.AccountNameInvalid", "name", "CreateAccountRequest"),
    ("Gl.FiscalYearCodeInvalid", "code", "DefineFiscalYearRequest"),
    ("Gl.FiscalYearHasNoPeriods", "periods", "DefineFiscalYearRequest"),
    ("Gl.JournalDescriptionInvalid", "description", "CreateJournalDraftRequest"),
    ("Gl.JournalReferenceInvalid", "reference", "CreateJournalDraftRequest"),

    // ---- Payroll
    ("Payroll.PayElementCodeInvalid", "code", "CreatePayElementRequest"),
    ("Payroll.PayElementNameInvalid", "name", "CreatePayElementRequest"),
    ("Payroll.PayElementAccountRequired", "glAccountId", "CreatePayElementRequest"),
    ("Payroll.PayElementAmountNegative", "defaultRateOrAmount", "CreatePayElementRequest"),
    ("Payroll.PayElementCalculationOrderInvalid", "calculationOrder", "CreatePayElementRequest"),
    ("Payroll.PeriodNameInvalid", "name", "GeneratePayrollPeriodRequest"),
    ("Payroll.PeriodCompanyRequired", "companyId", "GeneratePayrollPeriodRequest"),
    ("Payroll.RunCompanyRequired", "companyId", "CreatePayrollRunRequest"),
    ("Payroll.RunPeriodRequired", "payrollPeriodId", "CreatePayrollRunRequest"),
    ("Payroll.OneOffPaymentAmountNotPositive", "amount", "RecordOneOffPaymentRequest"),
    ("Payroll.OneOffPaymentCompanyRequired", "companyId", "RecordOneOffPaymentRequest"),
    ("Payroll.OneOffPaymentPayElementRequired", "payElementId", "RecordOneOffPaymentRequest"),
    ("Payroll.OneOffPaymentPeriodRequired", "payrollPeriodId", "RecordOneOffPaymentRequest"),

    // ---- Company
    ("Company.InvalidCode", "companyCode", "CreateCompanyRequest"),
    ("Company.InvalidName", "companyName", "CreateCompanyRequest"),
    ("Company.InvalidBaseCurrency", "baseCurrencyCode", "CreateCompanyRequest"),
    ("Company.InvalidTransitionReason", "reasonCode", "CompanyLifecycleRequest"),

    // ---- HR: Department
    ("Department.InvalidCode", "code", "CreateDepartmentRequest"),
    ("Department.InvalidName", "name", "CreateDepartmentRequest"),

    // ---- HR: Employee
    ("Employee.InvalidEmployeeNumber", "employeeNumber", "CreateEmployeeRequest"),
    ("Employee.InvalidFullName", "fullName", "CreateEmployeeRequest"),
    ("Employee.InvalidNationalId", "nationalId", "CreateEmployeeRequest"),
    ("Employee.InvalidEmploymentDate", "employmentDate", "CreateEmployeeRequest"),
    ("Employee.TerminationBeforeEmployment", "terminationDate", "TerminateEmployeeRequest"),
    ("Employee.InvalidTransitionReason", "reasonCode", "EmployeeLifecycleRequest"),
    ("Employee.DepartmentUnchanged", "departmentId", "ChangeEmployeeDepartmentRequest"),
    ("Employee.PositionUnchanged", "positionId", "ChangeEmployeePositionRequest"),
    ("Employee.TransferDestinationUnchanged", "destinationBranchId", "TransferEmployeeRequest"),

    // ---- HR: Position
    ("Position.InvalidCode", "code", "CreatePositionRequest"),
    ("Position.InvalidTitle", "title", "CreatePositionRequest"),
    ("Position.InvalidJobGradeCode", "code", "CreateJobGradeRequest"),
    ("Position.InvalidJobGradeName", "name", "CreateJobGradeRequest"),
    ("Position.InvalidRankOrder", "rankOrder", "CreateJobGradeRequest"),
    ("Position.InvalidGradeReference", "salaryGradeId", "CreateJobGradeRequest"),
    ("Position.InvalidSalaryGradeCode", "code", "CreateSalaryGradeRequest"),
    ("Position.InvalidSalaryGradeName", "name", "CreateSalaryGradeRequest"),

    // ---- Payroll, element-level: the two paths the flat form could not express at all
    ("Payroll.CompensationAssignmentElementRequired", "assignments[].payElementId",
      "RecordCompensationRequest"),
    ("Payroll.CompensationAssignmentAmountNegative", "assignments[].rateOrAmount",
      "RecordCompensationRequest"),
  ];

  // ---- DIRECTION ONE: every path still resolves against the wire contract.
  [Fact]
  public void Every_attributed_field_resolves_as_a_path_into_its_request_record()
  {
    var requests = ApiTypes();

    // ⚠ The floor reads the quantity the assertion reads: request types discovered, which is what the
    // lookup below searches. A walk that found none would make every "no such record" message a lie.
    Assert.True(requests.Length >= 40,
      $"only {requests.Length} types were discovered across the module API assemblies; the walk has " +
      "degraded and every failure below would blame the map for a broken scan.");

    var broken = new List<string>();
    foreach (var (code, field, request) in Attribution)
    {
      var type = requests.FirstOrDefault(candidate =>
        string.Equals(candidate.Name, request, StringComparison.Ordinal));

      if (type is null)
      {
        broken.Add($"{code}: no request record named {request}");
        continue;
      }

      if (Resolve(type, field) is { } failure)
      {
        broken.Add($"{code}: {failure}");
      }
    }

    Assert.True(broken.Count == 0,
      "a field no longer resolves against the wire contract that carries it, so a form would mark " +
      "nothing or mark the wrong input:\n  " + string.Join("\n  ", broken));
  }

  // Walks the path one segment at a time. Returns null when it resolves, or the reason it did not.
  //
  // ⚠ Each segment is matched on the DECLARED `JsonPropertyName`, never on the CLR name.
  // `StrictRequestReader` deserializes with case-sensitive default options, so the attribute is what a
  // caller must actually send — and a CLR-name match would agree with it only by convention.
  private static string? Resolve(Type request, string path)
  {
    var current = request;
    foreach (var raw in path.Split('.'))
    {
      var collection = raw.EndsWith(']');
      var segment = collection ? raw[..raw.IndexOf('[', StringComparison.Ordinal)] : raw;

      var member = current
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .FirstOrDefault(property =>
          string.Equals(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name, segment,
            StringComparison.Ordinal));

      if (member is null)
      {
        return $"{current.Name} declares no JsonPropertyName \"{segment}\" (from path \"{path}\")";
      }

      if (!collection)
      {
        current = member.PropertyType;
        continue;
      }

      if (ElementTypeOf(member.PropertyType) is not { } element)
      {
        return $"{current.Name}.{segment} is marked as a collection in \"{path}\" but is " +
          $"{member.PropertyType.Name}, which is not one";
      }

      current = element;
    }

    return null;
  }

  private static Type? ElementTypeOf(Type type)
  {
    if (type.IsArray)
    {
      return type.GetElementType();
    }

    if (!typeof(IEnumerable).IsAssignableFrom(type) || type == typeof(string))
    {
      return null;
    }

    return type.IsGenericType ? type.GetGenericArguments().FirstOrDefault() : null;
  }

  // ---- ⚠ DIRECTION TWO: no error carries a field this map does not know about.
  [Fact]
  public void Every_error_that_carries_a_field_has_a_row_in_the_map_above()
  {
    var carrying = DeclaredErrors().Where(error => error.Field is not null).ToArray();

    Assert.True(carrying.Length >= 63,
      $"only {carrying.Length} errors carrying a field were discovered; 63 set one, so the reflection " +
      "walk has degraded and 'all are mapped' would be a statement about nothing.");

    var unmapped = carrying
      .Select(error => error.Code)
      .Except(Attribution.Select(row => row.Code), StringComparer.Ordinal)
      .OrderBy(code => code, StringComparer.Ordinal)
      .ToArray();

    Assert.True(unmapped.Length == 0,
      "an Error names an input but nothing verifies that the name is real:\n  " +
      string.Join("\n  ", unmapped) +
      "\n\nAdd a row naming the request record its path runs into.");
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
