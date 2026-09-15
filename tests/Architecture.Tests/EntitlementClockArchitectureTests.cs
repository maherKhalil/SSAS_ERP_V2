using System.Reflection;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.API.Subscriptions;
using SSAS.Platform.Infrastructure.Subscriptions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// ONE ENTITLEMENT DECISION READS ONE CLOCK (item 185).
// ==================================================================================================
//
// `TenantModuleEntitlement` evaluates expiry through an injected `IDateTimeProvider`.
// `TenantEntitlementReader` used to select the in-force subscription record with a bare
// `DateTimeOffset.UtcNow`, so **half the decision was controllable by a caller and half was not** -- a
// test setting the injected clock would have had the other half silently ignore it.
//
// ⚠ THAT SPLIT IS HOW ITEM 182'S DEFECT CAME TO BE WRITTEN, one seam over: a fixture clock feeding one
// side of a comparison while the other side read wall time. Item 183 found this while surveying for more
// of the same, and it is **prevention rather than a live defect** -- nothing today seeds a future-dated
// record, so nothing currently disagrees.
//
// ---- WHY A STRUCTURAL ASSERTION AND NOT A BEHAVIOURAL ONE.
//
// The behaviour is unchanged: `UtcDateTimeProvider` returns `DateTimeOffset.UtcNow`, so production reads
// the same instant it always did. **There is no new behaviour to assert** -- what changed is that the
// instant became controllable, and a test proving that needs a seeded future-dated record and a real
// `PlatformDbContext`, which lives in `Integration.Tests` and the TASK gate does not run (item 176).
//
// So this pins the SEAM: both halves must take their instant from the same injectable source. It reddens
// if either reverts to reading the clock directly, which is the regression worth catching.
public sealed class EntitlementClockArchitectureTests
{
  [Theory]
  [InlineData(typeof(TenantEntitlementReader))]
  [InlineData(typeof(TenantModuleEntitlement))]
  public void Both_halves_of_the_entitlement_decision_take_an_injectable_clock(Type participant)
  {
    var constructor = participant.GetConstructors().SingleOrDefault();

    // ⚠ A TYPE WITH NO SINGLE PUBLIC CONSTRUCTOR WOULD OTHERWISE PASS OVER NOTHING.
    Assert.True(constructor is not null, $"{participant.Name} has no single public constructor to inspect.");

    Assert.True(
      constructor!.GetParameters().Any(parameter => parameter.ParameterType == typeof(IDateTimeProvider)),
      $"{participant.Name} does not take IDateTimeProvider, so it reads an instant nobody can control. " +
      "The other half of the entitlement decision does take one, and a decision split across two clocks " +
      "is what item 182's defect was made of.");
  }
}
