using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.PlatformSupport;

// Phase 2 platform-support authority (ADR-015 / DEC-TEN-0018): domain + application invariants.
public sealed class PlatformSupportAuthorityTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

  private static PermissionDefinition Permission(string name)
  {
    Assert.True(new PlatformPermissionCatalog().TryGet(name, out var definition));
    return definition;
  }

  // ---- Domain ----

  [Fact]
  public void Register_requires_a_valid_identity()
  {
    Assert.True(PlatformSupportPrincipal.Register(0).IsFailure);
    Assert.True(PlatformSupportPrincipal.Register(-1).IsFailure);
    Assert.True(PlatformSupportPrincipal.Register(42).IsSuccess);
  }

  [Fact]
  public void Principal_is_global_and_not_tenant_or_company_owned()
  {
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(PlatformSupportPrincipal).GetInterfaces());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), typeof(PlatformPermissionAssignment).GetInterfaces());
    Assert.Null(typeof(PlatformSupportPrincipal).GetProperty(nameof(SSAS.BuildingBlocks.Domain.ITenantOwnedEntity.TenantId)));
    Assert.Null(typeof(PlatformPermissionAssignment).GetProperty(nameof(SSAS.BuildingBlocks.Domain.ITenantOwnedEntity.TenantId)));
  }

  [Theory]
  [InlineData(PlatformPermissionNames.ViewTenants)]
  [InlineData(PlatformPermissionNames.ManageTenants)]
  [InlineData(PlatformPermissionNames.TenantLifecycle)]
  public void Known_platform_support_permission_can_be_granted(string name)
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    var result = principal.GrantPermission(Permission(name), "actor", Now);

    Assert.True(result.IsSuccess);
    Assert.Contains(principal.ActivePermissions, permission => permission.Value == name);
  }

  [Fact]
  public void Tenant_scoped_permission_cannot_be_granted()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    var result = principal.GrantPermission(Permission(PlatformPermissionNames.ViewCompanies), "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.TenantPermissionRejected, result.Error);
    Assert.Empty(principal.ActivePermissions);
  }

  [Fact]
  public void Duplicate_active_grant_is_a_conflict()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(principal.GrantPermission(Permission(PlatformPermissionNames.ManageTenants), "actor", Now).IsSuccess);

    var duplicate = principal.GrantPermission(Permission(PlatformPermissionNames.ManageTenants), "actor", Now.AddMinutes(1));

    Assert.True(duplicate.IsFailure);
    Assert.Equal(PlatformSupportErrors.DuplicatePermissionAssignment, duplicate.Error);
    Assert.Single(principal.ActivePermissions);
  }

  [Fact]
  public void Revoke_removes_authority_and_re_grant_is_allowed_and_keeps_history()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    var manage = Permission(PlatformPermissionNames.ManageTenants);
    Assert.True(principal.GrantPermission(manage, "actor", Now).IsSuccess);

    Assert.True(principal.RevokePermission(manage.Name, "actor", Now.AddMinutes(1)).IsSuccess);
    Assert.Empty(principal.ActivePermissions);

    Assert.True(principal.GrantPermission(manage, "actor", Now.AddMinutes(2)).IsSuccess);
    Assert.Single(principal.ActivePermissions);
    Assert.Equal(2, principal.PermissionAssignments.Count);
  }

  [Fact]
  public void Revoking_an_unassigned_permission_fails()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    var result = principal.RevokePermission(Permission(PlatformPermissionNames.ViewTenants).Name, "actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PermissionAssignmentNotFound, result.Error);
  }

  // ---- Lifecycle (ADR-016 / DEC-TEN-0020) ----

  [Fact]
  [Trait("Acceptance", "AC-TEN-0038")]
  // `AC-TEN-0038` — *"A registered `PlatformSupportPrincipal` STARTS `Active`; the ONLY transitions are
  // `Active -> Disabled` and `Disabled -> Active`."* Two claims, and this test carries the first.
  //
  // THE SECOND IS A COMPLEMENT CLAIM AND IT IS CLOSED BY FOUR TESTS TOGETHER, not by this one:
  //   `Disable_then_reenable_transitions_and_stamps_metadata_without_touching_assignments`  both legal moves
  //   `Disable_when_already_disabled_is_an_invalid_transition`                              Disabled -> Disabled
  //   `Reenable_when_already_active_is_an_invalid_transition`                               Active -> Active
  // **Two statuses give four ordered pairs; two are legal, two are refused, and all four are exercised** —
  // so *the ONLY transitions* is a complete case analysis rather than a sample. ⚠ It stays complete only
  // while `PlatformSupportPrincipalStatus` has two members; nothing here pins that, and the pin that would
  // (an `Enum.GetNames` assertion, as `Status_and_reason_vocabularies_are_exact` does for tenants) is
  // `PlatformSupportAuthorityArchitectureTests.Principal_status_enum_has_exactly_active_and_disabled`.
  //
  // ⚠⚠⚠ CORRECTED: I FIRST WROTE THAT NO SUCH PIN EXISTED AND ADDED ONE, AND THE PIN HAD BEEN THERE ALL
  // ALONG. The plant — a third enum member — reddened TWO tests, mine and the architecture test above, which
  // is how the duplication surfaced. **I asserted an absence without searching for it**, which is the rule
  // this repository has been enforcing all night, and I broke it while writing a note about completeness.
  // My duplicate is removed; the load-bearing warning now sits at the real pin, where a deleter reads it.
  //
  // ⚠ AND THE PLANT EARNED ITS KEEP IN A WAY I HAD NOT ANTICIPATED: **it did not only show that a control
  // discriminates, it showed that MY control was not the only one.** A plant that reddens two tests is
  // telling you the second one was already doing the job.
  public void Register_starts_active_with_no_status_transition_metadata()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    Assert.Equal(PlatformSupportPrincipalStatus.Active, principal.Status);
    Assert.Null(principal.StatusChangedUtc);
    Assert.Null(principal.StatusChangedBy);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0038")]
  [Trait("Acceptance", "AC-TEN-0041")]
  // `AC-TEN-0038`'s two LEGAL transitions, and `AC-TEN-0041`'s first clause — *"While `Disabled`, active
  // assignment rows REMAIN PERSISTED (not deleted, not revoked)"* — which is the `without_touching_
  // assignments` half of the name. The grant/revoke clauses are the two tests below, same trait.
  public void Disable_then_reenable_transitions_and_stamps_metadata_without_touching_assignments()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(principal.GrantPermission(Permission(PlatformPermissionNames.ManageTenants), "actor", Now).IsSuccess);

    Assert.True(principal.Disable("disabler", Now.AddMinutes(1)).IsSuccess);
    Assert.Equal(PlatformSupportPrincipalStatus.Disabled, principal.Status);
    Assert.Equal(Now.AddMinutes(1), principal.StatusChangedUtc);
    Assert.Equal("disabler", principal.StatusChangedBy);
    Assert.Single(principal.ActivePermissions);

    Assert.True(principal.Reenable("enabler", Now.AddMinutes(2)).IsSuccess);
    Assert.Equal(PlatformSupportPrincipalStatus.Active, principal.Status);
    Assert.Equal(Now.AddMinutes(2), principal.StatusChangedUtc);
    Assert.Equal("enabler", principal.StatusChangedBy);
    Assert.Single(principal.ActivePermissions);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0038")]
  public void Disable_when_already_disabled_is_an_invalid_transition()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(principal.Disable("actor", Now).IsSuccess);

    var again = principal.Disable("actor", Now.AddMinutes(1));

    Assert.True(again.IsFailure);
    Assert.Equal(PlatformSupportErrors.InvalidStatusTransition, again.Error);
    Assert.Equal(Now, principal.StatusChangedUtc);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0038")]
  public void Reenable_when_already_active_is_an_invalid_transition()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;

    var result = principal.Reenable("actor", Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.InvalidStatusTransition, result.Error);
    Assert.Null(principal.StatusChangedUtc);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0041")]
  // `AC-TEN-0041`'s SECOND clause — *"a grant is REJECTED"* while `Disabled`. Paired with the revoke test
  // below, which carries the third: **the criterion names three behaviours in the disabled state and they
  // point in different directions — rows persist, grants refuse, revokes proceed — so a test covering only
  // the refusals would satisfy two thirds of it and silently license deleting the rows.**
  public void Grant_is_rejected_while_disabled()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(principal.Disable("actor", Now).IsSuccess);

    var result = principal.GrantPermission(Permission(PlatformPermissionNames.ViewTenants), "actor", Now.AddMinutes(1));

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PrincipalDisabled, result.Error);
    Assert.Empty(principal.ActivePermissions);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0041")]
  // `AC-TEN-0041`'s THIRD clause — *"a revoke is ALLOWED"*. ⚠ This is the one that would be lost first in a
  // tidy-up: *disabled means no changes* is the intuitive rule and it is WRONG here by specification, so a
  // reviewer simplifying the disabled-state behaviour would break the criterion while making the code look
  // more consistent.
  public void Revoke_is_allowed_while_disabled()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    var manage = Permission(PlatformPermissionNames.ManageTenants);
    Assert.True(principal.GrantPermission(manage, "actor", Now).IsSuccess);
    Assert.True(principal.Disable("actor", Now.AddMinutes(1)).IsSuccess);

    Assert.True(principal.RevokePermission(manage.Name, "actor", Now.AddMinutes(2)).IsSuccess);

    Assert.Empty(principal.ActivePermissions);
    Assert.Equal(PlatformSupportPrincipalStatus.Disabled, principal.Status);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0042")]
  // ⚠ THE TRAIT ABOVE WAS MISSING FOR SEVERAL COMMITS WHILE THIS COMMENT CLAIMED THE CITATION. **A comment
  // naming a criterion with no trait beside it is exactly the mention-versus-trait defect this pass removed
  // from the coverage table** — a text census counts it as covered, a trait census counts it as uncited, and
  // both are wrong. I committed it after identifying it, which is the strongest argument for the trait being
  // the citation: **the prose looked complete and carried nothing.**
  //
  // `AC-TEN-0042`'s FIRST SENTENCE — *"Status mutations use the principal `RowVersion` (a STALE VERSION IS A
  // CONFLICT)."* The stale-version gate is the third of the three this test walks.
  //
  // ⚠ THE CRITERION'S SECOND SENTENCE HAS NO SITE IN ANY TEST: *"DOCUMENTATION STATES ACCURATELY that
  // disabling does not cryptographically invalidate an already-issued short-lived JWT; immediate cut-off is
  // via `SecurityVersion`/session revocation."* **Its subject is a DOCUMENT, not behaviour.**
  //
  // ⚠⚠ IT IS UNWITNESSABLE BY TESTS, WHICH IS NOT THE SAME AS UNWITNESSABLE IN PRINCIPLE, AND THE DIFFERENCE
  // DECIDES WHETHER ANYONE EVER CLOSES IT. The clause is perfectly checkable — read the documentation against
  // the code and confirm it says what the code does — but the method is a REVIEW, not a run. **"In principle"
  // licenses ignoring it forever; "by tests" hands it an owner and a method.** Recorded here as *witnessed by
  // documentation review, not by the suite*.
  //
  // ⚠ AND IT SHOULD NOT SIT IN A TEST-COVERAGE DENOMINATOR. A criterion the suite CANNOT close depresses that
  // ratio permanently and for the wrong reason, and a reader who cannot tell which gaps are closable learns
  // to discount the number instead of chasing it.
  public async Task Disable_handler_gates_on_actor_missing_principal_and_stale_version()
  {
    var missing = new DisablePlatformSupportPrincipalCommandHandler(
      new FakePrincipalRepository(), new FakePlatformSessionRepository(), new FakeUnitOfWork(), new StubCurrentUser("actor"), new StubClock());
    Assert.Equal(
      PlatformSupportErrors.PrincipalNotFound,
      (await missing.HandleAsync(new DisablePlatformSupportPrincipalCommand(1, [1]))).Error);

    var unauthorized = new DisablePlatformSupportPrincipalCommandHandler(
      new FakePrincipalRepository(PlatformSupportPrincipal.Register(7).Value), new FakePlatformSessionRepository(), new FakeUnitOfWork(), new StubCurrentUser(null), new StubClock());
    Assert.True((await unauthorized.HandleAsync(new DisablePlatformSupportPrincipalCommand(1, [1]))).IsFailure);

    // A fresh principal has an empty RowVersion, so any expected version is a concurrency conflict.
    var stale = new DisablePlatformSupportPrincipalCommandHandler(
      new FakePrincipalRepository(PlatformSupportPrincipal.Register(7).Value), new FakePlatformSessionRepository(), new FakeUnitOfWork(), new StubCurrentUser("actor"), new StubClock());
    Assert.Equal(
      IdentityAccessErrors.ConcurrencyConflict,
      (await stale.HandleAsync(new DisablePlatformSupportPrincipalCommand(1, [9]))).Error);
  }

  // ---- Defense-in-depth filter ----

  [Fact]
  public void Authority_filter_keeps_platform_support_and_drops_tenant_and_unknown()
  {
    var catalog = new PlatformPermissionCatalog();

    var filtered = PlatformSupportPermissionFilter.FilterToPlatformSupportScope(
      new[] { PlatformPermissionNames.ManageTenants, PlatformPermissionNames.ViewCompanies, "Platform.Unknown.Thing" },
      catalog);

    Assert.Equal(new[] { PlatformPermissionNames.ManageTenants }, filtered);
  }

  // ---- Application handlers ----

  [Fact]
  public async Task Register_handler_requires_trusted_platform_actor()
  {
    var handler = new RegisterPlatformSupportPrincipalCommandHandler(
      new FakePrincipalRepository(), new FakeUnitOfWork(), new StubCurrentUser(null));

    var result = await handler.HandleAsync(new RegisterPlatformSupportPrincipalCommand(9));

    Assert.True(result.IsFailure);
  }

  [Fact]
  public async Task Register_handler_rejects_a_duplicate_principal_for_the_identity()
  {
    var repository = new FakePrincipalRepository { ExistsForIdentity = true };
    var handler = new RegisterPlatformSupportPrincipalCommandHandler(
      repository, new FakeUnitOfWork(), new StubCurrentUser("actor"));

    var result = await handler.HandleAsync(new RegisterPlatformSupportPrincipalCommand(9));

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PrincipalAlreadyExists, result.Error);
    Assert.Null(repository.Added);
  }

  [Fact]
  public async Task Register_handler_persists_a_new_principal()
  {
    var repository = new FakePrincipalRepository();
    var unitOfWork = new FakeUnitOfWork();
    var handler = new RegisterPlatformSupportPrincipalCommandHandler(repository, unitOfWork, new StubCurrentUser("actor"));

    var result = await handler.HandleAsync(new RegisterPlatformSupportPrincipalCommand(9));

    Assert.True(result.IsSuccess);
    Assert.NotNull(repository.Added);
    Assert.Equal(9, repository.Added!.IdentityId);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Grant_handler_rejects_unknown_and_tenant_permissions_and_accepts_platform_support()
  {
    var principal = PlatformSupportPrincipal.Register(9).Value;
    var repository = new FakePrincipalRepository(principal);
    var unitOfWork = new FakeUnitOfWork();
    var handler = new GrantPlatformPermissionCommandHandler(
      repository, new PlatformPermissionCatalog(), unitOfWork, new StubCurrentUser("actor"), new StubClock());

    var unknown = await handler.HandleAsync(new GrantPlatformPermissionCommand(principal.Id, "Platform.Unknown.Thing"));
    var tenant = await handler.HandleAsync(new GrantPlatformPermissionCommand(principal.Id, PlatformPermissionNames.ViewCompanies));
    var valid = await handler.HandleAsync(new GrantPlatformPermissionCommand(principal.Id, PlatformPermissionNames.ManageTenants));

    Assert.Equal(PlatformSupportErrors.UnknownPermission, unknown.Error);
    Assert.Equal(PlatformSupportErrors.TenantPermissionRejected, tenant.Error);
    Assert.True(valid.IsSuccess);
    Assert.Contains(principal.ActivePermissions, permission => permission.Value == PlatformPermissionNames.ManageTenants);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  public async Task Grant_handler_reports_missing_principal()
  {
    var handler = new GrantPlatformPermissionCommandHandler(
      new FakePrincipalRepository(), new PlatformPermissionCatalog(), new FakeUnitOfWork(), new StubCurrentUser("actor"), new StubClock());

    var result = await handler.HandleAsync(new GrantPlatformPermissionCommand(123, PlatformPermissionNames.ManageTenants));

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PrincipalNotFound, result.Error);
  }

  [Fact]
  public async Task Revoke_handler_removes_an_existing_platform_permission()
  {
    var principal = PlatformSupportPrincipal.Register(9).Value;
    Assert.True(principal.GrantPermission(Permission(PlatformPermissionNames.ManageTenants), "actor", Now).IsSuccess);
    var repository = new FakePrincipalRepository(principal);
    var unitOfWork = new FakeUnitOfWork();
    var handler = new RevokePlatformPermissionCommandHandler(repository, unitOfWork, new StubCurrentUser("actor"), new StubClock());

    var result = await handler.HandleAsync(new RevokePlatformPermissionCommand(principal.Id, PlatformPermissionNames.ManageTenants));

    Assert.True(result.IsSuccess);
    Assert.Empty(principal.ActivePermissions);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  // ==================================================================================================
  // ⚠⚠⚠ WHAT THE DOUBLES BELOW CANNOT EXPRESS. READ THIS BEFORE ADDING A TEST OR A CITATION HERE.
  // ==================================================================================================
  //
  // **A TEST DOUBLE DOES NOT ONLY SIMPLIFY THE WORLD — IT BOUNDS THE VOCABULARY OF CLAIMS EVERY TEST IN THE
  // FILE CAN MAKE.** Enumerated up front so a clause is never cited here that nothing here could observe:
  //
  //   LOCK / SERIALIZATION SEMANTICS   `GetByIdForUpdateAsync` returns exactly what the unlocked read
  //                                    returns. Any clause about ordering, contention or for-update
  //                                    behaviour is INEXPRESSIBLE here. The double says so itself, below.
  //
  //   PROACTIVE SESSION REVOCATION     `FakePlatformSessionRepository.ListActiveByPrincipalForUpdateAsync`
  //                                    returns EMPTY, always. **A Disable that revoked nothing and a Disable
  //                                    that revoked correctly are indistinguishable in this file**, because
  //                                    the handler is never handed a session to revoke. So `AC-TEN-0040`'s
  //                                    *"its platform session is revoked"* CANNOT be cited here — its
  //                                    witness is `PlatformAuthenticationSessionFlowSqlServerTests
  //                                    .Disable_revokes_all_active_platform_sessions_of_the_principal_only`,
  //                                    which needs a database and sits in the parked PHASE scope.
  //
  //   ANY SESSION LOOKUP OR CREATION   every other member of that double throws `NotSupportedException`.
  //
  //   HOW MANY PRINCIPALS WERE ADDED   `Added` is a single slot overwritten by each `AddAsync`, the same
  //                                    shape that made `AC-TEN-0048`'s *exactly one* unassertable in
  //                                    `PlatformSupportBootstrapTests` until it was repaired. It is not
  //                                    load-bearing here — one principal is registered per test — but a
  //                                    cardinality claim must not be cited against it without fixing it
  //                                    first.
  //
  // ⚠ NONE OF THESE IS A DEFECT IN THE DOUBLES. They are correct choices for what this file tests, and the
  // list exists so the NEXT reader knows which criteria to take elsewhere rather than discovering it after
  // writing a citation that cannot fail.
  private sealed class FakePrincipalRepository(PlatformSupportPrincipal? principal = null) : IPlatformSupportPrincipalRepository
  {
    public bool ExistsForIdentity { get; init; }
    public PlatformSupportPrincipal? Added { get; private set; }

    public Task<PlatformSupportPrincipal?> GetByIdAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      Task.FromResult(principal);

    // In-memory fake: same result as the unlocked read. Real lock serialization is proven only by the SQL Server
    // concurrency tests (PlatformAuthenticationSessionFlowSqlServerTests), never by this fake.
    //
    // ⚠ THIS COMMENT IS THE PATTERN THE OTHER TWO DEFECTIVE DOUBLES IN THIS TREE LACKED — it DECLARES its own
    // inexpressibility instead of leaving a reader to infer it. `AuthenticationSessionApplicationTests`'
    // membership fake derived tenant eligibility from membership, and `PlatformSupportBootstrapTests`' one-slot
    // recorder discarded cardinality; neither said so, and both silently bounded what any test in their file
    // could claim.
    public Task<PlatformSupportPrincipal?> GetByIdForUpdateAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      Task.FromResult(principal);

    public Task<PlatformSupportPrincipal?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(principal);

    public Task<PlatformSupportPrincipal?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(principal);

    public Task<bool> ExistsForIdentityAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(ExistsForIdentity);

    public Task AddAsync(PlatformSupportPrincipal principal, CancellationToken cancellationToken = default)
    {
      Added = principal;
      return Task.CompletedTask;
    }
  }

  private sealed class FakePlatformSessionRepository : IPlatformAuthenticationSessionRepository
  {
    public Task<PlatformRefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(Guid refreshTokenPublicId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession?> GetByRefreshTokenForUpdateAsync(long platformAuthenticationSessionId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession?> GetByIdForUpdateAsync(long platformAuthenticationSessionId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<IReadOnlyList<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(long identityId, DateTimeOffset utcNow, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    // The Disable-handler tests all fail before reaching proactive revocation, so no active sessions are needed.
    public Task<IReadOnlyList<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession>> ListActiveByPrincipalForUpdateAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession>>([]);

    public Task AddAsync(SSAS.Platform.Domain.PlatformSupport.PlatformAuthenticationSession session, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }

    // Disable now opens its transaction before the lock-protected principal read (global principal → session
    // order), so even the early gate paths pass through here.
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<ITransaction>(new FakeTransaction());
  }

  private sealed class FakeTransaction : ITransaction
  {
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  private sealed class StubCurrentUser(string? userId) : ICurrentUser
  {
    public string? UserId { get; } = userId;
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class StubClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
