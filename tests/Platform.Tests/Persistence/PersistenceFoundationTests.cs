using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Platform.Tests.Persistence;

public sealed class PersistenceFoundationTests
{
  [Fact]
  public async Task Save_changes_assigns_utc_audit_fields_and_the_trusted_tenant()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    var aggregate = new TestAggregate("audit");
    scope.Context.Aggregates.Add(aggregate);

    await scope.UnitOfWork.SaveChangesAsync();

    Assert.Equal(scope.TenantId, aggregate.TenantId);
    Assert.Equal(scope.Clock.UtcNow, aggregate.CreatedUtc);
    Assert.Equal(scope.Clock.UtcNow, aggregate.ModifiedUtc);
    Assert.Equal("test-user", aggregate.CreatedBy);
    Assert.Equal("test-user", aggregate.ModifiedBy);
    Assert.Equal(TimeSpan.Zero, aggregate.CreatedUtc.Offset);
  }

  // ================================================================================================
  // ⚠⚠⚠ TWO CHARACTERISATION TESTS. THEY RECORD WHAT THE CODE DOES TODAY AND RULE ON NOTHING.
  // ================================================================================================
  //
  // ***NEITHER OF THESE IS A CHANGE REQUEST AND NEITHER RATIFIES THE BEHAVIOUR IT PINS.*** They exist
  // because both mechanisms fail **silently** — no exception, no error code, wrong or absent data — and a
  // silent behaviour that nothing asserts is indistinguishable from one nobody chose. *A reader six months
  // from now should not read these as approval; if the behaviour is wrong, the test changes with it and the
  // change will at least be visible in a diff.*
  //
  // Both were identified while mapping what a bulk data migration would have to reproduce by hand. The write
  // path stamps, refuses and filters on the caller's behalf, and **a loader that goes around it inherits
  // every one of those obligations without inheriting the enforcement.**

  // ---- ⚠⚠⚠ A ROW WRITTEN UNDER THE WRONG *AMBIENT CONTEXT* IS NOT REFUSED. IT BECOMES INVISIBLE.
  //
  // ***THE PRECISION IN THAT HEADING IS THE WHOLE FINDING, AND THE LOOSER VERSION OF IT IS FALSE.***
  // A row whose `TenantId` CONFLICTS with the trusted context is **refused, loudly**: `AssignTenant` throws
  // *"Tenant ownership must match the trusted tenant context."* **So "a wrong `TenantId` is silently
  // swallowed" is NOT true of this code**, and an earlier statement of this finding — including the first
  // draft of this comment — said exactly that.
  //
  // The silent path is the OTHER one. `AssignTenant` fills an EMPTY `TenantId` from the ambient context, so
  // a save made under the wrong trusted tenant produces a row that is ***wrong but internally consistent***
  // — nothing to conflict with, nothing to throw. The global query filter
  // (`CurrentTenantId.HasValue && entity.TenantId == CurrentTenantId.Value`) then hides it from every read.
  // **Still in the table, unreachable through the application, permanently, with nothing raised at write
  // time and nothing raised at read time.**
  //
  // ⚠⚠ AND THE LIMIT THAT MATTERS MOST TO ANYONE READING THIS FOR A DATA MIGRATION:
  // ***BOTH TESTS IN THIS PAIR CHARACTERISE THE EF PATH ONLY.*** `ApplyPersistenceRules` runs from
  // `SaveChanges`. **A raw `INSERT` reaches neither stamper, neither refusal and neither of these tests** —
  // and raw SQL is one of the options on the table for a bulk load. *So these establish "if you go through
  // EF, this happens" and say nothing whatever about the path most likely to be chosen for a large load.*
  // ***ASK WHO CALLS THIS. THE MIGRATION MIGHT NOT.***
  //
  // ***THE ASSERTION PAIR IS THE POINT: `Empty` THROUGH THE FILTER AND `Single` THROUGH
  // `IgnoreQueryFilters` — one of those alone would be indistinguishable from the row never being written.***
  // The row exists; the reader simply cannot see it, and no instrument in the product will say so.
  [Fact]
  public async Task A_row_belonging_to_another_tenant_is_invisible_rather_than_refused()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    scope.Context.Aggregates.Add(new TestAggregate("owned-by-the-trusted-tenant"));
    await scope.UnitOfWork.SaveChangesAsync();

    // A second reader on the SAME database, acting as a different tenant.
    await using var other = scope.CreateContext(Guid.NewGuid(), new RecordingDomainEventDispatcher());

    Assert.Empty(await other.Aggregates.ToListAsync());
    Assert.Single(await other.Aggregates.IgnoreQueryFilters().ToListAsync());
  }

  // ---- ⚠⚠⚠ AUDIT STAMPS ARE UNCONDITIONAL ON `Added`. A SUPPLIED VALUE IS OVERWRITTEN, NOT HONOURED.
  //
  // `ApplyPersistenceRules` assigns `CreatedUtc`/`CreatedBy` for every added `IAuditableEntity` with **no
  // `if empty` guard** — unlike `AssignTenant` and `AssignCompany`, which confirm a supplied value and
  // refuse a conflicting one. *The asymmetry is the whole finding:* two stampers on the same save path
  // treat a caller-supplied value in opposite ways, and only one of them tells the caller.
  //
  // ***CONSEQUENCE, STATED BECAUSE IT IS WHY THIS TEST EXISTS: ANY LOAD THROUGH EF CANNOT CARRY A SOURCE
  // SYSTEM'S CREATION HISTORY.*** Every migrated row would read "created today, by the migration user", and
  // the original values would be discarded on the way in with no error. For hire dates and posting dates
  // that is the substance rather than metadata.
  [Fact]
  public async Task Supplied_audit_stamps_are_overwritten_rather_than_confirmed_or_refused()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    var historical = new DateTimeOffset(2019, 3, 4, 9, 30, 0, TimeSpan.Zero);
    var aggregate = new TestAggregate("carries-its-own-history")
    {
      CreatedUtc = historical,
      ModifiedUtc = historical,
      CreatedBy = "the-source-system",
      ModifiedBy = "the-source-system",
    };
    scope.Context.Aggregates.Add(aggregate);

    await scope.UnitOfWork.SaveChangesAsync();

    Assert.NotEqual(historical, aggregate.CreatedUtc);
    Assert.Equal(scope.Clock.UtcNow, aggregate.CreatedUtc);
    Assert.NotEqual("the-source-system", aggregate.CreatedBy);
    Assert.Equal("test-user", aggregate.CreatedBy);
  }

  [Fact]
  public async Task Commit_persists_changes_then_dispatches_and_clears_domain_events()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    var aggregate = new TestAggregate("commit");
    aggregate.Raise(new TestDomainEvent(Guid.NewGuid(), scope.Clock.UtcNow));
    scope.Context.Aggregates.Add(aggregate);

    await using var transaction = await scope.UnitOfWork.BeginTransactionAsync();
    await transaction.CommitAsync();

    Assert.Single(scope.Dispatcher.DispatchedEvents);
    Assert.Empty(aggregate.DomainEvents);
    Assert.Equal(1, await scope.Context.Aggregates.IgnoreQueryFilters().CountAsync());
  }

  [Fact]
  public async Task Failed_save_does_not_dispatch_or_clear_domain_events()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    var aggregate = new TestAggregate(null!);
    aggregate.Raise(new TestDomainEvent(Guid.NewGuid(), scope.Clock.UtcNow));
    scope.Context.Aggregates.Add(aggregate);

    await Assert.ThrowsAsync<DbUpdateException>(() => scope.UnitOfWork.SaveChangesAsync());

    Assert.Empty(scope.Dispatcher.DispatchedEvents);
    Assert.Single(aggregate.DomainEvents);
  }

  [Fact]
  public async Task Rollback_discards_saved_changes_and_does_not_dispatch_events()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    var aggregate = new TestAggregate("rollback");
    aggregate.Raise(new TestDomainEvent(Guid.NewGuid(), scope.Clock.UtcNow));
    scope.Context.Aggregates.Add(aggregate);

    await using var transaction = await scope.UnitOfWork.BeginTransactionAsync();
    await scope.UnitOfWork.SaveChangesAsync();
    await transaction.RollbackAsync();

    Assert.Empty(scope.Dispatcher.DispatchedEvents);
    Assert.Equal(0, await scope.Context.Aggregates.IgnoreQueryFilters().CountAsync());
    Assert.Single(aggregate.DomainEvents);
  }

  [Fact]
  public async Task Nested_transactions_are_rejected()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    await using var transaction = await scope.UnitOfWork.BeginTransactionAsync();

    await Assert.ThrowsAsync<InvalidOperationException>(() => scope.UnitOfWork.BeginTransactionAsync());

    await transaction.RollbackAsync();
  }

  [Fact]
  public async Task Cancelled_save_does_not_write_or_dispatch_events()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    var aggregate = new TestAggregate("cancelled");
    aggregate.Raise(new TestDomainEvent(Guid.NewGuid(), scope.Clock.UtcNow));
    scope.Context.Aggregates.Add(aggregate);
    using var cancellationSource = new CancellationTokenSource();
    cancellationSource.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.UnitOfWork.SaveChangesAsync(cancellationSource.Token));

    Assert.Empty(scope.Dispatcher.DispatchedEvents);
    Assert.Equal(0, await scope.Context.Aggregates.IgnoreQueryFilters().CountAsync());
  }

  [Fact]
  public async Task Tenant_query_filter_isolates_test_entities_and_missing_tenant_is_rejected()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    scope.Context.Aggregates.Add(new TestAggregate("tenant-one"));
    await scope.UnitOfWork.SaveChangesAsync();

    var otherTenantId = Guid.NewGuid();
    await using var otherContext = scope.CreateContext(otherTenantId, new RecordingDomainEventDispatcher());
    var otherUnitOfWork = new EfUnitOfWork<TestPersistenceDbContext>(otherContext, new RecordingDomainEventDispatcher(),
        NullLogger<EfUnitOfWork<TestPersistenceDbContext>>.Instance);
    otherContext.Aggregates.Add(new TestAggregate("tenant-two"));
    await otherUnitOfWork.SaveChangesAsync();

    Assert.Single(await scope.Context.Aggregates.ToListAsync());
    Assert.Single(await otherContext.Aggregates.ToListAsync());

    await using var missingTenantContext = scope.CreateContext(null, new RecordingDomainEventDispatcher());
    var missingTenantUnitOfWork = new EfUnitOfWork<TestPersistenceDbContext>(missingTenantContext, new RecordingDomainEventDispatcher(),
        NullLogger<EfUnitOfWork<TestPersistenceDbContext>>.Instance);
    missingTenantContext.Aggregates.Add(new TestAggregate("missing-tenant"));

    await Assert.ThrowsAsync<InvalidOperationException>(() => missingTenantUnitOfWork.SaveChangesAsync());
  }

  [Fact]
  public async Task Date_time_offset_convention_persists_values_in_utc()
  {
    await using var scope = await PersistenceTestScope.CreateAsync();
    var aggregate = new TestAggregate("utc")
    {
      ObservedAt = new DateTimeOffset(2026, 7, 30, 16, 0, 0, TimeSpan.FromHours(3))
    };
    scope.Context.Aggregates.Add(aggregate);
    await scope.UnitOfWork.SaveChangesAsync();

    await using var readContext = scope.CreateContext(scope.TenantId, new RecordingDomainEventDispatcher());
    var persisted = await readContext.Aggregates.SingleAsync();

    Assert.Equal(TimeSpan.Zero, persisted.ObservedAt.Offset);
    Assert.Equal(aggregate.ObservedAt.UtcDateTime, persisted.ObservedAt.UtcDateTime);
  }

  private sealed class PersistenceTestScope : IAsyncDisposable
  {
    private readonly SqliteConnection connection;

    private PersistenceTestScope(
      SqliteConnection connection,
      TestPersistenceDbContext context,
      TestClock clock,
      Guid tenantId,
      RecordingDomainEventDispatcher dispatcher)
    {
      this.connection = connection;
      Context = context;
      Clock = clock;
      TenantId = tenantId;
      Dispatcher = dispatcher;
      UnitOfWork = new EfUnitOfWork<TestPersistenceDbContext>(context, dispatcher,
        NullLogger<EfUnitOfWork<TestPersistenceDbContext>>.Instance);
    }

    public TestPersistenceDbContext Context { get; }

    public TestClock Clock { get; }

    public Guid TenantId { get; }

    public RecordingDomainEventDispatcher Dispatcher { get; }

    public EfUnitOfWork<TestPersistenceDbContext> UnitOfWork { get; }

    public static async Task<PersistenceTestScope> CreateAsync()
    {
      var connection = new SqliteConnection("Data Source=:memory:");
      await connection.OpenAsync();
      var clock = new TestClock(new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero));
      var tenantId = Guid.NewGuid();
      var dispatcher = new RecordingDomainEventDispatcher();
      var context = CreateContext(connection, tenantId, clock);
      await context.Database.EnsureCreatedAsync();

      return new PersistenceTestScope(connection, context, clock, tenantId, dispatcher);
    }

    public TestPersistenceDbContext CreateContext(Guid? tenantId, RecordingDomainEventDispatcher dispatcher)
    {
      return CreateContext(connection, tenantId, Clock);
    }

    public async ValueTask DisposeAsync()
    {
      await Context.DisposeAsync();
      await connection.DisposeAsync();
    }

    private static TestPersistenceDbContext CreateContext(SqliteConnection connection, Guid? tenantId, TestClock clock)
    {
      var options = new DbContextOptionsBuilder<TestPersistenceDbContext>()
        .UseSqlite(connection)
        .Options;

      return new TestPersistenceDbContext(
        options,
        new TestCurrentUser("test-user"),
        new TestCurrentTenant(tenantId),
        clock);
    }
  }

  private sealed class TestPersistenceDbContext(
    DbContextOptions<TestPersistenceDbContext> options,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IDateTimeProvider dateTimeProvider) : PersistenceDbContext(options, currentUser, currentTenant, dateTimeProvider)
  {
    public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.Entity<TestAggregate>(entity =>
      {
        entity.HasKey(aggregate => aggregate.Id);
        entity.Property(aggregate => aggregate.Name).IsRequired();
      });

      base.OnModelCreating(modelBuilder);
    }
  }

  private sealed class TestAggregate(string name) : AggregateRoot<Guid>(Guid.NewGuid()), IAuditableEntity, ITenantOwnedEntity
  {
    public string Name { get; set; } = name;

    public DateTimeOffset ObservedAt { get; set; }

    public Guid TenantId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    public void Raise(DomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
  }

  private sealed record TestDomainEvent(Guid EventId, DateTimeOffset OccurredUtc)
    : DomainEvent(EventId, OccurredUtc);

  private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
  {
    public List<DomainEvent> DispatchedEvents { get; } = [];

    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      DispatchedEvents.AddRange(domainEvents);
      return Task.CompletedTask;
    }
  }

  private sealed class TestCurrentUser(string? userId) : ICurrentUser
  {
    public string? UserId { get; } = userId;
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestCurrentTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class TestClock(DateTimeOffset utcNow) : IDateTimeProvider
  {
    public DateTimeOffset UtcNow { get; } = utcNow;
  }
}
