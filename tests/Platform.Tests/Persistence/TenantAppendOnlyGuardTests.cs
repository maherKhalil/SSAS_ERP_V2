using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Platform.Tests.Persistence;

// ==================================================================================================
// THE APPEND-ONLY GUARD ON THE **TENANT** DATABASE.
// ==================================================================================================
//
// ---- ⚠⚠⚠ WHY A SECOND FILE, WHEN `PlatformAppendOnlyGuardTests` SITS BESIDE IT ASSERTING THE SAME RULE.
//
// ***THERE ARE TWO `PreventAppendOnlyMutation` METHODS. SAME NAME, SAME BODY SHAPE, DIFFERENT CLASS.***
// That file drives `PlatformDbContext` and says so in its own header. **This one drives `TenantDbContext`,
// and until this file existed nothing at the merge gate drove it behaviourally at all.**
//
// *Measured, not assumed:* removing `EntityState.Deleted` from `TenantDbContext.PreventAppendOnlyMutation`
// leaves delete unrefused and **1,131 `Platform.Tests` pass — including every test in the neighbouring
// file.** It watches the other class, so it cannot see this one break.
//
// ⚠ AND THE THREE TYPES THAT RIDE ON THIS ARE THE REASON IT IS WORTH A FILE RATHER THAN A NOTE:
// **`JournalLine`, `PayrollRunLine` and `AttendanceRecord`** — the posted ledger, what people were paid,
// and the record of when they worked. *All three were guarded behaviourally only by `Integration.Tests`,
// which is outside `GATE_SCOPE=TASK` and last ran green on 2026-09-01.* **That is a real tier conversion:
// green-at-a-date becomes green-at-every-merge.**
//
// ⚠⚠ THE SHAPE IS THE NEIGHBOURING FILE'S, DELIBERATELY, AND SO IS THE FIXTURE. A different harness for
// the same rule would let the two contexts' guards drift apart while both files stayed green — *which is
// the failure this whole pair exists to prevent, applied to the tests instead of the code.*
//
// ⚠⚠⚠ THE PROBES ARE NOT TENANT-, COMPANY- OR BRANCH-OWNED, AND THAT IS LOAD-BEARING RATHER THAN LAZY.
// `TenantDbContext` fences those three dimensions on the same save path. **A probe carrying any of them
// could be refused by the tenant fence and reported here as an append-only refusal** — the two are the same
// exception type, and the test would pass for the wrong reason. *The probe carries the ONE marker whose
// rule is under test and nothing else.*
public sealed class TenantAppendOnlyGuardTests
{
  [Fact]
  public async Task Writing_an_append_only_record_is_permitted()
  {
    // ---- THE CONTROL, AND WITHOUT IT EVERY REFUSAL BELOW IS SATISFIED BY A CONTEXT THAT REFUSES ALL
    // ---- WRITES. A guard never observed to permit anything is indistinguishable from one broken shut.
    await using var scope = await TenantAppendOnlyScope.CreateAsync();

    scope.Context.Add(new AppendOnlyProbe { Note = "written" });
    await scope.Context.SaveChangesAsync();

    Assert.Equal("written", (await scope.ReadBackAsync()).Note);
  }

  [Fact]
  public async Task Modifying_a_written_append_only_record_is_refused()
  {
    await using var scope = await TenantAppendOnlyScope.CreateAsync();
    var probe = new AppendOnlyProbe { Note = "written" };
    scope.Context.Add(probe);
    await scope.Context.SaveChangesAsync();

    probe.Note = "rewritten";

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync());

    // The MESSAGE, not merely the type. `InvalidOperationException` is what EF throws for a dozen
    // unrelated faults, so asserting the type alone would pass on a provider error.
    Assert.Equal(
      "Append-only records cannot be modified or deleted after they are written.",
      error.Message);

    // ---- AND THE DATABASE IS UNCHANGED, read through a SECOND context so the assertion sees the row
    // rather than the tracked instance whose in-memory value was changed before the refusal.
    Assert.Equal("written", (await scope.ReadBackAsync()).Note);
  }

  [Fact]
  public async Task Deleting_a_written_append_only_record_is_refused()
  {
    await using var scope = await TenantAppendOnlyScope.CreateAsync();
    var probe = new AppendOnlyProbe { Note = "written" };
    scope.Context.Add(probe);
    await scope.Context.SaveChangesAsync();

    scope.Context.Remove(probe);

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync());

    Assert.Equal(
      "Append-only records cannot be modified or deleted after they are written.",
      error.Message);

    // ⚠ THE ROW SURVIVES. The exception alone would pass on a guard that threw AFTER the delete reached
    // the database, which is a different and worse defect than refusing.
    Assert.Equal(1, await scope.Context.Set<AppendOnlyProbe>().CountAsync());
  }

  // ---- THE INNER OVERLOAD. EF routes `SaveChangesAsync(ct)` to `SaveChangesAsync(bool, ct)` by virtual
  // dispatch, so a rule fenced only on the outer one is bypassed by anything calling the inner directly.
  [Fact]
  public async Task The_inner_overload_cannot_be_used_to_bypass_the_guard()
  {
    await using var scope = await TenantAppendOnlyScope.CreateAsync();
    var probe = new AppendOnlyProbe { Note = "written" };
    scope.Context.Add(probe);
    await scope.Context.SaveChangesAsync();

    probe.Note = "rewritten";

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None));

    Assert.Equal(
      "Append-only records cannot be modified or deleted after they are written.",
      error.Message);
    Assert.Equal("written", (await scope.ReadBackAsync()).Note);
  }

  // ---- ⚠⚠⚠ THE SYNCHRONOUS ENTRY POINT, AND THE TWO CONTEXTS DIFFER HERE. THE TENANT ONE IS STRICTER.
  //
  // ***I WROTE THIS EXPECTING THE SIBLING'S ANSWER AND THE FIXTURE CORRECTED ME.*** In
  // `PlatformAppendOnlyGuardTests` the synchronous path reaches the append-only guard and is refused BY it.
  // **`TenantDbContext` never gets that far: it refuses synchronous saves outright**, because the cutover
  // write fence (`ADR-020`) is async and *"falling through is precisely the unfenced path that would let a
  // write commit against a frozen tenant."*
  //
  // ⚠ SO THE PROPERTY IS THE SAME AND THE MECHANISM IS NOT, AND THIS ASSERTS THE MECHANISM THAT ACTUALLY
  // HOLDS. **Asserting the sibling's message here would have been a test that passes only while somebody
  // keeps a defect** — the day the tenant context stopped blocking synchronous saves and started merely
  // append-only-refusing them, this would go GREEN on a real regression of the write fence.
  //
  // *This is the harmonisation hazard pointed at a test rather than at production code: I nearly made two
  // contexts agree because their guards share a name.*
  [Fact]
  public async Task The_synchronous_entry_point_is_refused_before_it_can_reach_the_guard()
  {
    await using var scope = await TenantAppendOnlyScope.CreateAsync();
    var probe = new AppendOnlyProbe { Note = "written" };
    scope.Context.Add(probe);
    await scope.Context.SaveChangesAsync();

    probe.Note = "rewritten";

    var error = Assert.Throws<InvalidOperationException>(() => scope.Context.SaveChanges());

    // The write fence refuses first, and the message names it rather than the append-only rule.
    Assert.StartsWith(
      "Synchronous SaveChanges is not supported on TenantDbContext", error.Message, StringComparison.Ordinal);

    // ---- AND THE ROW IS STILL UNCHANGED, which is the guarantee both mechanisms exist to provide. The
    // assertion above says WHICH rule refused; this one says the outcome was the right one either way.
    Assert.Equal("written", (await scope.ReadBackAsync()).Note);
  }

  // ---- AND AN UNMARKED RECORD ON THE SAME CONTEXT STAYS MUTABLE.
  //
  // Without this, a guard that refused EVERY update on the tenant context would pass all of the above and
  // break the rest of the tenant database. It is the same control the neighbouring file carries, and it is
  // the one that makes "append-only" a property of the MARKER rather than of the context.
  [Fact]
  public async Task An_unmarked_record_on_the_same_context_remains_mutable()
  {
    await using var scope = await TenantAppendOnlyScope.CreateAsync();
    var probe = new MutableProbe { Note = "written" };
    scope.Context.Add(probe);
    await scope.Context.SaveChangesAsync();

    probe.Note = "rewritten";
    await scope.Context.SaveChangesAsync();

    Assert.Equal("rewritten", (await scope.Context.Set<MutableProbe>().SingleAsync()).Note);
  }

  private sealed class TenantAppendOnlyScope : IAsyncDisposable
  {
    private readonly SqliteConnection connection;

    private TenantAppendOnlyScope(SqliteConnection connection, TenantDbContext context)
    {
      this.connection = connection;
      Context = context;
    }

    public TenantDbContext Context { get; }

    public static async Task<TenantAppendOnlyScope> CreateAsync()
    {
      var connection = new SqliteConnection("Data Source=:memory:");
      await connection.OpenAsync();

      return new TenantAppendOnlyScope(connection, await BuildAsync(connection, create: true));
    }

    // Read through a SECOND context so an assertion sees the database rather than the tracked instance.
    public async Task<AppendOnlyProbe> ReadBackAsync()
    {
      await using var reader = await BuildAsync(connection, create: false);

      return await reader.Set<AppendOnlyProbe>().AsNoTracking().SingleAsync();
    }

    // ---- THE WRITE FENCES ARE LEFT NULL, WHICH THE CONTEXT DOCUMENTS AS FAIL-CLOSED RATHER THAN OPEN.
    //
    // `ITenantWriteFence`, `IBranchWriteAuthorizer` and the company authorizer are optional because the
    // maintenance builders construct this context for schema work. Null never PERMITS a fenced write — it
    // refuses one. The probes below carry none of those markers, so no fence engages and the only rule in
    // play is the one under test.
    private static async Task<TenantDbContext> BuildAsync(SqliteConnection connection, bool create)
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlite(connection)
        .ReplaceService<IModelCustomizer, TenantProbeModelCustomizer>()
        .Options;

      var context = new TenantDbContext(
        options, new StubCurrentUser(), new StubCurrentTenant(), new StubClock());

      if (create)
      {
        // Only the probe tables, not the whole tenant schema. `EnsureCreated` would translate every tenant
        // configuration into SQLite — a different provider from the one they were written for — and a
        // provider mismatch would be reported here as an append-only failure.
        await context.Database.ExecuteSqlRawAsync(
          "CREATE TABLE TenantAppendOnlyProbes (Id TEXT NOT NULL PRIMARY KEY, Note TEXT NOT NULL);");
        await context.Database.ExecuteSqlRawAsync(
          "CREATE TABLE TenantMutableProbes (Id TEXT NOT NULL PRIMARY KEY, Note TEXT NOT NULL);");
      }

      return context;
    }

    public async ValueTask DisposeAsync()
    {
      await Context.DisposeAsync();
      await connection.DisposeAsync();
    }
  }

  // Injects the probes into the REAL TenantDbContext model, leaving production configuration untouched.
  private sealed class TenantProbeModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
  {
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
      base.Customize(modelBuilder, context);

      modelBuilder.Entity<AppendOnlyProbe>(entity =>
      {
        entity.ToTable("TenantAppendOnlyProbes");
        entity.HasKey(probe => probe.Id);
        entity.Property(probe => probe.Note).IsRequired();
      });

      modelBuilder.Entity<MutableProbe>(entity =>
      {
        entity.ToTable("TenantMutableProbes");
        entity.HasKey(probe => probe.Id);
        entity.Property(probe => probe.Note).IsRequired();
      });
    }
  }

  private sealed class AppendOnlyProbe : IAppendOnlyEntity
  {
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Note { get; set; } = string.Empty;
  }

  private sealed class MutableProbe
  {
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Note { get; set; } = string.Empty;
  }

  private sealed class StubCurrentUser : ICurrentUser
  {
    public string? UserId => "tenant-append-only-tests";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class StubCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class StubClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
  }
}
