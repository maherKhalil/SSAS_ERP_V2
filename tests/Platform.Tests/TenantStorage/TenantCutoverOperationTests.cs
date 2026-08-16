using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Tests.TenantStorage;

// The durable cutover operation's lifecycle (ADR-020, TS-Storage Phase E1).
//
// The property under test throughout is that the FREEZE IS A RECORDED FACT rather than a runtime one, and
// that the transitions which must never be confused — established versus merely requested, released before
// the flip versus after it — are not reachable from one another.
public sealed class TenantCutoverOperationTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
  private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void A_new_operation_starts_preparing_and_refuses_no_writes()
  {
    var operation = Begin();

    Assert.Equal(TenantCutoverOperationStatus.Preparing, operation.Status);
    Assert.True(operation.IsActive);
    // WRITES ARE NORMAL UNTIL THE DRAIN COMPLETES. Creating the record is not the freeze.
    Assert.False(operation.RefusesApplicationWrites);
    Assert.Null(operation.FrozenUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Freezing_refuses_application_writes_and_records_when()
  {
    var operation = Begin();
    Assert.True(operation.RequestFreeze("test", Now).IsSuccess);

    Assert.True(operation.Freeze("test", Now.AddSeconds(2)).IsSuccess);

    Assert.Equal(TenantCutoverOperationStatus.Frozen, operation.Status);
    Assert.True(operation.RefusesApplicationWrites);
    Assert.Equal(Now.AddSeconds(2), operation.FrozenUtc);
    // Requested and established stay distinguishable, so a crash mid-drain is readable afterwards.
    Assert.Equal(Now, operation.FreezeRequestedUtc);
  }

  // A resumed cutover that finds its freeze already durable has nothing to do.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Freezing_twice_is_idempotent()
  {
    var operation = Frozen();

    Assert.True(operation.Freeze("test", Now.AddMinutes(5)).IsSuccess);

    Assert.Equal(TenantCutoverOperationStatus.Frozen, operation.Status);
    Assert.Equal(Now, operation.FrozenUtc);
  }

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Releasing_restores_writes_and_is_idempotent()
  {
    var operation = Frozen();

    Assert.True(operation.ReleaseFreeze("copy failed", "test", Now.AddMinutes(1)).IsSuccess);
    Assert.Equal(TenantCutoverOperationStatus.Abandoned, operation.Status);
    Assert.False(operation.RefusesApplicationWrites);
    Assert.False(operation.IsActive);
    Assert.Equal("copy failed", operation.FailureSummary);

    // A SECOND RELEASE MUST NOT CORRUPT ANYTHING. Release is the one step that has to be as reliable as
    // the copy, so it is safe to call from every failure path including one that already ran it.
    Assert.True(operation.ReleaseFreeze(null, "test", Now.AddMinutes(2)).IsSuccess);
    Assert.Equal(TenantCutoverOperationStatus.Abandoned, operation.Status);
    Assert.Equal(Now.AddMinutes(1), operation.FreezeReleasedUtc);
    Assert.Equal("copy failed", operation.FailureSummary);
  }

  // A drain that could not complete is terminal and is NOT a freeze. A partial "Frozen" claim would make
  // every later step unsafe.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void A_failed_drain_never_claims_frozen()
  {
    var operation = Begin();
    Assert.True(operation.RequestFreeze("test", Now).IsSuccess);

    Assert.True(operation.FailFreeze("TenantStorage.CutoverFreezeTimedOut", "test", Now.AddSeconds(30))
      .IsSuccess);

    Assert.Equal(TenantCutoverOperationStatus.Abandoned, operation.Status);
    Assert.False(operation.RefusesApplicationWrites);
    Assert.Null(operation.FrozenUtc);
    Assert.Equal("TenantStorage.CutoverFreezeTimedOut", operation.FailureSummary);
  }

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Freezing_an_abandoned_operation_is_refused()
  {
    var operation = Frozen();
    Assert.True(operation.ReleaseFreeze(null, "test", Now.AddMinutes(1)).IsSuccess);

    var refrozen = operation.Freeze("test", Now.AddMinutes(2));

    Assert.True(refrozen.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverOperationNotPreparing.Code, refrozen.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void A_cutover_to_the_same_database_is_refused()
  {
    var begun = TenantCutoverOperation.Begin(Tenant, 10, 10, "test", Now);

    Assert.True(begun.IsFailure);
    Assert.Equal(TenantStorageErrors.CutoverTargetNotEligible.Code, begun.Error.Code);
  }

  [Theory]
  [InlineData(0L, 20L)]
  [InlineData(10L, 0L)]
  [Trait("Decision", "ADR-020")]
  public void A_cutover_without_both_endpoints_is_refused(long source, long target)
  {
    var begun = TenantCutoverOperation.Begin(Tenant, source, target, "test", Now);

    Assert.True(begun.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantDatabaseRequired.Code, begun.Error.Code);
  }

  [Fact]
  [Trait("Decision", "ADR-020")]
  public void A_cutover_without_a_tenant_is_refused()
  {
    var begun = TenantCutoverOperation.Begin(Guid.Empty, 10, 20, "test", Now);

    Assert.True(begun.IsFailure);
    Assert.Equal(TenantStorageErrors.TenantRequired.Code, begun.Error.Code);
  }

  private static TenantCutoverOperation Begin() =>
    TenantCutoverOperation.Begin(Tenant, 10, 20, "test", Now).Value;

  private static TenantCutoverOperation Frozen()
  {
    var operation = Begin();
    operation.RequestFreeze("test", Now);
    operation.Freeze("test", Now);
    return operation;
  }
}
