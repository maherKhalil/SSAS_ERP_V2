using System.Reflection;
using System.Text.Json.Serialization;
using SSAS.Attendance.API;

namespace SSAS.API.Tests.Attendance;

// ================================================================================================
// TS-ATT-0018. THE BINDING GUARD — AND IT IS STRUCTURAL, WHICH IS STRICTLY STRONGER HERE.
// ================================================================================================
//
// `StrictRequestReader.ReadStrictJsonAsync` deserializes with `JsonSerializerOptions.Default`, which is
// **case-sensitive** and reads enums **from numbers only**. Two features have shipped a total, silent defect
// because of it:
//
//   * **FP-011 (GL)** omitted `[property: JsonPropertyName]`. `{"code":"4100"}` never bound, and EVERY GL
//     write route answered `400 request.invalid` while routes, handlers, domain and mapper were all correct.
//
//   * **FP-012 (Payroll)** omitted `[property: JsonConverter(typeof(JsonStringEnumConverter))]`. `"Earning"`
//     could not become a `PayElementKind`, so `POST /api/payroll/elements` refused every well-formed body:
//     no pay element could be created, therefore no payroll could ever be calculated.
//
// Payroll's guard is BEHAVIOURAL — it posts a correctly-cased body at each route and asserts the answer is
// not `request.invalid`. That catches the defect on the routes it enumerates.
//
// **This one is STRUCTURAL, and catches it on every request record that will ever exist in this module** —
// including ones added after this test was written, and including properties on routes nobody thought to
// enumerate. Both faults are ABSENCES, and an absence is exactly what a reflection sweep is good at.
public sealed class AttendanceTransportContractTests
{
  // Derived, not listed. A request record added later is covered without anybody remembering to add it here
  // — which is the whole difference between this and a list of routes.
  private static Type[] RequestRecords() =>
    typeof(CreateWorkingCalendarRequest).Assembly
      .GetTypes()
      .Where(type => type.IsClass && type.IsSealed && type.Name.EndsWith("Request", StringComparison.Ordinal))
      .OrderBy(type => type.Name, StringComparer.Ordinal)
      .ToArray();

  [Fact]
  public void There_are_request_records_to_check_so_this_guard_is_not_vacuous()
  {
    // The failure mode of a reflection guard is finding nothing and passing. FP-012 replaced GL's
    // `There_is_no_gl_contracts_assembly` for exactly this reason, so the sweep asserts it swept something.
    Assert.NotEmpty(RequestRecords());
  }

  [Fact]
  [Trait("Criterion", "AC-ATT-0038")]
  public void Every_request_property_carries_an_explicit_json_property_name()
  {
    var missing = new List<string>();

    foreach (var record in RequestRecords())
    {
      foreach (var property in record.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        if (property.GetCustomAttribute<JsonPropertyNameAttribute>() is null)
        {
          missing.Add($"{record.Name}.{property.Name}");
        }
      }
    }

    Assert.True(
      missing.Count == 0,
      "These request properties have no [property: JsonPropertyName] and will not bind from a " +
      $"correctly-cased body — the FP-011 defect: {string.Join(", ", missing)}");
  }

  // ---- AND THE ENUM HALF, WHICH IS THE FP-012 DEFECT.
  //
  // A property-level `[JsonConverter]` is honoured regardless of serializer options, which is why it works
  // where a global option would not. Without it the WHOLE RECORD fails to bind, so a single missing
  // attribute takes down an entire route rather than one field.
  [Fact]
  [Trait("Criterion", "AC-ATT-0038")]
  public void Every_enum_valued_request_property_carries_a_string_enum_converter()
  {
    var missing = new List<string>();

    foreach (var record in RequestRecords())
    {
      foreach (var property in record.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (!type.IsEnum)
        {
          continue;
        }

        var converter = property.GetCustomAttribute<JsonConverterAttribute>();
        if (converter?.ConverterType != typeof(JsonStringEnumConverter))
        {
          missing.Add($"{record.Name}.{property.Name} ({type.Name})");
        }
      }
    }

    Assert.True(
      missing.Count == 0,
      "These enum-valued request properties have no [property: JsonConverter(typeof(JsonStringEnumConverter))]. " +
      "JsonSerializerOptions.Default reads enums from NUMBERS ONLY, so the whole record fails to bind and the " +
      $"route refuses every well-formed body — the FP-012 defect: {string.Join(", ", missing)}");
  }

  // ---- NO REQUEST ACCEPTS A BRANCH (OD-ATT-0011).
  //
  // `AttendanceRecord` is branch-owned and the write boundary stamps `BranchId` from the execution context.
  // A caller who could NAME a branch could record attendance into one they cannot read — which is exactly
  // the boundary the ruling drew.
  [Fact]
  [Trait("Decision", "OD-ATT-0011")]
  public void No_request_accepts_a_branch_identifier()
  {
    foreach (var record in RequestRecords())
    {
      Assert.DoesNotContain(
        record.GetProperties(),
        property => property.Name.Contains("Branch", StringComparison.OrdinalIgnoreCase));
    }
  }

  // ---- NO REQUEST CARRIES A STATUS FIELD.
  //
  // Every transition is a named-action POST with its own permission. A `PUT {status: "closed"}` would let
  // the act that freezes Payroll's inputs arrive through the same door as an ordinary edit.
  [Fact]
  public void No_request_carries_a_status_field()
  {
    foreach (var record in RequestRecords())
    {
      Assert.DoesNotContain(
        record.GetProperties(),
        property => property.Name.Equals("Status", StringComparison.OrdinalIgnoreCase));
    }
  }

  // ---- NO REQUEST CARRIES MONEY (DEC-ATT-0004, AC-ATT-0010).
  //
  // Attendance records quantities. A monetary field on the wire would mean the module boundary had drifted,
  // and the wire is the first place anyone would notice.
  [Fact]
  [Trait("Decision", "DEC-ATT-0004")]
  public void No_request_accepts_money_a_rate_or_a_currency()
  {
    foreach (var record in RequestRecords())
    {
      Assert.DoesNotContain(record.GetProperties(), property =>
        property.Name.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Rate", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Currency", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Multiplier", StringComparison.OrdinalIgnoreCase));
    }
  }

  // ---- THE UPDATE REQUESTS OMIT WHAT IS IMMUTABLE.
  //
  // The absent field IS the rule, made visible in the surface: a caller who sends `code` gets a 400 from the
  // strict reader rather than a silently ignored property.
  [Fact]
  public void The_leave_type_update_request_omits_the_immutable_code_and_behaviour()
  {
    var names = typeof(UpdateLeaveTypeRequest).GetProperties().Select(property => property.Name).ToArray();

    // ⚠ COMPILE-CHECKED AGAINST THE TYPE THAT LEGITIMATELY CARRIES THEM (252). As bare strings these
    // asserted nothing the day someone renamed `Code` on the create request: the update request would not
    // contain the OLD name either, so the test passed while the immutability rule went unchecked.
    Assert.DoesNotContain(nameof(CreateLeaveTypeRequest.Code), names);
    Assert.DoesNotContain(nameof(CreateLeaveTypeRequest.Behaviour), names);
  }

  // ---- A DECISION CANNOT CHANGE WHAT IS BEING DECIDED.
  //
  // Approve and reject carry a note and nothing else. An approver able to alter the dates, the type or the
  // employee at the moment of approving is the same failure `OD-PAY-0009` refused when it gave Payroll's
  // approval route no body at all.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0014")]
  public void A_leave_decision_carries_only_a_note()
  {
    var names = typeof(DecideLeaveRequestRequest).GetProperties().Select(property => property.Name).ToArray();

    Assert.Equal(["DecisionNote"], names);
  }

  // ---- THE ADJUSTMENT REQUEST NAMES NOTHING IT COULD RETARGET.
  //
  // No company, no employee, no date: all three come from the record being corrected, so a caller cannot
  // aim an adjustment at somebody else's record while naming a company they hold.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0019")]
  public void An_adjustment_request_cannot_retarget_the_record_it_corrects()
  {
    var names = typeof(AdjustAttendanceRequest).GetProperties().Select(property => property.Name).ToArray();

    // ⚠ THE WITNESS IS THE SIBLING THAT LEGITIMATELY NAMES ALL THREE (252). `RecordAttendanceRequest`
    // carries company, employee and date; the adjustment must carry none of them. Compile-checking against
    // it makes the two halves of that rule inseparable — rename one and this stops building.
    Assert.DoesNotContain(nameof(RecordAttendanceRequest.CompanyId), names);
    Assert.DoesNotContain(nameof(RecordAttendanceRequest.EmployeeId), names);
    Assert.DoesNotContain(nameof(RecordAttendanceRequest.AttendanceDate), names);
  }

  // ---- THE ATTENDANCE REQUEST NAMES NO PERIOD.
  //
  // The period is RESOLVED from the date. A caller able to name both could name a date and a period that
  // disagree, and the record would sit in a period that does not cover it — invisible until a payroll run
  // read the wrong period's totals.
  [Fact]
  public void A_record_request_names_a_date_and_lets_the_module_resolve_the_period()
  {
    var names = typeof(RecordAttendanceRequest).GetProperties().Select(property => property.Name).ToArray();

    Assert.Contains(nameof(RecordAttendanceRequest.AttendanceDate), names);
    Assert.DoesNotContain(
      nameof(SSAS.Attendance.Application.Periods.CloseAttendancePeriodCommand.AttendancePeriodId), names);
  }
}
