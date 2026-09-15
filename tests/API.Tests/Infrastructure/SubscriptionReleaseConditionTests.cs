using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE RELEASE CONDITION: A GATED ESTATE MUST NOT SHIP WITHOUT THE SEED (AC-SUB-0047, REQ-SUB-0011).
// ==================================================================================================
//
// *"**RELEASE CONDITION.** No release leaves a tenant reachable by a gated route with no subscription
// record — otherwise that deployment locks out the entire estate. **The gate (T-040) and the trial seed
// (T-041) ship in the SAME release, and the seed must not precede the gate within it.**"*
//
// ---- ⚠⚠⚠ THE DANGEROUS SIDE OF THE CONJUNCTION IS THE **SEED**, AND NOTHING GATED ASSERTED IT.
//
// `ModuleEnablementCoverageTests.Every_module_owned_endpoint_is_gated` (cited to `AC-SUB-0020`) asserts the
// GATE is present, unconditionally. **That points at the other half.** *A release with the gate and without
// the seed leaves every tenant unentitled and every module route refusing* — which is the estate-wide
// lockout this criterion exists to prevent.
//
// ***AN ENTAILMENT THAT RUNS THE WRONG WAY IS INVISIBLE PRECISELY BECAUSE BOTH STATEMENTS ARE ABOUT THE
// SAME TWO OBJECTS.*** I nearly dispositioned this criterion as already-covered on exactly that mistake.
//
// ---- ⚠⚠⚠ WHAT THIS CITATION COVERS, AND WHAT IT DOES NOT. READ BEFORE TRUSTING IT.
//
//     COVERED      the CONJUNCTION — the trial seed migration is present AND module routes are gated.
//     NOT COVERED  the ORDERING clause, *"the seed must not precede the gate"*.
//
// **The criterion's headline sentence describes an ordering that was SUPERSEDED.** Its own amendment says
// so, in a tail that is easy to miss — the row is 750 characters and the interesting half is at the end:
//
//   *"(Amended 2026-08-26 — the original clause read "the enablement gate is not active in the release that
//   introduces the migration", which held the gate back from the migration's release. The order actually
//   taken inverts that: **the gate shipped first and the seed follows.** The condition is unchanged and the
//   ordering clause is not...)"*
//
// ⚠⚠ **AND THE ORDERING CANNOT BE ASSERTED HERE EVEN IF WE WANTED IT: *THE GATE HAS NO MIGRATION.*** It is
// code — `RequireModule`, applied to route groups — while the seed is `20260826112310_AddTrialSubscriptionSeed`.
// **One operand of the ordering has a timestamp and the other does not**, so there is no pair to compare.
// The two migrations that DO exist are the commercial plane and the seed, which is a different ordering
// than the one the criterion names.
//
// ***SO THE ORDERING CLAUSE IS SATISFIED BY HISTORY AND IS NOT INDEPENDENTLY FALSIFIABLE NOW: once both
// things exist, no future change can make the seed have preceded the gate.*** A test asserting it would
// guard against renumbering and nothing else. **Stated here rather than left for a reader to assume the
// citation covers the sentence they can see.**
[Collection(HostIntegrationTestGroup.Name)]
public sealed class SubscriptionReleaseConditionTests(HostWebApplicationFactory factory)
{
  private const string SeedMigration = "20260826112310_AddTrialSubscriptionSeed";

  [Fact]
  [Trait("Criterion", "AC-SUB-0047")]
  public void The_trial_seed_ships_alongside_the_gate_so_no_release_leaves_the_estate_locked_out()
  {
    // ---- HALF ONE: THE SEED MIGRATION IS PRESENT. THIS IS THE HALF NOTHING ELSE ASSERTS.
    var migrations = typeof(PlatformDbContext).Assembly.GetTypes()
      .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
      .Select(type => type.GetCustomAttribute<MigrationAttribute>()?.Id)
      .Where(id => id is not null)
      .ToArray();

    // CONTROL: the reflection must have found a real migration stream. Without this, an assembly that
    // yielded nothing would make the membership check below a claim about an empty set — and it would fail
    // in the reassuring direction only because `Assert.Contains` would then fail loudly. Binding the count
    // states the population instead of hoping.
    Assert.True(migrations.Length >= 5,
      $"only {migrations.Length} migrations found by reflection; the scan collapsed and the assertion " +
      "below would be about nothing.");

    Assert.Contains(SeedMigration, migrations);

    // ---- HALF TWO: THE GATE IS LIVE ON THE MODULE PLANE.
    //
    // ⚠ REDUNDANT FOR DETECTION and present on purpose. `Every_module_owned_endpoint_is_gated` already
    // asserts this unconditionally and would catch a removal first. **It is here because AC-SUB-0047 is a
    // CONJUNCTION**, and a citation that asserts one conjunct while naming both is the misleading tier —
    // a reader would have no way to see which half was checked.
    var gated = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
      .Count(endpoint => endpoint.Metadata.GetMetadata<ModuleEnablementMetadata>() is not null);

    Assert.True(gated > 0,
      "no endpoint carries module-enablement metadata; the gate is not applied anywhere, so the seed " +
      "asserted above is protecting an estate that is not gated at all.");
  }
}
