namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// ⚠⚠⚠ IF YOUR TEST BOOTS A HOST, IT BELONGS IN THIS COLLECTION. THE REASON IS PROCESS-GLOBAL STATE (T-150).
// ==================================================================================================
//
// **xUnit gives a class with no `[Collection]` an IMPLICIT collection of its own, and collections run in
// PARALLEL.** So a class that constructs its own `WebApplicationFactory` boots a second `Program` host
// alongside this one. *That is not a theoretical hazard: it has cost two diagnosis cycles, in two different
// costumes, and both times it surfaced as an UNRELATED test failing.*
//
// ---- WHAT IS ACTUALLY SHARED — MEASURED, NOT SUSPECTED.
//
// ***1. SERILOG'S GLOBAL `Log.Logger`.*** `Program.cs:39` assigns it, and `ConfigureHostSerilog` freezes the
// reloadable logger. A second concurrent boot throws:
//
//     System.InvalidOperationException : The logger is already frozen
//        at Serilog.Extensions.Hosting.ReloadableLogger.Freeze()
//
// ⚠ **Process-wide, not a port collision** — no amount of per-host configuration avoids it.
//
// ***2. THE IMPLICIT-COLLECTION PARALLELISM ITSELF.*** Adding one uncollected host-booting class was enough
// to redden `SigningKeyStartupValidatorTests.Host_startup_fails_when_the_signing_key_cannot_be_obtained`,
// which passes alone (2/2) and failed only in the full run. **A test that passes alone and fails in the suite
// is the one failure mode where "run it again" is both the cheapest action and the worst one.**
//
// ---- ⚠ AND WHAT IS **NOT** SHARED, MEASURED SO NOBODY RE-INVESTIGATES IT.
//
//   static mutable fields in `src/`   NONE. 1,946 lines contain ` static `; every one that is not
//                                     `static readonly`, a method or a class is a COMMENT.
//   `AppContext.SetSwitch`            zero occurrences in `src/`
//   `Environment.SetEnvironmentVariable`  zero occurrences in `src/`
//   certificate / temp-file paths     not shared: the Development signing certificate is generated in
//                                     memory per process and data protection is ephemeral in Development,
//                                     so no two hosts contend for a file
//
// ***SO `Log.Logger` IS THE ONLY PROCESS-GLOBAL OUR OWN CODE WRITES.*** *The rest of the hazard is xUnit's
// scheduling, and the collection is the remedy for both.*
//
// ---- THE RULE.
//
//   needs no customisation   take `HostWebApplicationFactory` as a constructor parameter and boot NOTHING
//   needs its own factory    construct it, AND put `[Collection(HostIntegrationTestGroup.Name)]` on the class
//
// ⚠ The second case takes no fixture — the attribute is there purely to serialise the boot, and a class in
// this collection may still create its own factory. *`UnhandledExceptionResponseTests` and
// `HttpsRedirectionExclusionTests` are both that shape.*
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HostIntegrationTestGroup : ICollectionFixture<HostWebApplicationFactory>
{
  public const string Name = "Host integration tests";
}
