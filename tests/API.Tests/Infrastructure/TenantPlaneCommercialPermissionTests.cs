using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Tenancy.Permissions;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// NO TENANT-PLANE PERMISSION NAMES SUBSCRIPTION ADMINISTRATION (AC-SUB-0008, REQ-SUB-0004).
// ==================================================================================================
//
// *"No tenant-plane permission name for subscription administration exists in the composed catalog. The
// criterion is the absence — there is nothing to grant by mistake."*
//
// ---- ⚠⚠⚠ READ THIS BEFORE YOU TAKE THE CITATION AT FACE VALUE. **PLANE SEPARATION IS NOT ENFORCED.**
//
// The criterion says so itself, and the sentence is carried here rather than left in the spec, because a
// reader who sees `AC-SUB-0008` cited will otherwise believe something stronger than what holds:
//
//   ⚠ *"Satisfied vacuously as at 2026-08-30: the package defines no subscription permissions on **either**
//   plane (all 28 platform names enumerated), **so this is met by there being nothing to separate rather
//   than by the separation being implemented**"*
//
// ***SO THIS TEST WITNESSES AN ABSENCE THAT HOLDS FOR A REASON UNRELATED TO ANY SEPARATION MECHANISM.***
// **The day a subscription permission is defined on the platform plane, this test keeps passing and only
// then begins testing actual separation.** *A true citation that leaves a reader with a false belief is the
// MISLEADING tier, which outranks silent — hence the paragraph.*
//
// ---- ⚠⚠ WHY THIS IS A WITNESS AND NOT A TRIPWIRE, WHICH TURNS ON THE CRITERION'S GRAMMAR.
//
//     A UNIVERSAL OVER AN EMPTY SET   "all subscription permissions are platform-plane"  → UNFALSIFIABLE.
//     A NEGATIVE EXISTENTIAL          **"NO tenant-plane permission name ... EXISTS"**   → **fails the day one appears.**
//
// **0008 is the second.** *The complement being empty is what makes a universal worthless, and is exactly
// what a negative existential asserts.* So the criterion's own word "vacuously" is loose: its real content
// is *the separation is not implemented, there is merely nothing to separate* — a statement about WHY it
// holds, not about whether a violation would be detectable. **A guard asserting the absence IS the witness
// here**, which is the opposite of `SubscriptionInvariantTests`' tripwires, where the criteria assert
// positive behaviour and the guards assert their subjects are missing.
//
// ---- ⚠⚠⚠ AND THE TRAP THAT NEARLY MADE THIS THE WRONG TEST: **SCOPE DOES NOT SEPARATE THE PLANES.**
//
// The obvious reading of "tenant-plane" is `PermissionScope.Tenant`, and it is WRONG. The enum has two
// members — `Tenant` and `PlatformSupport` — and `PlatformPermissionCatalog` stamps only **four** of the 28
// platform names as `PlatformSupport`. ***THE OTHER TWENTY-FOUR PLATFORM NAMES ARE SCOPED `Tenant`.***
//
// **A scope filter would therefore sweep in most of the platform plane, and a subscription permission added
// to `PlatformPermissionNames` would redden this test** — which would make it a second copy of the
// `AC-SUB-0034`/`0035` tripwire asserting *no subscription permission anywhere*, a strictly stronger and
// different claim than the criterion makes.
//
// ***THE CRITERION'S OWN TEXT SETTLES WHAT "TENANT-PLANE" MEANS: it contrasts the tenant plane against
// "all 28 platform names", so the tenant plane is WHAT THE MODULES CONTRIBUTE.*** This reads the
// contributors, never the scope, and the plant results below are what establish that it does.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class TenantPlaneCommercialPermissionTests(HostWebApplicationFactory factory)
{
  // Vocabulary a subscription-administration permission would have to use to be grantable by mistake —
  // which is the criterion's own rationale: *there is nothing to grant by mistake*. A name nobody would
  // recognise as commercial cannot be granted as commercial by accident.
  private static readonly string[] Commercial =
    ["subscription", "plan", "billing", "invoice", "entitlement", "payment", "seat"];

  // ---- ⚠⚠⚠ THE TWO PLANTS THAT DECIDED THE TRAIT KEY. RUN BEFORE TAGGING, NOT AFTER.
  //
  // The key turns on WHICH PLANE the guard reads, and that is not decidable by reading the code — a scope
  // filter looks tenant-shaped and is not. **So both plants ran first and the key is their output:**
  //
  //     TENANT-PLANE PLANT     a subscription permission contributed by GL      → **THIS TEST REDDENS.**
  //     PLATFORM-PLANE PLANT   the same name in `PlatformPermissionNames` AND
  //                            `PlatformPermissionCatalog`, so it genuinely
  //                            reaches the composed catalog                     → **THIS TEST STAYS GREEN.**
  //
  // ***THE SECOND IS THE ONE THAT MATTERS AND IT IS THE ONE NOBODY RUNS.*** Had it reddened, this would be
  // asserting *no subscription permission anywhere* — stronger than the criterion, and a duplicate of
  // `SubscriptionInvariantTests`' tripwire rather than a witness for `AC-SUB-0008`.
  //
  // ⚠⚠ ENFORCEMENT SET FOR THE TENANT-PLANE PLANT: **ONE, and it is this test.** All 1,141 Platform tests
  // stayed green — including `PlatformInfrastructureRegistrationTests.No_subscription_permission_exists_on_
  // either_plane`, which despite its name and its comment builds a bare `PlatformPermissionCatalog` and is
  // therefore **blind to anything a module contributes.** *That test guards the platform plane; this one
  // guards the tenant plane; the criterion is about the tenant plane.* **They are complements, not
  // duplicates, and neither carries the other.**
  //
  // ⚠ ONE COLLATERAL RED, NAMED SO IT IS NOT MISTAKEN FOR ENFORCEMENT:
  // `GlArchitectureTests.Every_named_permission_is_defined_by_the_catalog_contributor` also reddened —
  // because the plant used a raw string literal instead of a `GlPermissionNames` constant. **That is a
  // property of how I wrote the plant, not a guard against commercial permissions.** A plant using a proper
  // constant would not have tripped it, and it would not catch a commercially-named constant.
  [Fact]
  [Trait("Criterion", "AC-SUB-0008")]
  public void No_module_contributes_a_permission_name_for_subscription_administration()
  {
    var contributors = factory.Services.GetServices<IPermissionCatalogContributor>().ToArray();

    // ---- THE STRONG HALF: THE EXACT CONTRIBUTOR SET.
    //
    // A tenant-plane commercial permission can only arrive by a module contributing one, so a NEW
    // CONTRIBUTOR is the mechanism this criterion is exposed to. The set is small, and a new module is a
    // major reviewable event rather than routine churn — so an exact set here fires rarely AND on-subject,
    // which is the pair that decides whether a strict guard survives.
    Assert.Equal(
      [
        "AttendancePermissionCatalogContributor",
        "GlPermissionCatalogContributor",
        "HisPermissionCatalogContributor",
        "HrPermissionCatalogContributor",
        "PayrollPermissionCatalogContributor"
      ],
      contributors.Select(contributor => contributor.GetType().Name)
        .OrderBy(name => name, StringComparer.Ordinal));

    var contributed = contributors
      .SelectMany(contributor => contributor.Permissions)
      .Select(permission => permission.Name)
      .ToArray();

    // ---- POSITIVE CONTROL, AND IT NAMES THE POPULATION RATHER THAN ASSERTING IT IS NON-EMPTY.
    //
    // `NotEmpty` would catch "every contributor vanished" — vivid and rare — and miss "one contributor
    // returned nothing", which is dull and common and would make the ban below pass over less than it
    // claims. Binding two known names from two different modules fails on the likelier accident.
    Assert.Contains("GL.Journals.View", contributed, StringComparer.Ordinal);
    Assert.Contains("HR.Employees.View", contributed, StringComparer.Ordinal);

    // ---- THE BAN. WEAKER, PLACED SECOND, AND LABELLED AS A NAME SEARCH.
    //
    // A commercial permission named `Tenant.Commerce.Administer` walks past this. It is matched to the
    // criterion's own rationale rather than to a complete vocabulary: a name that no grantor would read as
    // commercial cannot be granted as commercial by mistake, which is the harm the criterion names.
    var offending = contributed
      .Where(name => Commercial.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
      .ToArray();

    Assert.Empty(offending);
  }
}
