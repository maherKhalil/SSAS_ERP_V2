using SSAS.Attendance.Contracts.Summaries;
using SSAS.GL.Contracts.Posting;
using SSAS.HR.Contracts.Employment;

namespace SSAS.API.Tests.Payroll;

// ==================================================================================================
// THE PAYROLL HOST REACHES NO OTHER MODULE'S IMPLEMENTATION (T-153).
// ==================================================================================================
//
// `PayrollApiTestHost` has claimed since it was written that every route out of Payroll is stubbed, and
// **that claim was carried in a comment that had already gone stale twice** — it said "the TWO cross-module
// contracts" after FP-013 made it three and T-153 made it four.
//
// `DEC-L-002`: no gate reads prose. **This is the same claim, in the only form that can go red.**
public sealed class PayrollHostBoundaryTests(PayrollApiTestHost host) : IClassFixture<PayrollApiTestHost>
{
  private static readonly string[] ForeignModulePrefixes =
    ["SSAS.HR.", "SSAS.GL.", "SSAS.Attendance."];

  [Fact]
  [Trait("Decision", "ADR-012")]
  public void The_payroll_host_registers_no_foreign_module_service()
  {
    // ---- ⚠ IT INSPECTS IMPLEMENTATIONS, NOT SERVICE TYPES, AND THE DIFFERENCE IS THE WHOLE TEST.
    //
    // `IEmployeeRoster` IS an `SSAS.HR.` type and is registered here — **that is legal and is the design.**
    // A contract assembly exists to be depended on. What `ADR-012` forbids is reaching another module's
    // Domain, Application or Infrastructure, so the assertion is on what SATISFIES each registration.
    var foreign = Crossings
      .Select(contract => host.Services.GetService(contract)?.GetType().FullName)
      .Where(implementation => implementation is not null)
      .Where(implementation => ForeignModulePrefixes.Any(prefix =>
        implementation!.StartsWith(prefix, StringComparison.Ordinal)))
      .ToArray();

    Assert.Empty(foreign);
  }

  // ---- AND THE FOUR CROSSINGS ARE ALL SATISFIED FROM THIS TEST ASSEMBLY.
  //
  // The test above proves nothing foreign is registered. **This proves the crossings are actually WIRED** —
  // an unregistered contract would also pass an "is empty" assertion, which is `DEC-L-070`: a green
  // instrument is never evidence about itself.
  [Theory]
  [InlineData(typeof(IEmployeeRoster))]
  [InlineData(typeof(IEmployeeEngagementDirectory))]
  [InlineData(typeof(IJournalPoster))]
  [InlineData(typeof(IAttendanceSummary))]
  public void Every_route_out_of_payroll_is_satisfied_by_a_test_stub(Type contract)
  {
    var resolved = host.Services.GetService(contract);

    Assert.NotNull(resolved);
    Assert.Equal(typeof(PayrollApiTestHost).Assembly, resolved!.GetType().Assembly);
  }

  private static readonly Type[] Crossings =
    [typeof(IEmployeeRoster), typeof(IEmployeeEngagementDirectory),
      typeof(IJournalPoster), typeof(IAttendanceSummary)];
}
