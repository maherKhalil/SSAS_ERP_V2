using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Tests.Persistence;

// THE APPEND-ONLY GUARD ON THE PLATFORM DATABASE (FP-014 AC-SUB-0044, TS-SUB-0033).
//
// `TenantDbContext` has refused Modified and Deleted for `IAppendOnlyEntity` since FP-006C3.
// `PlatformDbContext` did not, and `OD-SUB-0008` rules the FP-014 subscription history append-only while
// `ADR-017` places that history in the PLATFORM database. The rule therefore rested on a mechanism absent
// from the side of the product where the data lives.
//
// ---- THESE TESTS FAIL WITHOUT THE GUARD, WHICH IS THE ONLY REASON THEY ARE WORTH HAVING.
//
// Each writes the row first and mutates it second, against a real SQLite table. Remove
// `PreventAppendOnlyMutation` from `PlatformDbContext` and the mutation SUCCEEDS — the update or the
// delete commits, `Assert.ThrowsAsync` finds no exception, and the test goes red. A test that passed
// either way would assert that the code nobody exercised did not break the code nobody changed.
//
// ---- THE TEST ENTITY IS TEST-ONLY, AND DELIBERATELY SO.
//
// No production Platform entity implements `IAppendOnlyEntity` today and none is added here — FP-014's
// entities arrive with FP-014. The entity is injected into the real `PlatformDbContext` model through a
// replaced `IModelCustomizer`, so the guard under test is the production one on the production context
// rather than a copy of it. Adding a production marker to make a test compile would be the opposite
// trade: changing shipped code to suit a test.
public sealed class PlatformAppendOnlyGuardTests
{
  [Fact]
  public async Task Writing_an_append_only_record_is_allowed()
  {
    await using var scope = await AppendOnlyScope.CreateAsync();
    scope.Context.Add(new AppendOnlyProbe { Note = "written" });

    await scope.Context.SaveChangesAsync();

    Assert.Equal(1, await scope.Context.Set<AppendOnlyProbe>().CountAsync());
  }

  // The guard must refuse the SECOND write, not the first. A guard that refused Added as well would be
  // indistinguishable from a broken table, and every append-only record in the product is written once.
  [Fact]
  public async Task Modifying_a_written_append_only_record_is_refused()
  {
    await using var scope = await AppendOnlyScope.CreateAsync();
    var probe = new AppendOnlyProbe { Note = "written" };
    scope.Context.Add(probe);
    await scope.Context.SaveChangesAsync();

    probe.Note = "rewritten";

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync());

    Assert.Equal(
      "Append-only records cannot be modified or deleted after they are written.",
      error.Message);
    Assert.Equal("written", (await scope.ReadBackAsync()).Note);
  }

  [Fact]
  public async Task Deleting_a_written_append_only_record_is_refused()
  {
    await using var scope = await AppendOnlyScope.CreateAsync();
    var probe = new AppendOnlyProbe { Note = "written" };
    scope.Context.Add(probe);
    await scope.Context.SaveChangesAsync();

    scope.Context.Remove(probe);

    var error = await Assert.ThrowsAsync<InvalidOperationException>(
      () => scope.Context.SaveChangesAsync());

    Assert.Equal(
      "Append-only records cannot be modified or deleted after they are written.",
      error.Message);
    Assert.Equal(1, await scope.Context.Set<AppendOnlyProbe>().CountAsync());
  }

  // ---- THE FENCE POINT IS THE POINT, AND THIS IS THE TEST THAT PROVES IT.
  //
  // EF Core routes `SaveChangesAsync(ct)` to `SaveChangesAsync(bool, ct)` by virtual dispatch, so a rule
  // hung on the convenience overload alone leaves a caller able to reach the inner one and commit straight
  // past it. `PersistenceDbContext` documents that exact lesson, and `PlatformDbContext`'s seven older
  // guards still sit on the convenience overload where this hazard lives.
  //
  // Move `PreventAppendOnlyMutation` up to `SaveChangesAsync(CancellationToken)` beside them and the three
  // tests above still pass while THIS one fails.
  [Fact]
  public async Task The_inner_overload_cannot_be_used_to_bypass_the_guard()
  {
    await using var scope = await AppendOnlyScope.CreateAsync();
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

  // The synchronous entry points route `SaveChanges()` -> `SaveChanges(bool)` the same way. Nothing in this
  // codebase writes synchronously today, which is exactly why an unfenced synchronous path would sit
  // unnoticed until something did.
  [Fact]
  public async Task The_synchronous_entry_point_cannot_be_used_to_bypass_the_guard()
  {
    await using var scope = await AppendOnlyScope.CreateAsync();
    var probe = new AppendOnlyProbe { Note = "written" };
    scope.Context.Add(probe);
    await scope.Context.SaveChangesAsync();

    probe.Note = "rewritten";

    var error = Assert.Throws<InvalidOperationException>(() => scope.Context.SaveChanges());

    Assert.Equal(
      "Append-only records cannot be modified or deleted after they are written.",
      error.Message);
  }

  // A type that is NOT marked append-only must remain freely mutable. Without this, a guard that refused
  // every update on the context would pass all of the above and break the rest of the Platform database.
  [Fact]
  public async Task An_unmarked_record_on_the_same_context_remains_mutable()
  {
    await using var scope = await AppendOnlyScope.CreateAsync();
    var probe = new MutableProbe { Note = "written" };
    scope.Context.Add(probe);
    await scope.Context.SaveChangesAsync();

    probe.Note = "rewritten";
    await scope.Context.SaveChangesAsync();

    var reloaded = await scope.Context.Set<MutableProbe>().SingleAsync();
    Assert.Equal("rewritten", reloaded.Note);
  }

  private sealed class AppendOnlyScope : IAsyncDisposable
  {
    private readonly SqliteConnection connection;

    private AppendOnlyScope(SqliteConnection connection, PlatformDbContext context)
    {
      this.connection = connection;
      Context = context;
    }

    public PlatformDbContext Context { get; }

    public static async Task<AppendOnlyScope> CreateAsync()
    {
      var connection = new SqliteConnection("Data Source=:memory:");
      await connection.OpenAsync();

      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlite(connection)
        .ReplaceService<IModelCustomizer, ProbeModelCustomizer>()
        .Options;

      var context = new PlatformDbContext(
        options,
        new StubCurrentUser(),
        new StubCurrentTenant(),
        new StubClock());

      // ---- ONLY THE PROBE TABLES ARE CREATED, NOT THE WHOLE PLATFORM SCHEMA.
      //
      // `EnsureCreated` would translate every Platform configuration into SQLite, which is a different
      // provider from the one those configurations were written for. This test is about a change-tracker
      // rule, not about schema portability, and creating the two tables it touches keeps a provider
      // mismatch from being reported as an append-only failure.
      await context.Database.ExecuteSqlRawAsync(
        "CREATE TABLE AppendOnlyProbes (Id TEXT NOT NULL PRIMARY KEY, Note TEXT NOT NULL);");
      await context.Database.ExecuteSqlRawAsync(
        "CREATE TABLE MutableProbes (Id TEXT NOT NULL PRIMARY KEY, Note TEXT NOT NULL);");

      return new AppendOnlyScope(connection, context);
    }

    // Read through a second context so the assertion sees the DATABASE, not the tracked instance whose
    // in-memory value was changed before the refusal.
    public async Task<AppendOnlyProbe> ReadBackAsync()
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlite(connection)
        .ReplaceService<IModelCustomizer, ProbeModelCustomizer>()
        .Options;

      await using var reader = new PlatformDbContext(
        options,
        new StubCurrentUser(),
        new StubCurrentTenant(),
        new StubClock());

      return await reader.Set<AppendOnlyProbe>().AsNoTracking().SingleAsync();
    }

    public async ValueTask DisposeAsync()
    {
      await Context.DisposeAsync();
      await connection.DisposeAsync();
    }
  }

  // Injects the probes into the REAL PlatformDbContext model, leaving production configuration untouched.
  private sealed class ProbeModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
  {
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
      base.Customize(modelBuilder, context);

      modelBuilder.Entity<AppendOnlyProbe>(entity =>
      {
        entity.ToTable("AppendOnlyProbes");
        entity.HasKey(probe => probe.Id);
        entity.Property(probe => probe.Note).IsRequired();
      });

      modelBuilder.Entity<MutableProbe>(entity =>
      {
        entity.ToTable("MutableProbes");
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
    public string? UserId => "append-only-tests";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
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
    public DateTimeOffset UtcNow => new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
  }
}
