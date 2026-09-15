using System.Reflection;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Domain.Positions;

namespace SSAS.Architecture.Tests;

// ================================================================================================
// NO EMPLOYEE COMPENSATION VALUE LIVES IN HR (`AC-PAY-0003` clause 2, item 234).
// ================================================================================================
//
// ---- ⚠ WHY THIS EXISTS WHEN A GUARD ALREADY DID.
//
// `AC-PAY-0003` clause 2 was guarded only by
// `PositionApplicationArchitectureTests.No_position_command_carries_a_compensation_value_or_headcount`
// — **POSITION MUTATION COMMANDS only.** A future `Employee.BaseSalary` property would pass it: wrong
// type, wrong package, not a command.
//
// ⚠ **And a payroll-side search could never have found it.** A payroll criterion guarded by a test called
// `No_position_command_…` is unreachable from the criterion's own module by any name search. This one is
// named after WHAT IT BANS.
//
// ---- ⚠⚠ THE CRITERION WAS CORRECTED BEFORE THIS WAS WRITTEN, AND THAT ORDER MATTERED.
//
// As worded, clause 2 said *no compensation value is stored on any HR table*, which is FALSE: the salary
// band stores three amounts. Establishing which artefact was narrower produced the answer that the
// SPECIFICATION was — the whole HR domain declares `decimal` in one file — and the criterion now says
// **employee** compensation, with the band named as the ruled exception.
//
// **A criterion false as read is worse than one that is silent**: a test written to the letter fails
// against the salary band and gets "fixed" by deleting it.
//
// ---- WHAT THE LINE IS.
//
// `DEC-POS-0023`: the amounts live on the salary grade and nowhere else. A band is what a JOB pays; an
// employee's compensation is what a PERSON is paid, and that lives in Payroll. `SalaryGradeId` and
// `JobGradeId` are structural POINTERS and are not values.
public sealed class HrCompensationAbsenceTests
{
  private static readonly Assembly HrDomainAssembly = typeof(Employee).Assembly;

  // The money words. `int` is deliberately NOT banned: `RowCount`, `ByteCount` and `RankOrder` are
  // ordinals and counts, and banning the type would forbid them to prove nothing about pay.
  private static readonly string[] MoneyWords =
    ["Amount", "Salary", "Wage", "Pay", "Rate", "Remuneration", "Compensation"];

  // ---- ⚠ A POINTER IS NOT A VALUE, AND MATCHING ON "Salary" ALONE FORBIDS THE POINTER.
  //
  // `DEC-POS-0023` permits a structural REFERENCE to a grade and forbids an AMOUNT. The first version of
  // this guard listed `SalaryGradeId` and `JobGradeId` by name and immediately failed on
  // `JobGradeUpdated.NewSalaryGradeId` and `.PreviousSalaryGradeId` -- the same two pointers under
  // different prefixes.
  //
  // ⚠⚠ **`No_position_command_carries_a_compensation_value_or_headcount` RECORDS THE IDENTICAL FAILURE:
  // *"Matching on Salary alone would forbid the pointer and prove the wrong thing, which is how this
  // guard first failed."*** **The written diagnosis did not stop the same mistake one package over --
  // naming a failure mode confers no immunity to it.**
  //
  // The rule rather than the list: a name ending in `Id` is a REFERENCE. ⚠ It is applied only to
  // non-`decimal` properties, so a `decimal` called `SomethingId` is still caught -- the exemption is for
  // pointers, not for anything that spells itself like one.
  private static bool IsStructuralPointer(PropertyInfo property) =>
    property.PropertyType != typeof(decimal) &&
    property.PropertyType != typeof(decimal?) &&
    property.Name.EndsWith("Id", StringComparison.Ordinal);

  [Fact]
  [Trait("Criterion", "AC-PAY-0003")]
  [Trait("Decision", "DEC-POS-0023")]
  public void No_employee_compensation_value_is_declared_in_hr()
  {
    var properties = HrDomainAssembly.GetTypes()
      .Where(type => type.IsClass && !type.IsNested)
      .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => new { Type = type, Property = property }))
      .Where(candidate => !IsStructuralPointer(candidate.Property))
      .Where(candidate =>
        candidate.Property.PropertyType == typeof(decimal) ||
        candidate.Property.PropertyType == typeof(decimal?) ||
        MoneyWords.Any(word => candidate.Property.Name.Contains(word, StringComparison.Ordinal)))
      .ToArray();

    // ⚠ THE ANTI-VACUITY FLOOR. Four links stand between the assembly and the offender list -- types
    // loaded, class, public instance properties, the money predicate -- and the ban below is satisfied if
    // ANY of them stops matching. Without this the test passes loudest when it is judging nothing.
    Assert.True(properties.Length >= 3,
      $"only {properties.Length} monetary properties were found across the HR domain; the reflection " +
      "chain has stopped matching and the ban below would judge nothing.");

    // ⚠⚠ AND THE STRONGER CONTROL: THE RULED EXCEPTION MUST ACTUALLY BE FOUND.
    //
    // A floor alone passes if the walk finds three properties on the wrong type. Naming the three the
    // exception covers means the allowance is exercised on every run rather than asserted once and
    // trusted -- and a fourth amount added to the band fails here rather than slipping through as
    // "already allowed".
    var band = properties
      .Where(candidate => candidate.Type == typeof(SalaryBand))
      .Select(candidate => candidate.Property.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(["MaximumAmount", "MidpointAmount", "MinimumAmount"], band);

    var offenders = properties
      .Where(candidate => candidate.Type != typeof(SalaryBand))
      .Select(candidate => candidate.Type.Name + "." + candidate.Property.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      offenders.Length == 0,
      "These HR domain types declare a monetary property. `AC-PAY-0003` allows compensation values in " +
      "Payroll and, by `DEC-POS-0023`, on the salary BAND -- what a job pays, not what a person is paid. " +
      "An employee compensation value in HR is the defect the criterion exists to prevent: " +
      string.Join(", ", offenders));
  }
}
