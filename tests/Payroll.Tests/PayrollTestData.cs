using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;

namespace SSAS.Payroll.Tests;

// Builders, so a scenario reads as the thing it asserts rather than as construction. Every factory here
// returns the REAL aggregate through its real factory — nothing fabricates state a handler could not
// produce, which is what keeps these tests about the domain rather than about the fixture.
internal static class PayrollTestData
{
  public static readonly Guid Company = Guid.Parse("11111111-1111-1111-1111-111111111111");

  public static PayElement Element(
    string code,
    PayElementKind kind,
    PayElementBehaviour behaviour,
    decimal defaultRateOrAmount = 0m,
    int order = 0,
    Guid? account = null)
  {
    var element = PayElement.Create(Company, code, code, kind, behaviour, defaultRateOrAmount, order);
    Assert.True(element.IsSuccess, element.IsFailure ? element.Error.Message : string.Empty);

    if (account is { } accountId)
    {
      Assert.True(element.Value.MapToAccount(accountId).IsSuccess);
    }

    return element.Value;
  }

  public static EmployeeCompensation Compensation(
    Guid employeeId,
    DateTimeOffset effectiveFrom,
    decimal baseAmount,
    params (Guid PayElementId, decimal? RateOrAmount)[] assignments)
  {
    var record = EmployeeCompensation.Create(Company, employeeId, effectiveFrom, baseAmount, assignments);
    Assert.True(record.IsSuccess, record.IsFailure ? record.Error.Message : string.Empty);
    return record.Value;
  }

  // A 31-day period so proration arithmetic is legible: one day is 1/31 of the month.
  public static PayrollPeriod Period(
    DateTimeOffset? start = null, DateTimeOffset? end = null, DateTimeOffset? payDate = null)
  {
    var from = start ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var to = end ?? new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    var period = PayrollPeriod.CreateAlignedTo(
      Company, Guid.NewGuid(), "January 2026", from, to,
      payDate ?? new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero));

    Assert.True(period.IsSuccess, period.IsFailure ? period.Error.Message : string.Empty);
    return period.Value;
  }

  public static PayrollRun Run(Guid periodId)
  {
    var run = PayrollRun.Create(Company, periodId);
    Assert.True(run.IsSuccess);
    return run.Value;
  }

  public static PayrollEmployeeInput Employee(
    Guid id, DateTimeOffset hired, DateTimeOffset? terminated, EmployeeCompensation compensation) =>
    new(id, hired, terminated, compensation);
}
