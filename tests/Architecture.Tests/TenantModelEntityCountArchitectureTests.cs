using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// A FAST ALARM ON THE TENANT MODEL'S SIZE (T-141).
// ==================================================================================================
//
// **This asserts one number. Its job is LATENCY, not coverage.**
//
// ---- WHY IT EXISTS, AND THE EVIDENCE IS A COMMENT IN ANOTHER TEST.
//
// Several Integration tests hardcode counts and lists of the tenant model's entities — the cutover manifest,
// the payroll table count, the platform schema list. **They must hardcode them: the product DERIVES its
// manifest from the model, so a test that also derived would be tautological and prove nothing. The
// hardcoding IS the guard.**
//
// **Therefore they drift by construction whenever an entity is added, and the only question is how long
// before anyone notices.** In T-140 the answer was **eighteen days** — `OneOffPayment` (T-110) and
// `UserEmployeeLink` (T-082) had made eight Integration tests red, and no full suite had completed in that
// window to say so.
//
// **And it had happened before. `PlatformIdentityAccessPersistenceTests` records its own previous
// occurrence in a comment:** *"FP-006C1 added the user-to-company assignment table. This list asserted the
// platform schema without it until FP-006C6, when this suite was run again."* **A test drifted, someone
// documented why, and it then drifted again for exactly the same reason.** **Documenting the debt was not a
// control.**
//
// **So this fires in `GATE_SCOPE=TASK`, which every task runs, in milliseconds** — for the person who added
// the entity, at the moment they added it, in a run they cannot skip. The forty-minute suite keeps the
// richer assertions and simply stops being the first thing to notice.
//
// ---- ⚠ THIS NUMBER WILL DRIFT TOO, AND THAT IS THE POINT RATHER THAN A FLAW.
//
// It is one more hardcoded count and it goes stale the moment an entity is added — **which is exactly what
// it is for.** The difference is that it goes stale **inside one gate run** instead of inside eighteen days.
//
// **It deliberately does NOT assert the entity LIST.** The Integration tests hold that claim, and two places
// asserting one fact is `DEC-L-080`. **The list appears only in the FAILURE MESSAGE**, so a failure names
// which entity arrived without a second copy of the truth existing when it passes.
public sealed class TenantModelEntityCountArchitectureTests
{
  // ---- ⚠ THE TWO ASSERTIONS BELOW COVER EACH OTHER'S BLIND SPOT. NEITHER IS BELT-AND-BRACES.
  //
  //   the count (35)      catches GROWTH        an entity added, and the Integration lists now stale
  //   the by-name theory  catches DISAPPEARANCE a module absent from the composed model entirely
  //
  // **A count alone cannot tell "35 entities" from "35 entities with Payroll missing and something else
  // double-counted"** — two errors cancelling into a green, which is `DEC-L-083`'s family.
  //
  // **And the theory alone is not complete either: a FIFTH module, added and absent, is named in neither
  // `InlineData` row.** But the count catches that, because 35 would have become 36.
  //
  // **So each covers what the other cannot, and deleting either leaves a real gap.** Said explicitly because
  // two assertions about one model read as redundancy, and redundancy is what gets removed later.

  // ---- THE ONE NUMBER. Raise it in the same change that adds the entity, and the Integration
  // ---- expectations T-140 lists will need the same treatment — that is the alarm working, not a chore
  // ---- it invented.
  //
  // ⚠ 36 IS NOT THE CUTOVER'S 35, AND THEY ARE NOT MEANT TO MATCH. I set this to 35 first, from
  // `TablesCopied`, and it failed — **two numbers measuring different things.** `TenantCutoverCopyPlan`
  // filters `!entity.IsOwned()` and requires a table name, so the copied-table count EXCLUDES what this
  // counts. **Do not "reconcile" them: a change that made them equal would mean an owned type had
  // become a table, which is a real event and not a tidy-up.**
  private const int ExpectedEntityCount = 36;

  [Fact]
  public void The_composed_tenant_model_has_exactly_the_expected_number_of_entities()
  {
    var model = ComposedTenantModel();

    var entities = model.GetEntityTypes()
      .Select(entity => entity.ClrType.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // `Assert.True` with a message rather than `Assert.Equal`, because the ONLY place the entity list
    // may appear is a failure — and `Assert.Equal(int, int)` has no message overload, so it would report
    // "35 != 36" and leave the reader to find which entity arrived.
    Assert.True(
      entities.Length == ExpectedEntityCount,
      $"Expected {ExpectedEntityCount} tenant entities, found {entities.Length}. " +
      $"Raise ExpectedEntityCount and update the Integration expectations T-140 lists." +
      $"{Environment.NewLine}{string.Join(Environment.NewLine, entities)}");
  }

  // ---- NOT VACUOUS, AND THIS IS THE ASSERTION THAT EARNS THE FILE.
  //
  // **T-133 found the unicode guard passing a planted `varchar` because its `TenantModel()` built a
  // `TenantDbContext` DIRECTLY — two entity types, none of the ERP.** An alarm on model size that inspected
  // an empty model would be green forever and read as coverage.
  //
  // Every module must therefore be REPRESENTED, checked by name rather than by count, so the failure says
  // which module is missing rather than that a number is small.
  [Theory]
  [InlineData("Employee")]
  [InlineData("Account")]
  [InlineData("PayrollRun")]
  [InlineData("AttendanceRecord")]
  public void Every_module_is_represented_in_the_composed_model(string entityName) =>
    Assert.Contains(
      ComposedTenantModel().GetEntityTypes(),
      entity => entity.ClrType.Name == entityName);

  // ---- CONTRIBUTORS ARE DISCOVERED, NOT LISTED — AND HERE IS WHERE THE HARDCODING WENT.
  //
  // **`UnicodeStringPersistenceArchitectureTests` names its four contributors in a C# array.** That is a
  // second drift surface: a fifth module would be silently absent and the guard would inspect less than it
  // claims — **which is the exact defect T-133 fixed in that file.** Reproducing it here would be building
  // the disease into the alarm for it.
  //
  // **⚠ BUT DISCOVERY DOES NOT REMOVE THE LIST. IT MOVES IT.** Reflection sees only assemblies this project
  // REFERENCES, so the residual hardcoding now lives in
  // **`tests/Architecture.Tests/SSAS.Architecture.Tests.csproj`'s `<ProjectReference>` elements** — a file
  // nobody opens while thinking about model coverage.
  //
  // **That is better rather than solved:** a missing project reference usually breaks something else loudly,
  // and a C# array does not. **A module added without a reference here is invisible to discovery AND to a
  // hardcoded array equally** — nothing inside this project can see an assembly it does not reference, and
  // catching that needs a different guard than this one.
  private static Microsoft.EntityFrameworkCore.Metadata.IModel ComposedTenantModel()
  {
    // ---- ⚠ FROM THE OUTPUT DIRECTORY, NOT FROM `AppDomain.CurrentDomain.GetAssemblies()`.
    //
    // **`AppDomain` returns only assemblies already LOADED**, and .NET loads them lazily — nothing in
    // this test touches a module type before discovery runs, so it found ZERO contributors and built the
    // same two-entity model T-133 caught. **`Assert.NotEmpty` below is what turned that into a failure
    // instead of a green alarm reporting on an empty product.**
    //
    // Reading the directory finds every assembly the project references, whether or not it is loaded.
    var contributors = Directory
      .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.dll")
      .Select(LoadOrNull)
      .Where(assembly => assembly is not null)
      .Select(assembly => assembly!)
      .SelectMany(SafeTypes)
      .Where(type => typeof(ITenantModelContributor).IsAssignableFrom(type) &&
        type is { IsAbstract: false, IsInterface: false })
      .OrderBy(type => type.FullName, StringComparer.Ordinal)
      .Select(type => (ITenantModelContributor)Activator.CreateInstance(type)!)
      .ToArray();

    // A discovery that found nothing would build the two-entity model T-133 caught, and every assertion
    // above would then be reporting on an empty product.
    Assert.NotEmpty(contributors);

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model;Database=model;Integrated Security=True")
      .Options;

    using var context = new TenantDbContext(
      options, new ModelUser(), new ModelTenant(Guid.NewGuid()), new ModelClock(), modelContributors: contributors);

    return context.Model;
  }

  // A module assembly that cannot be reflected over is a fact worth failing on rather than skipping — a
  // silent `catch` here would shrink the model exactly the way T-133's guard shrank it.
  // A file that is not a managed assembly, or cannot be loaded, is skipped — native and resource DLLs sit
  // beside the managed ones. A load FAILURE of a real module assembly would shrink the model silently, so
  // the emptiness check above is what stands between that and a false green.
  private static Assembly? LoadOrNull(string path)
  {
    try
    {
      return Assembly.LoadFrom(path);
    }
    catch (BadImageFormatException)
    {
      return null;
    }
    catch (FileLoadException)
    {
      return null;
    }
  }

  private static IEnumerable<Type> SafeTypes(Assembly assembly)
  {
    try
    {
      return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException loaded)
    {
      return loaded.Types.Where(type => type is not null)!;
    }
  }

  // The same stubs `UnicodeStringPersistenceArchitectureTests` uses. Model construction touches none of
  // them; they exist because `TenantDbContext` requires them.
  private sealed class ModelUser : ICurrentUser
  {
    public string? UserId => null;

    public string? UserName => null;

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class ModelClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
  }
}
