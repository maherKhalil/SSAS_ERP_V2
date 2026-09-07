using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SSAS.Host.API.Authentication;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE SIGNING-KEY STARTUP VALIDATOR FAILS THE BOOT, WHICH IS THE ONLY THING IT DOES (T-124).
// ==================================================================================================
//
// `SigningKeyStartupValidator` is an `IHostedService` whose entire body is `_ = provider.Snapshot;`.
// **It exists so that an unobtainable signing key kills STARTUP rather than the first request that needs
// a token.** That is its whole contract and nothing asserted it.
//
// ---- ⚠⚠⚠ HOW IT WAS FOUND, AND THE PART WORTH KEEPING.
//
// A sweep for production types whose NAME appears nowhere in `tests/` returned 310, of which 16 were
// behavioural. ***FIFTEEN OF THE SIXTEEN WERE FALSE: a DI-registered service exercised through an endpoint
// NEVER has its concrete name in a test, and the check that separated them was looking for the INTERFACE***
// — `IRoleReadService` and `ITenantReadService` are both named in tests, so their implementations are
// reached through the host.
//
// This one survived that filter: it has no interface, and neither its name nor its behaviour appeared
// anywhere. ⚠ **AND THE BEHAVIOUR WAS CHECKED SEPARATELY FROM THE NAME**, because a test asserting "startup
// fails without a key" would never mention the class — `ISigningKeyProvider` is named in sixteen test
// files and every one of them resolves `Snapshot` SUCCESSFULLY to sign a token. *Nobody had ever made it
// fail.*
//
// ---- ⚠⚠ EXECUTED, BUT NOT ASSERTED — AND THOSE ARE DIFFERENT THINGS.
//
// `HostWebApplicationFactory` boots the real `Program` with no service replacement and no hosted-service
// removal, so this validator RUNS in every API test that starts a host. **It was never unreached; its
// FAILURE path was simply never observed.** *An absence guard that has never fired is indistinguishable
// from one that cannot* — and here the guard lives in `src/` rather than in a test.
public sealed class SigningKeyStartupValidatorTests
{
  // ⚠ THE ASSERTION IS ABOUT THE BOOT, NOT ABOUT THE TYPE. A test that merely proves the class exists, or
  // that a throwing provider throws, would pass with the validator deleted. **What must hold is that the
  // failure arrives AT STARTUP** — so the host is started explicitly and the throw is caught there.
  // ==================================================================================================
  // ⚠⚠⚠ NOT `async Task` — AND THE REASON IS AN INCIDENT WORTH THE PARAGRAPH (T-180).
  // ==================================================================================================
  //
  // The assertion is SYNCHRONOUS: `CreateClient` throws on the calling thread. This method was written
  // `async Task` when the file was added (`f6dc6b4`), so it emitted **CS1998 — *"This async method lacks
  // 'await' operators"*** — and `DEC-L-008` condition one is ZERO BUILD WARNINGS, which the gate enforces:
  //
  //     !!! WARNINGS (Debug): 1 -- DEC-L-008 condition 1 is zero. This gate is RED.
  //
  // ***IT STOOD FOR TWENTY-FIVE COMMITS BEHIND TWENTY-ODD HONEST GREENS.*** `dotnet test` reported
  // `API 1019/1019 Passed!` every single time, and it was TRUE — **`dotnet test` does not fail on warnings,
  // and only the gate reads `build-Debug.log`.** *A per-suite green is not the gate, and this is what that
  // costs when nobody runs the gate for twenty-five commits.*
  //
  // ⚠⚠ AND IT WAS SEEN AND NOT ACTED ON. The CS1998 line scrolled past in the FIRST `dotnet test` run of
  // the night, in the build output above the results, and was read as noise.
  //
  // ***THE HABIT THAT WOULD HAVE CAUGHT IT IS ONE WORD WIDER THAN THE ONE ALREADY IN USE:
  // GREP `dotnet test` OUTPUT FOR `warning`, NOT ONLY `dotnet build` OUTPUT.*** **`dotnet test` builds
  // first and prints the compiler's warnings, then prints a green summary that says nothing about them —
  // so the one command whose output gets read carries the evidence and buries it.**
  [Fact]
  public void Host_startup_fails_when_the_signing_key_cannot_be_obtained()
  {
    using var factory = new UnobtainableKeyFactory();

    // `CreateClient` is what forces the host to build and start. If the validator is doing its job the
    // exception surfaces here rather than on a later request.
    var failure = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

    Assert.Equal(UnobtainableKeyProvider.Reason, Unwrap(failure).Message);
  }

  // ---- ⚠⚠⚠ THE CONTROL: THE SAME HOST STARTS WHEN THE KEY IS OBTAINABLE.
  //
  // Without this, the test above is satisfied by a host that cannot start for ANY reason — a broken test
  // fixture, a missing connection string, an unrelated hosted service — and it would go on passing while
  // proving nothing about signing keys. **The two differ in exactly one registration.**
  [Fact]
  public void The_same_host_starts_when_the_key_is_obtainable()
  {
    using var factory = new HostWebApplicationFactory();

    using var client = factory.CreateClient();

    Assert.NotNull(client);
  }

  // The innermost exception: the host wraps startup failures, and asserting on the wrapper would pass for
  // any startup failure at all — which is the discrimination this test exists to make.
  private static Exception Unwrap(Exception exception)
  {
    var current = exception;
    while (current.InnerException is not null)
    {
      current = current.InnerException;
    }

    return current;
  }

  private sealed class UnobtainableKeyFactory : WebApplicationFactory<global::Program>
  {
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.UseEnvironment("Development");
      builder.ConfigureTestServices(services =>
      {
        services.RemoveAll<ISigningKeyProvider>();
        services.AddSingleton<ISigningKeyProvider, UnobtainableKeyProvider>();
      });
    }
  }

  // Stands in for the real provider's failure mode: a certificate that cannot be loaded, a path that does
  // not resolve, a key that is not configured. ⚠ The provider is the right thing to fake here because the
  // property under test is the VALIDATOR's — that a failure from this dependency reaches the boot — and a
  // fake that withheld nothing else keeps that axis intact.
  private sealed class UnobtainableKeyProvider : ISigningKeyProvider
  {
    public const string Reason = "the signing key could not be obtained";

    public SigningKeySnapshot Snapshot => throw new InvalidOperationException(Reason);
  }
}
