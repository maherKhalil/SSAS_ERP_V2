using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Payroll.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE MODEL THE HOST COMPOSES IS THE MODEL THE GUARDS VERIFY (T-132).
// ==================================================================================================
//
// ---- ⚠⚠⚠ THE CLAIM WAS ALREADY MADE IN THIS TREE AND NOTHING DELIVERED IT.
//
// `EmployeeHostCompositionTests`' H10 header: *"Runtime persistence, the migration tool and the cutover must
// all compose the same tenant model … this asserts the routes agree rather than assuming they do."* **The
// claim is stated; the delivery was missing**, and H10 itself cannot make it — its container registers HR
// alone, so its exact-list-of-one is correct for what it builds and silent about the Host.
//
// ---- ⚠⚠⚠ WHAT THAT COST, MEASURED BY PLANT.
//
// `services.AddSingleton<ITenantModelContributor, GlTenantModelContributor>()` was deleted from
// `SSAS.GL.Infrastructure` — **the running product then composes a tenant model with no GL entities at
// ---- ⚠⚠ AND WHEN THIS GUARD EXISTED, THE SAME PLANT REDDENED IT — WITH THE ASSERTION NAMED (T-181).
//
// **PLANT:** that same registration deleted. ***RED at the FINAL assertion — the symmetric-difference
// `Assert.True` — reporting `verified by the guards, never composed by the host: Account, FiscalPeriod,
// FiscalYear, JournalDraft, JournalDraftLine, JournalEntry, JournalLine`.*** **REVERT → GREEN.**
// ⚠ **The two `NotEmpty` floors above it executed and PASSED — reached, not proven.** *Their arrangement is
// a collapsed population, not a mismatched one, and it was not built.*
//
// all** — and the suites were run: **API 1004/1004, Architecture 725/725, Platform 1145/1145. 2,874 tests,
// not one red.** *A whole module's entities gone from the model the Host actually builds, and nothing saw it.*
//
// Every existing guard looks somewhere else, and each reads complete on its own:
//
//   `TenantModelHasNoPendingChangesTests`         composes its OWN context from four `new`-ed contributors
//   `TenantModelEntityCountArchitectureTests`     composes its own, contributors discovered from the output
//                                                 DIRECTORY — found on disk whether or not DI registers them
//   `TenantModelResidencyTests`                   reads the REAL host, but asserts only the ABSENCE of
//                                                 sixteen platform types
//
// ⚠⚠ AND THE THIRD ONE'S ANTI-VACUITY CONTROL IS THE SHARPEST PART. It asserts `Branch` and `Company` are
// PRESENT — **and both arrive from the two EF configurations, not from any contributor. *That control passes
// with zero contributors registered.*** A correct control for "did the model build", worthless for "did the
// contributors run", and nothing distinguishes those readings from outside.
//
// ---- ⚠⚠⚠ WHY A SET COMPARISON AND NOT A LIST OF MODULES.
//
// The obvious guard — assert one entity per module by name — **is the defect it would be built to catch**: it
// reddens when a module LEAVES and is silent when a FIFTH IS ADDED AND NEVER REGISTERED. That is H10's own
// failure, whose list is frozen at a number that was never the Host's.
//
// ***A SYMMETRIC DIFFERENCE NEEDS NO LIST, SO IT CANNOT FREEZE.*** A fifth module registered but unverified
// reddens; a fifth verified but unregistered reddens; a registration deleted reddens. **Nothing here needs
// updating when a module is added — which is precisely why nothing here can go stale.**
//
// ---- ⚠⚠ ITS LIMIT, STATED SO IT IS NOT READ AS MORE.
//
// **This compares entity-type NAMES — membership — and nothing about their configuration.** Two models can
// agree on every name and differ in constraints, indexes and column types. *That half belongs to
// `TenantModelHasNoPendingChangesTests` and `PlatformModelHasNoPendingChangesTests`, one per plane, and this
// guard must not be read as covering it.*
[Collection(HostIntegrationTestGroup.Name)]
public sealed class HostComposedTenantModelTests(HostWebApplicationFactory factory)
{
  [Fact]
  public void The_host_composes_the_same_tenant_entity_set_the_model_guards_verify()
  {
    var hostComposed = EntityNames(factory.Services.GetRequiredService<ITenantModelSource>().Model);
    var verified = EntityNames(VerifiedModel());

    // ---- ⚠ THE CONTROL, AND IT IS DRAWN FROM THE MECHANISM AT RISK RATHER THAN FROM THE SAME OBJECT.
    //
    // Two empty sets are equal. A comparison alone would pass if the host resolved an empty model, or if
    // discovery on either side collapsed — **the exact false green this guard exists to catch, wearing the
    // costume of a pass.** `NotEmpty` on both sides is the floor; the equality below is the claim.
    Assert.NotEmpty(hostComposed);
    Assert.NotEmpty(verified);

    // The difference is computed in BOTH directions because the two failures mean opposite things and want
    // opposite fixes: a type the host builds and no guard verifies is unguarded drift; a type the guards
    // verify and the host never builds is a module the product forgot to register.
    var hostOnly = hostComposed.Except(verified, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
    var verifiedOnly = verified.Except(hostComposed, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    // `Assert.True` with an interpolated message rather than `Assert.Equal` on the two sets: a set comparison
    // failure prints both collections in full — seventy-odd names twice — and leaves the reader to diff them.
    // The offenders are what a reader needs, and they only exist in the failure.
    Assert.True(
      hostOnly.Length == 0 && verifiedOnly.Length == 0,
      $"the tenant model the HOST composes ({hostComposed.Count} entity types) is not the model the guards " +
      $"verify ({verified.Count}). Every guard on the tenant model checks a composition built in a test, so " +
      "a difference here means those guards are reporting on a model the product does not build." +
      $"{Environment.NewLine}composed by the host, verified by nothing: " +
      $"{Format(hostOnly)}" +
      $"{Environment.NewLine}verified by the guards, never composed by the host — a module whose contributor " +
      $"is not registered: {Format(verifiedOnly)}");
  }

  private static string Format(string[] names) =>
    names.Length == 0 ? "(none)" : string.Join(", ", names);

  private static HashSet<string> EntityNames(Microsoft.EntityFrameworkCore.Metadata.IModel model) =>
    [.. model.GetEntityTypes().Select(entity => entity.ClrType.Name)];

  // ---- THE VERIFIED SIDE, COMPOSED EXACTLY AS `TenantModelHasNoPendingChangesTests` COMPOSES IT.
  //
  // ⚠ **Named rather than discovered, and deliberately the same four names that guard names.** Discovery
  // from the output directory — what `TenantModelEntityCountArchitectureTests` does — would make this side
  // agree with the product for the wrong reason: a contributor absent from BOTH the DI registration and the
  // build output would leave the two sets equal and the guard green. *The point of this comparison is that
  // one side is the product's answer and the other is the guards' answer; if this side asked the product it
  // would be comparing something with itself.*
  private static Microsoft.EntityFrameworkCore.Metadata.IModel VerifiedModel()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=(unused);Database=(unused);Integrated Security=True")
      .Options;

    using var context = new TenantDbContext(
      options,
      new UnusedUser(),
      new UnusedTenant(),
      new UnusedClock(),
      modelContributors:
      [
        new HrTenantModelContributor(),
        new GlTenantModelContributor(),
        new PayrollTenantModelContributor(),
        new AttendanceTenantModelContributor(),
      ]);

    return context.Model;
  }

  // The model is built from entity configuration alone; none of these is consulted to shape it.
  private sealed class UnusedUser : ICurrentUser
  {
    public string? UserId => null;

    public string? UserName => null;

    public string? Email => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class UnusedTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class UnusedClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}
