using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Application.Subscriptions.EnabledModules;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;
using Xunit;

namespace SSAS.Platform.Tests.Subscriptions.EnabledModules;

public sealed class GetEnabledModulesQueryHandlerTests
{
    private readonly StubCurrentTenant _currentTenant = new();
    private readonly StubTenantEntitlementReader _reader = new();
    private readonly StubTenantEntitlementCache _cache = new();
    private readonly StubDateTimeProvider _clock = new();
    private readonly GetEnabledModulesQueryHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetEnabledModulesQueryHandlerTests()
    {
        _handler = new GetEnabledModulesQueryHandler(_currentTenant, _reader, _cache, _clock);
        _currentTenant.TenantIdValue = _tenantId;
    }

    [Fact]
    public async Task Handle_TenantIdEmpty_ReturnsEmpty()
    {
        _currentTenant.TenantIdValue = Guid.Empty;
        var result = await _handler.Handle(new GetEnabledModulesQuery(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExpiredTerm_ReturnsEmpty()
    {
        var instant = DateTimeOffset.UtcNow;
        _clock.UtcNowValue = instant;
        var snapshot = new TenantEntitlementSnapshot(
            _tenantId,
            Guid.NewGuid(),
            SubscriptionTerm.Rehydrate(SubscriptionTermKind.Fixed, instant.AddDays(-10), instant.AddDays(-1)).Value,
            new HashSet<string> { "HR" },
            new Dictionary<string, long>(),
            []
        );

        _cache.SnapshotToReturn = snapshot;

        var result = await _handler.Handle(new GetEnabledModulesQuery(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ActiveTerm_ReturnsPlanModulesAndGrants()
    {
        var instant = DateTimeOffset.UtcNow;
        _clock.UtcNowValue = instant;
        var snapshot = new TenantEntitlementSnapshot(
            _tenantId,
            Guid.NewGuid(),
            SubscriptionTerm.Rehydrate(SubscriptionTermKind.Fixed, instant.AddDays(-10), instant.AddDays(10)).Value,
            new HashSet<string> { "HR", "GL" },
            new Dictionary<string, long>(),
            [
                new EntitlementGrantFact(EntitlementGrantKind.ModuleGrant, "Payroll", null, null, instant.AddDays(-1), null),
                new EntitlementGrantFact(EntitlementGrantKind.ModuleGrant, "FutureModule", null, null, instant.AddDays(1), null), // not in force yet
                new EntitlementGrantFact(EntitlementGrantKind.LimitRaise, "SomeLimit", "Seats", 100, instant.AddDays(-1), null) // not a module
            ]
        );

        _cache.SnapshotToReturn = snapshot;

        var result = await _handler.Handle(new GetEnabledModulesQuery(), CancellationToken.None);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo("HR", "GL", "Payroll");
    }

    [Fact]
    public async Task Handle_CacheMiss_ReadsAndCaches()
    {
        var instant = DateTimeOffset.UtcNow;
        _clock.UtcNowValue = instant;
        var snapshot = new TenantEntitlementSnapshot(
            _tenantId,
            Guid.NewGuid(),
            SubscriptionTerm.Rehydrate(SubscriptionTermKind.Fixed, instant.AddDays(-10), instant.AddDays(10)).Value,
            new HashSet<string> { "HR" },
            new Dictionary<string, long>(),
            []
        );

        _cache.SnapshotToReturn = null;
        _reader.SnapshotToReturn = snapshot;

        var result = await _handler.Handle(new GetEnabledModulesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo("HR");
        _cache.StoredSnapshots.Should().Contain(snapshot);
    }

    private sealed class StubCurrentTenant : ICurrentTenant
    {
        public Guid? TenantIdValue { get; set; }
        public Guid? TenantId => TenantIdValue;
        public string BaseCurrencyCode => "USD";
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }

    private sealed class StubTenantEntitlementReader : ITenantEntitlementReader
    {
        public TenantEntitlementSnapshot? SnapshotToReturn { get; set; }
        public Task<TenantEntitlementSnapshot> ReadAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(SnapshotToReturn!);
    }

    private sealed class StubTenantEntitlementCache : ITenantEntitlementCache
    {
        public TenantEntitlementSnapshot? SnapshotToReturn { get; set; }
        public List<TenantEntitlementSnapshot> StoredSnapshots { get; } = new();

        public bool TryGet(Guid tenantId, out TenantEntitlementSnapshot snapshot)
        {
            snapshot = SnapshotToReturn!;
            return snapshot != null;
        }

        public void Store(TenantEntitlementSnapshot snapshot) => StoredSnapshots.Add(snapshot);
        public void InvalidateTenant(Guid tenantId) { }
        public void InvalidatePlan(Guid planId) { }
    }

    private sealed class StubDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNowValue { get; set; }
        public DateTimeOffset UtcNow => UtcNowValue;
    }
}
