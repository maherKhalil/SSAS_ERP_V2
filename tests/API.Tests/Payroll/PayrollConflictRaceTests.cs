using System.Net;
using SSAS.Payroll.Application.Permissions;

namespace SSAS.API.Tests.Payroll;

// ==================================================================================================
// PAYROLL'S THREE UNIQUENESS RACES ANSWER 409, NOT 500 (T-178).
// ==================================================================================================
//
// Each handler pre-checks with a read, so two callers can pass with the same value and both reach the save.
// The unique index decides it at commit — and the loser reached `PayrollApiErrorMapper` with an unmapped
// `Persistence.UniqueConstraint`, answered 500, while each module's own conflict code sat mapped to 409 and
// unreturned on that path.
//
// ---- ⚠ ALL THREE ARE THE SAME SHAPE, AND IT IS NOT THE ONE THE JOURNAL NUMBER HAS.
//
// **The race and the pre-check produce an identical caller-visible condition**, so retrying the identical
// request fails again — the caller must change the input. A lost journal-number race, by contrast, is
// satisfied by a retry, which allocates a new number. Same 409, opposite instruction.
//
// ---- ⚠ AND THESE TESTS PROVE THE MAPPING, NOT THE RACE. THE NAMES SAY SO NOW (T-193).
//
// The race above is real and is why the mapping matters. **No race happens here.** Each test sets a
// persistence failure on the unit of work and asserts what the mapper does with it — which is worth having
// and is the whole defect T-178 fixed.
//
// They were called `A_duplicate_..._race_is_409_rather_than_500`, and that name is what a reader sees in a
// failure report and in a green suite. **It would have been read as the concurrency being covered**, when
// what is covered is the answer given once the database has already decided. Renamed to say the second
// thing, because a test name is a stronger claim than a comment, not a weaker one.
public sealed class PayrollConflictRaceTests(PayrollApiTestHost host) : IClassFixture<PayrollApiTestHost>
{
  private static readonly SSAS.BuildingBlocks.Domain.Error UniqueViolation =
    new("Persistence.UniqueConstraint", "Unique index violated.");

  [Fact]
  [Trait("Decision", "DEC-DEP-0027")]
  public async Task A_persistence_conflict_on_pay_element_create_maps_to_409_rather_than_500()
  {
    host.ResetToAuthorizedState();
    host.UnitOfWork.Failure = UniqueViolation;

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, "/api/payroll/elements", host.TokenWith(PayrollPermissionNames.ManageElements),
      """{"code":"BASIC","name":"Basic Salary","kind":"Earning","behaviour":"BaseSalary","defaultRateOrAmount":0,"calculationOrder":0}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  [Fact]
  [Trait("Decision", "DEC-DEP-0027")]
  public async Task A_persistence_conflict_on_payroll_period_create_maps_to_409_rather_than_500()
  {
    host.ResetToAuthorizedState();
    host.UnitOfWork.Failure = UniqueViolation;

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, "/api/payroll/periods", host.TokenWith(PayrollPermissionNames.ManageRuns),
      """{"companyId":"22222222-2222-2222-2222-222222222222","anyDateInPeriodUtc":"2026-01-15T00:00:00Z","payDateUtc":"2026-02-05T00:00:00Z"}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }

  // ⚠ The condition here is "an UNREVERSED run exists", not "a run exists". The index is filtered on
  // `[ReversedUtc] IS NULL` and `ExistsForPeriodAsync` filters the same predicate, so reverse-and-rerun
  // stays legal — which it was not before T-112, when the guard matched a run in any state.
  [Fact]
  [Trait("Decision", "DEC-DEP-0027")]
  public async Task A_persistence_conflict_on_payroll_run_create_maps_to_409_rather_than_500()
  {
    host.ResetToAuthorizedState();

    // The period must exist, or the handler refuses before it ever reaches the save.
    var period = SSAS.Payroll.Domain.Runs.PayrollPeriod.CreateAlignedTo(
      PayrollApiTestHost.CompanyA, Guid.NewGuid(), "January 2026",
      new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero)).Value;
    host.Periods.Stored.Add(period);

    host.UnitOfWork.Failure = UniqueViolation;

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, "/api/payroll/runs", host.TokenWith(PayrollPermissionNames.ManageRuns),
      $$"""{"companyId":"{{PayrollApiTestHost.CompanyA}}","payrollPeriodId":"{{period.Id}}"}"""));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
  }
}
