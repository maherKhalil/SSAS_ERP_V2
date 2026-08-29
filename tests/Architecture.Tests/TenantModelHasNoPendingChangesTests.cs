using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Payroll.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE TENANT MODEL HAS NO CHANGES THAT NO MIGRATION CARRIES (T-210).
// ==================================================================================================
//
// ---- ⚠ THIS EXISTS BECAUSE ITS ABSENCE COST 103 INTEGRATION FAILURES IN ONE DAY.
//
// On 2026-08-29 `7117485` added `EmploymentType` — a MAPPED property — to the `Employee` entity across
// domain, application, payroll handlers, host wiring and six test files. **It touched no migration and the
// model snapshot never gained the column.** Every `INSERT` into `Employees` then failed against a database
// that has no such column: 95 failures in `EmployeeBoundarySqlServerTests` reporting a wrapped
// `Persistence.WriteFailure`, and 8 in `DepartmentApplicationSqlServerTests` reporting the raw
// `Invalid column name 'EmploymentType'`.
//
// **NOTHING COULD HAVE CAUGHT IT WHERE ANYONE WOULD LOOK**, and the reason is the QUESTION rather than the
// count. `GetPendingMigrations` answers whether APPLIED migrations match a LIVE DATABASE. It cannot see a
// model that has drifted from its own snapshot, **because both of those are code and it never compares
// them** — and it needs a database, so nothing that uses it can run at task scope.
//
// ---- ⚠ AN EARLIER VERSION OF THIS COMMENT SAID "APPEARS FIVE TIMES, AND EVERY ONE IS IN
// `Integration.Tests`". BOTH HALVES WERE WRONG (T-219, counted 2026-08-30).
//
//   `GetPendingMigrationsAsync()` in `Integration.Tests`   **15 call sites across 9 files**
//   in `src/`                                              **4** — `TenantMigrationRunner` (the interface,
//                                                          its implementation and its call) and
//                                                          `TenantDatabaseSchemaHealthService`
//
// **The argument survives the correction and the sentence did not.** Those four production uses ask the
// same live-database question, so none of them could have caught the drift either — but "every one is in
// `Integration.Tests`" was false, and a false supporting claim beside a true conclusion is exactly what
// stops the next reader checking the conclusion. Written the same evening as the rule that a count in
// prose is a claim that rots; **this one was wrong on the day it was written.**
//
// ---- A SCHEMA DIVERGENCE IS A FACT ABOUT THE CODE AND NEEDS NO SERVER TO OBSERVE.
//
// That is why this belongs here rather than in Integration: `HasPendingModelChanges` compares the model
// built from the entities against `TenantDbContextModelSnapshot`, both of which are compiled in. No
// connection is opened. It runs in milliseconds inside the suite that actually runs on every task.
//
// ---- ⚠ THE CONTRIBUTORS MUST ALL BE SUPPLIED, AND OMITTING ONE IS A FALSE GREEN.
//
// `TenantDbContext` takes `IEnumerable<ITenantModelContributor>?` and defaults it to empty. A context built
// with NO contributors has none of the 73 entity types the snapshot carries — so a check written that way
// would compare an almost-empty model against a full snapshot and report wholesale drift, or worse, be
// "fixed" by relaxing it until it passed. **All four modules are named below, and the count is asserted, so
// a fifth module added without a line here fails rather than silently narrowing what is checked.**
public sealed class TenantModelHasNoPendingChangesTests
{
  // Named rather than discovered by reflection: a reflection scan that stops matching would quietly reduce
  // this to the empty-contributor case, which is the false green described above.
  private static ITenantModelContributor[] Contributors() =>
  [
    new HrTenantModelContributor(),
    new GlTenantModelContributor(),
    new PayrollTenantModelContributor(),
    new AttendanceTenantModelContributor(),
  ];

  [Fact]
  public void Every_module_that_maps_entities_is_represented_here()
  {
    // ⚠ ANTI-VACUITY. The check below is only as complete as this list, and a module missing from it
    // removes its entities from the comparison rather than failing — the quietest possible way for this
    // guard to stop protecting something.
    Assert.Equal(4, Contributors().Length);

    var implementations = typeof(HrTenantModelContributor).Assembly.GetTypes()
      .Concat(typeof(GlTenantModelContributor).Assembly.GetTypes())
      .Concat(typeof(PayrollTenantModelContributor).Assembly.GetTypes())
      .Concat(typeof(AttendanceTenantModelContributor).Assembly.GetTypes())
      .Count(type => typeof(ITenantModelContributor).IsAssignableFrom(type)
        && type is { IsAbstract: false, IsInterface: false });

    Assert.Equal(Contributors().Length, implementations);
  }

  [Fact]
  public void The_tenant_model_has_no_changes_that_no_migration_carries()
  {
    using var context = TenantModel();

    // ⚠ IF THIS IS RED, THE FIX IS `dotnet ef migrations add`, NOT AN EDIT HERE.
    //
    // A mapped property, an index, a key or a relationship has been added to an entity and no migration
    // carries it. Every write touching that table will fail against a real database with a message that
    // may name the column — or, if the caller wraps its persistence errors, may not.
    Assert.False(
      context.Database.HasPendingModelChanges(),
      "the tenant model has changes no migration carries: run `dotnet ef migrations add` against " +
      "TenantDbContext. A mapped member was added without a migration, and every write to that table " +
      "will fail against a real database.");
  }

  private static TenantDbContext TenantModel()
  {
    // A provider is required to build a relational model; no connection is ever opened.
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=(unused);Database=(unused);Integrated Security=True")
      .Options;

    return new TenantDbContext(
      options,
      new UnusedUser(),
      new UnusedTenant(),
      new UnusedClock(),
      modelContributors: Contributors());
  }

  // The model is built from entity configuration alone; none of these is consulted to shape it, and each
  // throws rather than answering so a future dependency on them surfaces here instead of silently.
  private sealed class UnusedUser : ICurrentUser
  {
    public string? UserId => null;

    public string? UserName => null;

    public string? Email => null;

    public Guid? CompanyId => null;

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
