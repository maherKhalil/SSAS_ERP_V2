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

    public static async Task<FlowScope> CreateAsync()
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

      return new FlowScope(connection, context, new RecordingConsumer());
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
      probe.RaiseDomainEvent(new FlowProbeAnnounced(Guid.NewGuid(), DateTimeOffset.UnixEpoch, probe.Id));

      return probe;
    }
  }

  private sealed record FlowProbeAnnounced(Guid EventId, DateTimeOffset OccurredUtc, Guid ProbeId)
    : DomainEvent(EventId, OccurredUtc);

  private sealed class RecordingConsumer : IDomainEventConsumer
  {
    public List<(DomainEvent Event, DomainEventDispatchMetadata Metadata)> Received { get; } = [];

    public Task HandleAsync(
      DomainEvent domainEvent,
      DomainEventDispatchMetadata metadata,
      CancellationToken cancellationToken = default)
    {
      Received.Add((domainEvent, metadata));

      return Task.CompletedTask;
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
