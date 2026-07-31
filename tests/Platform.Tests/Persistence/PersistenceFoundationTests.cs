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
    var otherUnitOfWork = new EfUnitOfWork<TestPersistenceDbContext>(otherContext, new RecordingDomainEventDispatcher());
    otherContext.Aggregates.Add(new TestAggregate("tenant-two"));
    await otherUnitOfWork.SaveChangesAsync();

    Assert.Single(await scope.Context.Aggregates.ToListAsync());
    Assert.Single(await otherContext.Aggregates.ToListAsync());

    await using var missingTenantContext = scope.CreateContext(null, new RecordingDomainEventDispatcher());
    var missingTenantUnitOfWork = new EfUnitOfWork<TestPersistenceDbContext>(missingTenantContext, new RecordingDomainEventDispatcher());
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
      UnitOfWork = new EfUnitOfWork<TestPersistenceDbContext>(context, dispatcher);
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
    public Guid? CompanyId => null;
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
