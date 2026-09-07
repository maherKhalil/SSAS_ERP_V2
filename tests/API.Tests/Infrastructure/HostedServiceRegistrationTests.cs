using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SSAS.Host.API.Authentication;
using SSAS.Platform.Infrastructure.Localization;
using SSAS.Platform.Infrastructure.PlatformSupport;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// EVERY STARTUP SERVICE THE PRODUCT OWNS IS ACTUALLY REGISTERED (T-135).
// ==================================================================================================
//
// ---- ⚠⚠⚠ WHY THIS EXISTS: FOUR OF THEM WERE REMOVED AT ONCE AND NOTHING NOTICED.
//
// `AddHostedService` is a collection registration, and a collection's missing member is SILENT — the host
// resolves a shorter list and starts perfectly. **The registrations for `TenantStorageBootstrapHostedService`,
// `PlatformSupportBootstrapHostedService`, `TenantDatabaseBackupSchedulerHostedService` and
// `TenantDatabaseRestoreVerificationHostedService` were deleted TOGETHER and the suites run: API 1005/1005,
// Platform 1159/1159, Architecture 725/725 — 2,889 tests, all green.** `LocalizationCatalogActivationHostedService`
// was planted alone with the same result.
//
// ⚠ **A group plant's green is total**: had any one of the four been guarded, removing it would have reddened
// something, and removing it alongside three others does not un-redden that. *The only escape is a guard
// asserting a RELATION between members — "A is present only if B is" — and no such guard exists here; the six
// are named by six unrelated registration sites in four files.*
//
// **Only `SigningKeyStartupValidator` reddened, and only because `SigningKeyStartupValidatorTests` asserts
// that an unobtainable key kills STARTUP — which can only happen if the service is registered.** *One
// behavioural test, written for a different reason, was the entire coverage of this collection.*
//
// ---- ⚠⚠ THE SET IS OURS ONLY, AND THE FRAMEWORK'S THREE ARE NAMED HERE RATHER THAN ASSERTED.
//
// The host resolves NINE `IHostedService` implementations. Three belong to ASP.NET and are measured, not
// chosen:
//
//     Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService
//     Microsoft.AspNetCore.Hosting.GenericWebHostService
//     Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckPublisherHostedService
//
// **An exact set including those would redden on a package upgrade for no product reason** — a guard whose
// most likely failure is unrelated to its subject teaches its reader to re-baseline it, and a guard that gets
// re-baselined is one nobody reads. *So the assertion is scoped to types this repository owns, and the three
// are written down so a reader knows what was excluded and can tell "the framework added a fourth" from
// "the filter broke".*
//
// ⚠ THE FILTER FAILS SAFE. It selects by the `SSAS.` assembly prefix; if that ever stopped matching, the
// filtered set would be EMPTY and the comparison below would redden rather than pass vacuously.
//
// ---- ⚠⚠⚠ THE LIMIT, AND IT IS THE WHOLE DIFFERENCE BETWEEN THIS FILE AND `SigningKeyStartupValidatorTests`.
//
// ***THIS PROVES EACH SERVICE IS WIRED. IT PROVES NOTHING ABOUT ANY OF THEM RUNNING CORRECTLY.*** A service
// registered and broken passes here. **Asserting that a startup service does its job is a different kind of
// test and costs a file per service** — `SigningKeyStartupValidatorTests` is the one example in the tree, and
// it exists for one of the six. *Do not read a green here as coverage of what these services DO.*
[Collection(HostIntegrationTestGroup.Name)]
public sealed class HostedServiceRegistrationTests(HostWebApplicationFactory factory)
{
  // ---- BOUND BY `typeof`, NOT BY NAME.
  //
  // A string list would go stale silently if a type were renamed or moved namespace; these are compile-time
  // references, so the only way this list can be wrong is if someone deletes a service and this line with it
  // — which is a deliberate act, and the point of the guard is to make the REGISTRATION's disappearance
  // require one.
  private static readonly Type[] Owned =
  [
    typeof(SigningKeyStartupValidator),
    typeof(LocalizationCatalogActivationHostedService),
    typeof(PlatformSupportBootstrapHostedService),
    typeof(TenantDatabaseBackupSchedulerHostedService),
    typeof(TenantDatabaseRestoreVerificationHostedService),
    typeof(TenantStorageBootstrapHostedService),
  ];

  [Fact]
  public void Every_startup_service_this_repository_owns_is_registered_in_the_host()
  {
    var resolved = factory.Services.GetServices<IHostedService>().ToArray();

    // ---- ⚠ THE CONTROL, DRAWN FROM THE MECHANISM AT RISK.
    //
    // Two empty sets are equal. If the container handed back nothing at all — a factory misconfigured, a
    // resolution that silently yields none — the comparison below would pass while proving the opposite of
    // what it claims. The framework's own services are what make this floor meaningful: they are present
    // whether or not a single line of ours is.
    Assert.NotEmpty(resolved);

    var ours = resolved
      .Select(service => service.GetType())
      .Where(type => type.Assembly.GetName().Name?.StartsWith("SSAS.", StringComparison.Ordinal) == true)
      .Select(type => type.FullName!)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    var expected = Owned
      .Select(type => type.FullName!)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // `Assert.True` with the two sets interpolated rather than `Assert.Equal`: xUnit's collection failure
    // TRUNCATES each element at 50 characters and elides the tail, and every name here shares a long
    // namespace prefix — so the offender is precisely the part that gets cut. Measured while building this
    // file, on this very collection.
    Assert.True(
      expected.SequenceEqual(ours, StringComparer.Ordinal),
      "the set of startup services the host registers is not the set this repository owns. A missing entry " +
      "is a service that will never run — `AddHostedService` is a collection, so an absent registration " +
      "makes the host start with a shorter list and no error at all." +
      $"{Environment.NewLine}registered but not expected: {Format(ours.Except(expected, StringComparer.Ordinal))}" +
      $"{Environment.NewLine}expected but NOT REGISTERED: {Format(expected.Except(ours, StringComparer.Ordinal))}");
  }

  private static string Format(IEnumerable<string> names)
  {
    var ordered = names.OrderBy(name => name, StringComparer.Ordinal).ToArray();

    return ordered.Length == 0 ? "(none)" : string.Join(", ", ordered);
  }
}
