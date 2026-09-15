using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SSAS.Host.API.Authentication;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// EVERY OPTIONS VALIDATOR THIS REPOSITORY OWNS IS REGISTERED IN THE HOST (T-140).
// ==================================================================================================
//
// ---- ⚠⚠⚠ WHY: A VALIDATOR WITH SIX TESTS THAT THE HOST MAY NEVER CONSULT.
//
// `IValidateOptions<T>` is a collection registration, and a collection's missing member is silent: the
// options system resolves one fewer validator and the host starts. **`services.AddSingleton<IValidateOptions
// <TenantStorageOptions>, TenantStorageOptionsValidator>()` was deleted and the suites run: API 1006,
// Platform 1159, Architecture 725 — 2,890 tests, all green.**
//
// ***AND THAT VALIDATOR IS ONE OF THE BEST-TESTED THINGS IN THE TREE.*** `TenantStorageOptionsValidatorTests`
// has six refusal assertions. **Every one of them constructs the validator directly** — `Validate(options)`
// on a `new TenantStorageOptionsValidator()` — *so all six pass whether or not the host ever asks it
// anything.* **The tests prove the validator WORKS; nothing proved the host CONSULTS it.**
//
// ⚠ This is the same split as `SigningKeyStartupValidator` and the module company-context filters: a
// well-tested thing behind a one-line wiring that nothing asserts.
//
// ---- ⚠⚠ SCOPE: OURS ONLY — AND HERE THE FRAMEWORK'S CONTRIBUTION IS NOT MERELY IRRELEVANT, IT IS UNSTABLE.
//
// The host registers **twenty-two** `IValidateOptions<>` descriptors. Sixteen are the framework's, and they
// are not a fixed set: `AddOptions<T>().Validate(...)` emits one `Microsoft.Extensions.Options.ValidateOptions
// <T>` per chained call, so `AuthenticationPolicyOptions` appears EIGHT times and `AuthenticationClientOptions`
// THREE — **counts that change when someone adds a validation clause, which is not this guard's subject.**
// Others come from ASP.NET itself:
//
//     Asp.Versioning.ValidateApiVersioningOptions              (ApiVersioningOptions)
//     DataAnnotationValidateOptions<ApplicationOptions>        (from .ValidateDataAnnotations())
//     ValidateOptions<JwtBearerOptions>, ValidateOptions<PasswordHasherOptions>
//
// ***SO THE ASSERTION IS SCOPED TO VALIDATOR TYPES DECLARED IN THIS REPOSITORY, SELECTED BY THE `SSAS.`
// ASSEMBLY PREFIX.*** *An exact set including the framework's would redden when a `.Validate()` clause is
// added — a failure with nothing to do with what this file claims.*
//
// ⚠ **The filter fails safe**: if the `SSAS.` prefix stopped matching, the filtered set would be EMPTY and
// the comparison would redden rather than pass vacuously. **And the floor is taken on the UNFILTERED
// descriptor set, which the framework populates whether or not one line of ours survives.**
//
// ⚠ RECONCILES WITH THE EARLIER SOURCE COUNT: a `git grep` for `IValidateOptions<` in `src/` returns twelve
// sites. **Six validator types, each appearing twice — once at its `class X : IValidateOptions<T>`
// declaration and once at its registration.** *Two instruments, one population.*
//
// ---- ⚠⚠⚠ THE LIMIT, AND IT IS THE SAME ONE `HostedServiceRegistrationTests` CARRIES.
//
// ***THIS PROVES EACH VALIDATOR IS REGISTERED. IT PROVES NOTHING ABOUT ANY OF THEM REFUSING CORRECTLY*** —
// that is what the per-validator unit tests do, and they are the half that already existed. **Nor does it
// prove the options type is ever BOUND or validated at startup**; a validator registered against options
// nothing resolves is still inert, and this guard cannot see that.
//
// ⚠ AND THE DESCRIPTORS ARE A SNAPSHOT. `ConfigureTestServices` runs after the application's own
// registration, so everything `Program` adds is present — **but a registration made later still would not
// be.** *Stated because the instrument's reach is not obvious from its result.*
//
// ---- ⚠ `IDomainEventConsumer` IS DELIBERATELY NOT FOLDED IN HERE.
//
// It is the same class of defect — deleting its one registration is invisible, and the localization cache
// then never invalidates — **but it is a different interface with a different failure and a different
// remedy, and a file asserting two unrelated collections has a failure message that cannot say which
// subject broke.** *Left out on purpose rather than overlooked; it needs its own guard or none.*
// ---- ⚠⚠ IN THE HOST COLLECTION EVEN THOUGH IT TAKES NO FIXTURE, AND THAT IS NOT COSMETIC.
//
// This class boots its OWN `Program` host to capture the descriptors. xUnit runs each collection in
// parallel, and a class with no `[Collection]` gets an implicit one of its own — **so without this
// attribute a third `Program` host starts concurrently with the shared host and with
// `SigningKeyStartupValidatorTests`' own factory.** *It was added after that combination reddened
// `Host_startup_fails_when_the_signing_key_cannot_be_obtained`, which passes alone and failed only in the
// full run.* **The attribute serialises this against the shared-host group; it takes no fixture and needs
// none.**
[Collection(HostIntegrationTestGroup.Name)]
public sealed class OptionsValidatorRegistrationTests
{
  // ---- BOUND BY `typeof`, SO A RENAME OR MOVE IS A COMPILE ERROR RATHER THAN A SILENT MISMATCH.
  private static readonly Type[] Owned =
  [
    typeof(JwtOptionsValidator),
    typeof(CompromisedPasswordOptionsValidator),
    typeof(PlatformSupportBootstrapOptionsValidator),
    typeof(TenantDatabaseBackupSchedulerOptionsValidator),
    typeof(TenantDatabaseRestoreVerificationOptionsValidator),
    typeof(TenantStorageOptionsValidator),
  ];

  [Fact]
  public void Every_options_validator_this_repository_owns_is_registered_in_the_host()
  {
    using var factory = new DescriptorCapturingFactory();
    using var client = factory.CreateClient();

    var validatorDescriptors = factory.Descriptors
      .Where(descriptor => descriptor.ServiceType.IsGenericType
        && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>))
      .ToArray();

    // ---- ⚠ THE CONTROL, DRAWN FROM THE MECHANISM AT RISK.
    //
    // An empty descriptor list and a correct one are both "no differences" once filtered. The framework's
    // sixteen are present whether or not a single line of ours is, so this floor fails when the CAPTURE
    // broke — which is the way this instrument would most plausibly lie.
    Assert.NotEmpty(validatorDescriptors);

    var registered = validatorDescriptors
      .Select(descriptor => descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType())
      .Where(type => type is not null)
      .Select(type => type!)
      .Where(type => type.Assembly.GetName().Name?.StartsWith("SSAS.", StringComparison.Ordinal) == true)
      .Select(type => type.FullName!)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    var expected = Owned
      .Select(type => type.FullName!)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // `Assert.True` with both differences interpolated rather than `Assert.Equal` on two sets: the collection
    // renderer truncates each element at fifty characters, and these names share a long namespace prefix, so
    // the distinguishing part is exactly what would be cut. See `AssertionMessageChoice.cs`.
    Assert.True(
      expected.SequenceEqual(registered, StringComparer.Ordinal),
      "the set of options validators the host registers is not the set this repository owns. A missing entry " +
      "is a validator that will never be consulted — `IValidateOptions<T>` is a collection, so an absent " +
      "registration leaves the options system with a shorter list and no error at all." +
      $"{Environment.NewLine}registered but not expected: {Format(registered.Except(expected, StringComparer.Ordinal))}" +
      $"{Environment.NewLine}expected but NOT REGISTERED: {Format(expected.Except(registered, StringComparer.Ordinal))}");
  }

  private static string Format(IEnumerable<string> names)
  {
    var ordered = names.OrderBy(name => name, StringComparer.Ordinal).ToArray();

    return ordered.Length == 0 ? "(none)" : string.Join(", ", ordered);
  }

  // The registrations cannot be read back from a built `IServiceProvider` — `IValidateOptions<T>` is closed
  // over each options type, so there is no single collection to resolve and enumerating them would mean
  // naming the very types this guard exists to discover. The descriptor list is the population itself.
  private sealed class DescriptorCapturingFactory : WebApplicationFactory<global::Program>
  {
    public ServiceDescriptor[] Descriptors { get; private set; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.UseEnvironment("Development");
      builder.ConfigureTestServices(services => Descriptors = [.. services]);
    }
  }
}
