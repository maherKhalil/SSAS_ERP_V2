using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE PLATFORM MODEL HAS NO CHANGES THAT NO MIGRATION CARRIES (T-129).
// ==================================================================================================
//
// **The direct twin of `TenantModelHasNoPendingChangesTests`, and deliberately not a second design.** Same
// question, same mechanism, same failure instruction; the only differences are the ones the two contexts
// force. *Where that file has a limitation this one inherits it rather than inventing a way round it.*
//
// ---- ⚠⚠⚠ WHY THIS DID NOT EXIST, AND WHAT THAT COST — MEASURED, NOT ARGUED.
//
// `HasPendingModelChanges` was applied to the TENANT model when T-210 built it, and the platform model was
// never given the twin. **So the guard everyone would name if asked "what stops schema drift here" covered
// one of the two contexts.** *That is not visible from either file: each reads complete on its own.*
//
// It was found by planting. `UX_PlatformSupportPrincipals_IdentityId` — the unique index carrying
// *"one platform-support authority record per global Identity"* (`ADR-015`), a UNIQUENESS INVARIANT rather
// than a performance index — was removed from `PlatformSupportPrincipalConfiguration`, and:
//
//     Architecture   724 / 724   GREEN
//     Platform      1145 / 1145  GREEN
//     API           1004 / 1004  GREEN
//
// ***AN INVARIANT DELETED, 2,873 TESTS, NOT ONE RED.*** The identical plant on the tenant side — a filtered
// unique index removed from `BranchConfiguration` — reddened exactly one test, the tenant twin of this one.
// **The two models were the same shape of thing with opposite protection.**
//
// ---- ⚠⚠ AND THE ASYMMETRY COMPOUNDS, WHICH IS WHY THE PLATFORM SIDE NEEDED IT MORE.
//
// `PlatformDbContext` declares **32 `DbSet<>` properties**; `TenantDbContext` declares **2**. A `DbSet` puts
// an entity in the model on its own, so on the platform side a configuration can disappear entirely and its
// entity REMAINS — unconfigured, keyless-by-convention, its constraints and indexes and column types gone.
// **On the tenant side the same removal usually breaks the model build outright.** *The tenant model's
// protection is partly structural; the platform model had none.*
//
// ⚠ ONE FALSE LEAD RECORDED SO NOBODY RE-WALKS IT: making a configuration stop implementing
// `IEntityTypeConfiguration<T>` produced SEVENTY failures, which reads as emphatic coverage and is not.
// EF could not build the model at all — the configuration carried `HasConversion`, so the value object
// became an entity type and construction threw. **A configuration carrying a conversion cannot leave
// quietly; one carrying only constraints and indexes can.** *Seventy reds measured "is the model still
// buildable", not "does any guard see this".*
public sealed class PlatformModelHasNoPendingChangesTests
{
  [Fact]
  public void The_platform_model_has_no_changes_that_no_migration_carries()
  {
    using var context = PlatformModel();

    // ⚠ IF THIS IS RED, THE FIX IS `dotnet ef migrations add`, NOT AN EDIT HERE.
    //
    // A mapped property, an index, a key or a relationship has been added to a platform entity and no
    // migration carries it. Every write touching that table will fail against a real database with a
    // message that may name the column — or, if the caller wraps its persistence errors, may not.
    Assert.False(
      context.Database.HasPendingModelChanges(),
      "the platform model has changes no migration carries: run `dotnet ef migrations add` against " +
      "PlatformDbContext. A mapped member was added without a migration, and every write to that table " +
      "will fail against a real database.");
  }

  // ---- ⚠ NO CONTRIBUTOR LIST HERE, AND THAT IS A REAL DIFFERENCE RATHER THAN A SIMPLIFICATION.
  //
  // `TenantDbContext` composes four module contributors and its guard must name all four, because a context
  // built with none would compare an almost-empty model against a full snapshot. **`PlatformDbContext` takes
  // no contributors: its model is `ApplyConfigurationsFromAssembly` over one assembly plus its own `DbSet`
  // properties, so there is no list to get wrong and no equivalent false green to defend against.**
  //
  // ⚠⚠ THE SEAM THAT DOES SURVIVE, INHERITED FROM THE TWIN AND WORTH STATING: **both model guards build
  // their composition here rather than resolving it from the product's container.** A registration the
  // product forgot is invisible to either — `EmployeeHostCompositionTests` is the only place that reads
  // `GetServices<ITenantModelContributor>()`, and it is not a file anyone opens while thinking about model
  // composition. *This guard checks that the model matches its migrations, never that the running host
  // builds that model.*
  private static PlatformDbContext PlatformModel()
  {
    // A provider is required to build a relational model; no connection is ever opened.
    var options = new DbContextOptionsBuilder<PlatformDbContext>()
      .UseSqlServer("Server=(unused);Database=(unused);Integrated Security=True")
      .Options;

    return new PlatformDbContext(options, new UnusedUser(), new UnusedTenant(), new UnusedClock());
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
