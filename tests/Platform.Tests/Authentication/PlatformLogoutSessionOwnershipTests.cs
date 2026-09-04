using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Tests.Authentication;

// ==================================================================================================
// A PLATFORM SESSION BELONGING TO SOMEBODY ELSE IS NOT REVOKED.
// ==================================================================================================
//
// ---- ⚠⚠⚠ WHY THIS EXISTED AS A CLAIM AND NOT AS A TEST.
//
// `AC-TEN-0082` requires that the logout target be resolved from the **validated `session_id` claim** and that
// *"a caller-supplied session/identity/principal id cannot select the target."* The handler enforces it in two
// moves — the ids are bound from the token by the transport layer, and then
// `session.IdentityId != command.IdentityId` makes a foreign session a silent no-op.
//
// **The transport half is structural. THE OWNERSHIP CHECK IS A LIVE, EXECUTABLE BRANCH, and nothing drove it.**
// `PlatformSupportAuthenticationLogoutPipelineTests` covers unauthenticated callers, wrong-plane tokens and a
// missing CSRF cookie — *all of which are rejected BEFORE the handler runs* — and the end-to-end test logs a
// caller out of their own session. ***NO TEST PRESENTED A VALID PLATFORM CALLER REACHING FOR SOMEONE ELSE'S
// SESSION,*** which is the one case the branch exists for.
//
// ⚠ AND ITS TENANT TWIN HAS HANDLER-LEVEL TESTS WHILE THIS ONE HAD NONE. `AuthenticationSessionApplicationTests
// .Current_session_logout_revokes_only_the_bound_session_and_is_terminally_idempotent` seeds a sibling session
// and proves only the bound one dies. *Measured asymmetry, one worked example, same shape.*
//
// ---- ⚠⚠ WHAT THE SECOND ASSERTION ADDS, AND WHY SUCCESS ALONE WOULD BE WORSE THAN NOTHING.
//
// The handler returns `Result.Success()` for a foreign session **deliberately** — a distinguishable failure
// would disclose that the session exists. ***SO "IT SUCCEEDED" IS ALSO WHAT A HANDLER THAT REVOKED THE FOREIGN
// SESSION WOULD RETURN.*** Asserting the status is still `Active` is the whole test; asserting only the result
// would pass on the exact defect this branch prevents.
public sealed class PlatformLogoutSessionOwnershipTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

  private const long OwnerIdentityId = 900;
  private const long OtherIdentityId = 901;

  [Fact]
  [Trait("Criterion", "AC-TEN-0082")]
  public async Task A_session_owned_by_another_identity_is_left_active_and_the_caller_learns_nothing()
  {
    var session = PlatformAuthenticationSession.Create(
      OwnerIdentityId,
      platformSupportPrincipalId: 1,
      clientId: "ssas-platform-web",
      tokenFamilyId: Guid.NewGuid(),
      securityVersionAtCreation: 1,
      createdUtc: Now,
      idleExpiresUtc: Now.AddHours(1),
      absoluteExpiresUtc: Now.AddDays(1));

    // THE PREMISE, so the assertion below is about the OWNERSHIP CHECK and not about an already-dead session.
    Assert.Equal(AuthenticationSessionStatus.Active, session.Status);

    var handler = new RevokeCurrentPlatformAuthenticationSessionCommandHandler(
      new FakeSessionRepository(session), new FakeUnitOfWork(), new TestClock());

    // The repository returns the owner's session; the command carries a DIFFERENT identity — exactly the state
    // a forged or replayed `session_id` would produce if the transport binding were ever bypassed.
    var result = await handler.HandleAsync(
      new RevokeCurrentPlatformAuthenticationSessionCommand(PlatformAuthenticationSessionId: 1, OtherIdentityId));

    // Success, because a distinguishable failure would disclose that the session exists...
    Assert.True(result.IsSuccess);

    // ...AND UNTOUCHED, which is the half the success value cannot carry.
    Assert.Equal(AuthenticationSessionStatus.Active, session.Status);
  }

  // ---- THE POSITIVE CONTROL. Without it the assertion above passes on a handler that revokes NOTHING, ever.
  //
  // ⚠ Same fakes, same session, and the ONLY difference is that the identity now matches. *That is what makes
  // the test above about OWNERSHIP rather than about a handler that has simply stopped working.*
  [Fact]
  [Trait("Criterion", "AC-TEN-0082")]
  public async Task The_owner_revoking_their_own_session_does_revoke_it()
  {
    var session = PlatformAuthenticationSession.Create(
      OwnerIdentityId,
      platformSupportPrincipalId: 1,
      clientId: "ssas-platform-web",
      tokenFamilyId: Guid.NewGuid(),
      securityVersionAtCreation: 1,
      createdUtc: Now,
      idleExpiresUtc: Now.AddHours(1),
      absoluteExpiresUtc: Now.AddDays(1));

    var handler = new RevokeCurrentPlatformAuthenticationSessionCommandHandler(
      new FakeSessionRepository(session), new FakeUnitOfWork(), new TestClock());

    var result = await handler.HandleAsync(
      new RevokeCurrentPlatformAuthenticationSessionCommand(PlatformAuthenticationSessionId: 1, OwnerIdentityId));

    Assert.True(result.IsSuccess);
    Assert.Equal(AuthenticationSessionStatus.Revoked, session.Status);
  }

  private sealed class FakeSessionRepository(PlatformAuthenticationSession session)
    : IPlatformAuthenticationSessionRepository
  {
    // Returns the seeded session for ANY id. The test is about the identity comparison the handler makes after
    // the load, so making the lookup id-sensitive would only move the refusal earlier and prove less.
    public Task<PlatformAuthenticationSession?> GetByIdForUpdateAsync(
      long platformAuthenticationSessionId, CancellationToken cancellationToken = default) =>
      Task.FromResult<PlatformAuthenticationSession?>(session);

    public Task<PlatformRefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(
      Guid refreshTokenPublicId, CancellationToken cancellationToken = default) =>
      Task.FromResult<PlatformRefreshTokenSessionLocator?>(null);

    public Task<PlatformAuthenticationSession?> GetByRefreshTokenForUpdateAsync(
      long platformAuthenticationSessionId, CancellationToken cancellationToken = default) =>
      Task.FromResult<PlatformAuthenticationSession?>(null);

    public Task<IReadOnlyList<PlatformAuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(
      long identityId, DateTimeOffset utcNow, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<PlatformAuthenticationSession>>([]);

    public Task<IReadOnlyList<PlatformAuthenticationSession>> ListActiveByPrincipalForUpdateAsync(
      long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<PlatformAuthenticationSession>>([]);

    public Task AddAsync(PlatformAuthenticationSession session, CancellationToken cancellationToken = default) =>
      Task.CompletedTask;
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success(1));

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<ITransaction>(new FakeTransaction());
  }

  private sealed class FakeTransaction : ITransaction
  {
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
