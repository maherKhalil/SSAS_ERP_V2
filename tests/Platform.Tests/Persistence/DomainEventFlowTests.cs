using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SSAS.BuildingBlocks.Application.Abstractions.Diagnostics;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Tests.Persistence;

// ==================================================================================================
// THE DOMAIN-EVENT FLOW, EXERCISED RATHER THAN READ (item 166).
// ==================================================================================================
//
// Owner decision 17 recorded that no dispatcher existed. It was withdrawn on 2026-08-30 after the chain
// was traced by READING -- which is the same method that produced the original error. This exercises it:
// a real `EfUnitOfWork`, a real `DomainEventDispatcher`, a registered consumer, and a real
// `PlatformDbContext` on SQLite.
//
// ---- ⚠ THE CLAIM THAT MATTERS IS THE TRANSACTION ONE.
//
// `TerminateEmployeeCommandHandler` says "`EfUnitOfWork` dispatches domain events only when no
// transaction is open". Read alone, that sounds like events raised inside a transaction are LOST. They are
// not: `SaveChangesAsync` withholds them while a transaction is open and `CommitAsync` dispatches them
// after the commit succeeds. **Withheld, then released -- not dropped.** The rollback path never
// dispatches, which is the point: an event announcing work that was undone is worse than no event.
//
// Both halves are asserted below, because asserting only the first would pin "not dispatched" as the whole
// truth and read as a defect.
//
// ---- WHAT THIS DOES NOT COVER.
//
// The aggregate here is a PROBE, not a production one, and no command handler is involved. What is real is
// everything the event passes THROUGH: change tracker, unit of work, dispatcher, consumer registration and
// metadata. A production handler adds its own dependency graph without changing any of that, and a
// handler-level test belongs in `Integration.Tests` against a real database.
public sealed class DomainEventFlowTests
{
  [Fact]
  public async Task A_saved_aggregate_reaches_a_registered_consumer_with_metadata()
  {
    await using var scope = await FlowScope.CreateAsync();
    scope.Context.Set<FlowProbe>().Add(FlowProbe.Announcing("first"));

    await scope.UnitOfWork.SaveChangesAsync();

    var received = Assert.Single(scope.Consumer.Received);
    Assert.IsType<FlowProbeAnnounced>(received.Event);
    Assert.Equal("correlation-166", received.Metadata.CorrelationId);
    Assert.Equal("flow-tests", received.Metadata.ActorId);
    Assert.Equal("request-166", received.Metadata.RequestId);
  }

  // ---- THE AGGREGATE IS LEFT CLEAN, SO A SECOND SAVE CANNOT ANNOUNCE THE SAME EVENT AGAIN.
  [Fact]
  public async Task A_dispatched_event_is_cleared_and_is_not_announced_twice()
  {
    await using var scope = await FlowScope.CreateAsync();
    var probe = FlowProbe.Announcing("once");
    scope.Context.Set<FlowProbe>().Add(probe);

    await scope.UnitOfWork.SaveChangesAsync();
    await scope.UnitOfWork.SaveChangesAsync();

    Assert.Empty(probe.DomainEvents);
    Assert.Single(scope.Consumer.Received);
  }

  // ---- ⚠ HALF ONE OF THE TRANSACTION CLAIM: THE SAVE WITHHOLDS.
  [Fact]
  public async Task A_save_inside_an_open_transaction_dispatches_nothing_yet()
  {
    await using var scope = await FlowScope.CreateAsync();
    await using var transaction = await scope.UnitOfWork.BeginTransactionAsync();
    scope.Context.Set<FlowProbe>().Add(FlowProbe.Announcing("withheld"));

    await scope.UnitOfWork.SaveChangesAsync();

    Assert.Empty(scope.Consumer.Received);
  }

  // ---- ⚠ HALF TWO, AND THE ONE THAT SHOWS NOTHING IS LOST: THE COMMIT RELEASES.
  [Fact]
  public async Task Committing_the_transaction_dispatches_what_the_save_withheld()
  {
    await using var scope = await FlowScope.CreateAsync();
    await using var transaction = await scope.UnitOfWork.BeginTransactionAsync();
    scope.Context.Set<FlowProbe>().Add(FlowProbe.Announcing("released"));
    await scope.UnitOfWork.SaveChangesAsync();
    Assert.Empty(scope.Consumer.Received);

    await transaction.CommitAsync();

    var received = Assert.Single(scope.Consumer.Received);
    Assert.IsType<FlowProbeAnnounced>(received.Event);
  }

  // ---- AND THE REASON THE DESIGN IS THAT WAY: UNDONE WORK IS NEVER ANNOUNCED.
  [Fact]
  public async Task A_rolled_back_transaction_never_dispatches()
  {
    await using var scope = await FlowScope.CreateAsync();
    await using (var transaction = await scope.UnitOfWork.BeginTransactionAsync())
    {
      scope.Context.Set<FlowProbe>().Add(FlowProbe.Announcing("undone"));
      await scope.UnitOfWork.SaveChangesAsync();
      await transaction.RollbackAsync();
    }

    Assert.Empty(scope.Consumer.Received);
  }

  // ==================================================================================================
  // ⚠ THE LIMIT OF THE MECHANISM (item 167): DISPATCH COLLECTS FROM THE CHANGE TRACKER AND NOWHERE ELSE.
  // ==================================================================================================
  //
  // `DispatchDomainEventsAsync` reads `dbContext.ChangeTracker.Entries()`. An aggregate that raised events
  // while EF was not tracking it is invisible to that walk -- no error, no warning, no event.
  //
  // ⚠ THESE TESTS PIN THE DROP AS CURRENT BEHAVIOUR. They are not asserting that it is correct. Item 167
  // established that NO PRODUCTION PATH REACHES IT: every `AsNoTracking` query touching an event-raising
  // aggregate is a scalar existence check, a projection, or a read service returning DTOs, and no read
  // service is injected into a command handler. **The hazard is real and unreached** -- so it is pinned
  // here, where a future change that starts reaching it has something to disagree with.
  //
  // The second assertion in each is the one that carries the meaning: the events are STILL ON THE
  // AGGREGATE afterwards. That distinguishes "nothing was raised" from "something was raised and nobody
  // collected it", which is the whole difference between a quiet success and a silent drop.
  [Fact]
  public async Task An_aggregate_never_attached_is_not_dispatched_from()
  {
    await using var scope = await FlowScope.CreateAsync();
    var detached = FlowProbe.Announcing("never attached");

    await scope.UnitOfWork.SaveChangesAsync();

    Assert.Empty(scope.Consumer.Received);
    Assert.Single(detached.DomainEvents);
  }

  // ---- THE PRODUCTION-SHAPED VERSION: READ BACK WITH `AsNoTracking`, MUTATE, SAVE.
  [Fact]
  public async Task An_aggregate_read_with_no_tracking_and_then_mutated_is_not_dispatched_from()
  {
    await using var scope = await FlowScope.CreateAsync();
    scope.Context.Set<FlowProbe>().Add(FlowProbe.Announcing("stored"));
    await scope.UnitOfWork.SaveChangesAsync();
    scope.Consumer.Received.Clear();

    var untracked = await scope.Context.Set<FlowProbe>().AsNoTracking().SingleAsync();
    untracked.Announce();
    await scope.UnitOfWork.SaveChangesAsync();

    Assert.Empty(scope.Consumer.Received);
    Assert.Single(untracked.DomainEvents);
  }

  // ==================================================================================================
  // ⚠ A CONSUMER THAT THROWS AFTER THE COMMIT HAS ALREADY SUCCEEDED (item 172).
  // ==================================================================================================
  //
  // `EfUnitOfWork.CommitAsync` saves, commits, THEN dispatches -- all inside one `try`. A consumer that
  // throws therefore reaches a `catch` that calls `RollbackAsync` on an ALREADY-COMMITTED transaction.
  //
  // These tests establish what actually happens, rather than what the shape suggests. The second is the
  // one that matters: the data is written and the caller is told the command failed.
  [Fact]
  public async Task A_consumer_that_throws_after_commit_loses_its_exception_to_a_rollback_failure()
  {
    await using var scope = await FlowScope.CreateAsync(throwOnDispatch: true);
    var transaction = await scope.UnitOfWork.BeginTransactionAsync();
    scope.Context.Set<FlowProbe>().Add(FlowProbe.Announcing("committed then thrown"));
    await scope.UnitOfWork.SaveChangesAsync();

    var fromCommit = await Assert.ThrowsAnyAsync<Exception>(() => transaction.CommitAsync());

    // ⚠ NOT the consumer's exception. The `catch` calls `RollbackAsync` on the already-committed
    // transaction, THAT throws, and the provider error replaces the consumer failure before `throw;` is
    // ever reached. Asserted on shape rather than on the SQLite wording, which is provider-specific.
    Assert.DoesNotContain("consumer failed after commit", fromCommit.Message, StringComparison.Ordinal);
    Assert.Contains("transaction", fromCommit.Message, StringComparison.OrdinalIgnoreCase);
  }

  // ---- ⚠ AND DISPOSING THE TRANSACTION THEN THROWS A SECOND TIME, MASKING THE FIRST.
  // `CommitAsync` threw before setting `completed`, so `DisposeAsync` believes the transaction is still
  // open and rolls back -- but `EfUnitOfWork`'s `finally` has already cleared its field, so the rollback
  // is refused. In an `await using` block this exception REPLACES whatever the body was propagating.
  [Fact]
  public async Task Disposing_after_that_failure_throws_again_and_masks_the_first_exception()
  {
    await using var scope = await FlowScope.CreateAsync(throwOnDispatch: true);
    var transaction = await scope.UnitOfWork.BeginTransactionAsync();
    scope.Context.Set<FlowProbe>().Add(FlowProbe.Announcing("masked"));
    await scope.UnitOfWork.SaveChangesAsync();
    await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());

    var fromDispose = await Assert.ThrowsAsync<InvalidOperationException>(
      async () => await transaction.DisposeAsync());

    Assert.Equal("The transaction is no longer active.", fromDispose.Message);
  }

  // ---- ⚠ AND THE COMMIT STUCK. THE CALLER SAW TWO FAILURES AND THE ROW IS IN THE DATABASE.
  [Fact]
  public async Task The_row_is_committed_even_though_the_caller_saw_a_failure()
  {
    await using var scope = await FlowScope.CreateAsync(throwOnDispatch: true);
    var transaction = await scope.UnitOfWork.BeginTransactionAsync();
    scope.Context.Set<FlowProbe>().Add(FlowProbe.Announcing("durable"));
    await scope.UnitOfWork.SaveChangesAsync();
    await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());

    var rows = await scope.Context.Set<FlowProbe>().AsNoTracking().CountAsync();

    Assert.Equal(1, rows);
  }

  private sealed class FlowScope : IAsyncDisposable
  {
    private readonly SqliteConnection connection;

    private FlowScope(SqliteConnection connection, PlatformDbContext context, RecordingConsumer consumer)
    {
      this.connection = connection;
      Context = context;
      Consumer = consumer;
      UnitOfWork = new EfUnitOfWork<PlatformDbContext>(
        context,
        new DomainEventDispatcher(
          [consumer],
          new StubCorrelation(),
          new StubRequestMetadata(),
          new StubCurrentUser()));
    }

    public PlatformDbContext Context { get; }

    public RecordingConsumer Consumer { get; }

    public EfUnitOfWork<PlatformDbContext> UnitOfWork { get; }

    public Exception? Observed { get; set; }

    public static async Task<FlowScope> CreateAsync(bool throwOnDispatch = false)
    {
      var connection = new SqliteConnection("Data Source=:memory:");
      await connection.OpenAsync();

      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlite(connection)
        .ReplaceService<IModelCustomizer, FlowModelCustomizer>()
        .Options;

      var context = new PlatformDbContext(
        options, new StubCurrentUser(), new StubCurrentTenant(), new StubClock());

      // Only the probe table, for the reason `PlatformAppendOnlyGuardTests` states: translating the whole
      // Platform configuration into SQLite would report a provider mismatch as a dispatch failure.
      await context.Database.ExecuteSqlRawAsync(
        "CREATE TABLE FlowProbes (Id TEXT NOT NULL PRIMARY KEY, Note TEXT NOT NULL);");

      return new FlowScope(connection, context, new RecordingConsumer { Throws = throwOnDispatch });
    }

    public async ValueTask DisposeAsync()
    {
      await Context.DisposeAsync();
      await connection.DisposeAsync();
    }
  }

  private sealed class FlowModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
  {
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
      base.Customize(modelBuilder, context);

      modelBuilder.Entity<FlowProbe>(entity =>
      {
        entity.ToTable("FlowProbes");
        entity.HasKey(probe => probe.Id);
        entity.Property(probe => probe.Note).IsRequired();
        entity.Ignore(probe => probe.DomainEvents);
      });
    }
  }

  private sealed class FlowProbe : AggregateRoot<Guid>
  {
    private FlowProbe(Guid id, string note)
      : base(id) => Note = note;

    public string Note { get; private set; }

    public static FlowProbe Announcing(string note)
    {
      var probe = new FlowProbe(Guid.NewGuid(), note);
      probe.Announce();

      return probe;
    }

    public void Announce() =>
      RaiseDomainEvent(new FlowProbeAnnounced(Guid.NewGuid(), DateTimeOffset.UnixEpoch, Id));
  }

  private sealed record FlowProbeAnnounced(Guid EventId, DateTimeOffset OccurredUtc, Guid ProbeId)
    : DomainEvent(EventId, OccurredUtc);

  private sealed class RecordingConsumer : IDomainEventConsumer
  {
    public List<(DomainEvent Event, DomainEventDispatchMetadata Metadata)> Received { get; } = [];

    public bool Throws { get; init; }

    public Task HandleAsync(
      DomainEvent domainEvent,
      DomainEventDispatchMetadata metadata,
      CancellationToken cancellationToken = default)
    {
      Received.Add((domainEvent, metadata));

      return Throws
        ? Task.FromException(new InvalidOperationException("consumer failed after commit"))
        : Task.CompletedTask;
    }
  }

  private sealed class StubCorrelation : ICorrelationContext
  {
    public string CorrelationId => "correlation-166";
  }

  private sealed class StubRequestMetadata : IRequestMetadata
  {
    public string? RequestId => "request-166";
  }

  private sealed class StubCurrentUser : ICurrentUser
  {
    public string? UserId => "flow-tests";
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
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}
