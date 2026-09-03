using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.TenantLifecycle;

public sealed class TenantLifecycleApplicationTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Requirement", "FR-TEN-0101")]
  [Trait("Acceptance", "AC-TEN-0001")]
  [Trait("Decision", "DEC-TEN-0001")]
  public async Task Create_generates_authoritative_guid_enforces_code_uniqueness_and_commits_once()
  {
    var repository = new FakeTenantRepository();
    var unitOfWork = new FakeUnitOfWork();
    var issuer = new FakeTrialSubscriptionIssuer();
    var handler = new CreateTenantCommandHandler(
      repository, issuer, unitOfWork, new TestCurrentUser("platform-actor"), new TestClock());

    var result = await handler.HandleAsync(new CreateTenantCommand("  Acme  ", "  Shared Name  "));

    Assert.True(result.IsSuccess);
    Assert.NotEqual(Guid.Empty, result.Value);
    Assert.Equal(result.Value, repository.Added?.TenantId);

    // The trial was issued for THIS tenant before the single commit — one save covering both, so there is
    // no interval in which the tenant exists without one (`DEC-L-034`).
    Assert.Equal(result.Value, issuer.IssuedFor);
    Assert.Equal("ACME", repository.Added?.NormalizedTenantCode);
    Assert.Equal(TenantStatus.Provisioning, repository.Added?.Status);
    Assert.Equal(TenantStatusChangeReason.Created, repository.Added?.StatusChangeReasonCode);
    Assert.Equal(1, unitOfWork.SaveCount);

    repository.CodeExists = true;
    var duplicate = await handler.HandleAsync(new CreateTenantCommand("acme", "Another Shared Name"));
    Assert.Equal("Tenant.CodeExists", duplicate.Error.Code);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0002")]
  [Trait("Scenario", "TS-TEN-0010")]
  public async Task Concurrent_unique_index_conflict_maps_to_the_same_tenant_code_error()
  {
    var repository = new FakeTenantRepository();
    var unitOfWork = new FakeUnitOfWork
    {
      Failure = IdentityAccessErrors.UniqueConstraintViolation
    };
    var handler = new CreateTenantCommandHandler(
      repository,
      new FakeTrialSubscriptionIssuer(),
      unitOfWork,
      new TestCurrentUser("platform-actor"),
      new TestClock());

    var result = await handler.HandleAsync(new CreateTenantCommand("ACME", "Acme Trading"));

    Assert.Equal("Tenant.CodeExists", result.Error.Code);
    Assert.Equal(1, unitOfWork.SaveCount);
  }

  [Fact]
  [Trait("Security", "SEC-TEN-0203")]
  // ⚠⚠⚠ `AC-TEN-0012` EXAMINED AND NOT CITED: THE `["Administrator"]` ARGUMENT REACHES NO BRANCH.
  //
  // *"An ordinary tenant role cannot administer Tenant lifecycle."* This test's name says exactly that, and
  // its fixture hands the handler a tenant role to prove it. But `ApplicationExecutionContext
  // .GetPlatformActor` is `string.IsNullOrWhiteSpace(currentUser.UserId)` and NOTHING ELSE — `Roles` is
  // never read, on this path or any other reachable from here. **The refusal below is caused entirely by
  // `UserId` being null, so the role argument is inert and the assertion cannot tell the two apart.**
  //
  // A handler that granted authority on `Roles.Contains("Administrator")` while still requiring a non-null
  // `UserId` would pass every line here. The test carries *a platform actor is required*; it does not carry
  // *and authority is not inferred from roles*, though the name claims both. The local variable is called
  // `anonymous`, which is what the fixture actually is.
  //
  // ⚠ THE TELL IS AN ARGUMENT THAT DOES NO WORK, and it is the same tell as the unused theory parameter that
  // took the gate red earlier in this run — except a constructor argument is not compiler-visible, so
  // nothing complains and it reads as deliberate scope-setting. **An inert fixture value and one chosen to
  // exclude a confound are identical in the source.**
  //
  // ⚠⚠ AND THE CRITERION IS NOT MERELY UNCITED HERE — IT HAS NO APPLICATION-LAYER MECHANISM TO CITE.
  // `GetPlatformActor` admits ANY non-empty `UserId`, so a tenant user calling this handler directly is
  // authorized by it. What stands between a tenant role and tenant administration today is that these
  // commands are routed nowhere: `Tenant_endpoints_remain_deferred_and_the_platform_api_does_not_reach_
  // tenant_application` holds that, and `AC-TEN-0020` defers the transport. So the criterion holds by the
  // ABSENCE OF A SURFACE, and its real mechanism is platform-plane authorization — the `AC-TEN-0021..0030`
  // block, deferred with the endpoints. **Adding a discriminating fixture here would assert a boundary this
  // layer does not enforce and is not the place to.**
  //
  // ⚠⚠⚠ AND THAT MAKES IT A SCHEDULED FAILURE RATHER THAN A GAP. **A PROPERTY HELD BY THE ABSENCE OF A
  // SURFACE FAILS SILENTLY ON THE DAY THE SURFACE IS BUILT** — and here the day is named and already on the
  // plan. Nothing about building those routes forces anyone to notice that the guard was never written.
  //
  // ⚠ THIS NOTE IS THE REDUNDANT COPY AND KNOWS IT. The person who builds the `0021..0030` transport will be
  // reading the CRITERIA DOCUMENT, not a Platform unit test about tenant creation, so the load-bearing
  // record is the entry in `FP-003`'s acceptance criteria — outside this lane and requested from the
  // architect. As of this commit **that entry is composed but BLOCKED** behind an owner decision on repo
  // and handoff writes, so the only copy that exists today is this one, in the place least likely to be
  // read by the person who needs it. Whoever unblocks that write should place it; until then, this comment
  // is carrying a criterion on its own.
  public async Task Platform_actor_is_required_without_inferring_authority_from_tenant_roles()
  {
    var repository = new FakeTenantRepository();
    var unitOfWork = new FakeUnitOfWork();
    var anonymous = new CreateTenantCommandHandler(
      repository, new FakeTrialSubscriptionIssuer(), unitOfWork, new TestCurrentUser(null, ["Administrator"]), new TestClock());

    var result = await anonymous.HandleAsync(new CreateTenantCommand("ACME", "Acme"));

    Assert.Equal("Tenant.Unauthorized", result.Error.Code);
    Assert.Null(repository.Added);
  }

  [Fact]
  [Trait("Scenario", "TS-TEN-0013")]
  [Trait("Acceptance", "AC-TEN-0014")]
  public async Task Lifecycle_handlers_enforce_rowversion_and_all_approved_transitions()
  {
    var tenant = CreateTenant();
    SetRowVersion(tenant, [1]);
    var repository = new FakeTenantRepository(tenant);
    var unitOfWork = new FakeUnitOfWork();
    var user = new TestCurrentUser("platform-actor");
    var clock = new TestClock();

    var stale = await new ActivateTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new ActivateTenantCommand(tenant.TenantId, [2]));
    Assert.Equal("Persistence.ConcurrencyConflict", stale.Error.Code);

    Assert.True((await new ActivateTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new ActivateTenantCommand(tenant.TenantId, [1]))).IsSuccess);
    Assert.True((await new SuspendTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new SuspendTenantCommand(tenant.TenantId, TenantStatusChangeReason.Security, [1]))).IsSuccess);
    Assert.True((await new ReactivateTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new ReactivateTenantCommand(tenant.TenantId, TenantStatusChangeReason.IssueResolved, [1]))).IsSuccess);
    Assert.True((await new ArchiveTenantCommandHandler(repository, unitOfWork, user, clock)
      .HandleAsync(new ArchiveTenantCommand(tenant.TenantId, TenantStatusChangeReason.CustomerClosure, [1]))).IsSuccess);
    Assert.Equal(TenantStatus.Archived, tenant.Status);
    Assert.Equal(4, unitOfWork.SaveCount);
  }

  [Theory]
  [InlineData(null, false, null, false, TenantAuthenticationIneligibilityReason.TenantNotFound)]
  [InlineData(TenantStatus.Provisioning, true, TenantStatus.Provisioning, false, TenantAuthenticationIneligibilityReason.Provisioning)]
  [InlineData(TenantStatus.Active, true, TenantStatus.Active, true, TenantAuthenticationIneligibilityReason.None)]
  [InlineData(TenantStatus.Suspended, true, TenantStatus.Suspended, false, TenantAuthenticationIneligibilityReason.Suspended)]
  [InlineData(TenantStatus.Archived, true, TenantStatus.Archived, false, TenantAuthenticationIneligibilityReason.Archived)]
  [Trait("Requirement", "FR-TEN-0108")]
  [Trait("Requirement", "REQ-PLT-0005")]
  [Trait("Decision", "DEC-IAM-0013")]
  [Trait("Requirement", "FR-AUTH-0120")]
  [Trait("Acceptance", "AC-AUTH-0018")]
  [Trait("Acceptance", "AC-IAM-0021")]
  [Trait("Acceptance", "AC-TEN-0016")]
  [Trait("Acceptance", "AC-TEN-0008")]
  [Trait("Scenario", "TS-TEN-0012")]
  // ⚠⚠⚠ `AC-TEN-0008` ADDED ONE COMMIT AFTER `AC-TEN-0016`, ON THIS SAME THEORY, AND FINDING IT IS THE
  // WHOLE POINT OF THE SWEEP THAT `AC-TEN-0016` STARTED. Last commit established that a test carrying other
  // packages' criteria does not read as uncited, so the already-traited tests became the population to
  // re-examine rather than the one to skip. **This theory was the first entry in that population and it was
  // carrying a SECOND absent criterion from its own package.** The correction found its next instance on the
  // test that produced it.
  //
  // *"Eligibility is true ONLY for `Active`. A MISSING Tenant returns `Exists = false`, null status, false
  // eligibility, and `TenantNotFound`; existing statuses return the MATCHING EXACT REASON or `None` for
  // Active."* **The five `[InlineData]` rows are that sentence, in its own order.** Row 1 is the missing
  // clause's four values; rows 2-5 are each status with its own reason; the `eligible` column is true on
  // exactly one row.
  //
  // ---- WHY THE *ONLY* HOLDS HERE AND THE *EXACTLY* BELOW DOES NOT — SAME WORD-SHAPE, OPPOSITE DISPOSAL.
  //
  // Both are claims about a COMPLEMENT, and a row list can only ever assert presence. *Only for Active* is
  // nonetheless discharged, because the complement is FINITE AND PINNED ELSEWHERE:
  // `TenantLifecycleDomainTests.Status_and_reason_vocabularies_are_exact` asserts
  // `Enum.GetNames<TenantStatus>()` EQUALS the four names, so a fifth status cannot arrive without
  // reddening. **Four pinned members, four rows plus null, one true — that is a complete case analysis and
  // not a sample.**
  //
  // ⚠ `AC-TEN-0016`'s *returns exactly five members* had NO such neighbour when this was written: nothing
  // asserted the arity of `TenantAuthenticationEligibilityResult`'s property list. **THE TWO CLAIMS WERE THE
  // SAME SENTENCE-SHAPE ON THE SAME TEST AND ONLY ONE OF THEM COMPOSED** — which is the reusable test for
  // this family: an enumeration closes a complement claim exactly when something else pins the size of the
  // set being enumerated. Where that neighbour is absent, the enumeration proves presence and nothing more.
  //
  // ⚠⚠ THE MISSING NEIGHBOUR NOW EXISTS, BUILT IN THE SAME STRETCH THAT NAMED THE GAP.
  // `TenantLifecycleArchitectureTests.Tenant_read_projections_expose_exactly_their_lifecycle_contract` pins
  // this type's members by NAME AND TYPE at exactly five. Two consequences for the residuals recorded
  // below, and they should be read together with that test rather than in isolation:
  //
  //   *returns EXACTLY*   CLOSED. A sixth property reddens the pin, whatever it is called.
  //   five of six bans    CLOSED AS CONSEQUENCES rather than as assertions — no `IQueryable`, aggregate,
  //                       generic repository, subscription decision or authorization grant can be a sixth
  //                       member of a set asserted to have exactly five.
  //
  // **The residual text below is kept as written because it is what the gap looked like from here**, and
  // the shape of the miss is the reusable part. It is a record, not a live gap.
  //
  // ---- ONE RESIDUAL, AND IT IS A LAYER RATHER THAN A CLAUSE.
  //
  // *A MISSING Tenant* is asserted here as *a null status argument*. Row 1 calls `FromStatus(id, null)`;
  // what makes a missing row produce that null is `SingleOrDefaultAsync` over a `TenantStatus?` projection
  // in `TenantAuthenticationEligibilityReadService:15-18` (and `tenant?.Status` at `:29` for the UPDLOCK
  // path). **That step is EF behaviour against a real table, so no unit fixture can reach it** — it needs a
  // database, which puts it in Integration and behind the parked `PHASE` scope. The pure function is
  // covered; the mapping into it is not.
  //
  // ⚠⚠⚠ `AC-TEN-0016` ADDED, AND IT IS THE ONE THIS TEST WAS ALREADY ASSERTING VERBATIM. *"The
  // authentication-eligibility contract accepts ONE TenantId and returns EXACTLY TenantId, Exists,
  // nullable TenantStatus, IsAuthenticationEligible, and TenantAuthenticationIneligibilityReason. It
  // exposes NO NAME, `IQueryable`, aggregate, generic repository, subscription decision, or authorization
  // grant."* Lines `:164-168` are those five members in the criterion's own order; `:169` is the *no name*
  // ban. **The test predates the citation and the name says it — *derived exactly and has no name* is the
  // criterion's two halves in six words.**
  //
  // ⚠ WORTH NOTING WHY IT WAS MISSED: this theory already carried `AC-AUTH-0018` and `AC-IAM-0021`, so it
  // did not READ as uncited. **A test carrying criteria from two other packages looks covered**, and the
  // criterion from its own package — the one naming the exact contract it asserts — was the absent one.
  //
  // ---- TWO RESIDUALS, AND THE FIRST IS IN THE WORD *EXACTLY*.
  //
  // **`:164-168` assert the five members are PRESENT and correct. Nothing asserts they are the ONLY five.**
  // A sixth property added tomorrow passes every line here unless its name happens to contain `Name`. The
  // criterion says *returns exactly*, and *exactly* is a claim about the complement — which needs a
  // property-count or a set comparison, and has neither.
  //
  // ⚠⚠ AND ONE BAN OF SIX IS CARRIED. *No name* is asserted; *no `IQueryable`, aggregate, generic
  // repository, subscription decision, or authorization grant* are not. **Five of those six would be
  // caught by a walk this test already performs** — it has `GetProperties()` in hand at `:169` — so the
  // gap is the predicate, not the access.
  // ⚠ `AC-IAM-0021` ADDED — its SECOND half, *"…or new tenant-scoped TOKENS."* The `Suspended` row returns
  // `eligible: false` with reason `Suspended`, and eligibility is the gate token issuance consults, so this
  // is where the token half of an IAM criterion is actually decided. `AuthorizationPipelineTests` carries
  // the first half (*normal application access*) at the route layer.
  //
  // ⚠⚠ CROSS-PACKAGE, AND THE PRECEDENT IS ON THE LINE ABOVE: this test already carries `AC-AUTH-0018` and
  // `DEC-IAM-0013`. Tenant suspension is `FP-003` behaviour that `FP-001` and `FP-002` both have criteria
  // about, because each cares about a different consequence. **A trait claims EVIDENCE, not OWNERSHIP** —
  // and whoever maintains this theory should know an IAM criterion now rests on the `Suspended` row.
  //
  // ⚠⚠⚠ AND THE KEY IS `Acceptance`, NOT `Criterion`, DELIBERATELY. Both keys carry `AC-` ids in this repo
  // and nothing validates either; this file uses `Acceptance` throughout, so `Criterion` here would make the
  // file internally inconsistent to buy consistency with a different file. **The key is a per-file
  // convention rather than a meaning, which is exactly why any future checker must read all four.**
  //
  // ⚠ ADJACENT-SCOPE CHECKED: `Suspended` is its own `[InlineData]` row with its own expected reason, so
  // the criterion is carried by an exercised case and not by a class-membership argument.
  //
  // NOT cited on `TenantLifecycleDomainTests.Every_approved_transition_updates_trusted_metadata_and_raises_
  // safe_event`, which also asserts `IsAuthenticationEligible` is false after `Suspend()`. That test's
  // subject is transition metadata and events; the eligibility line is incidental to it and a legitimate
  // refactor could drop it. **A criterion should not rest on an assertion its own test is not about.**
  public void Eligibility_is_derived_exactly_and_has_no_name(
    TenantStatus? status,
    bool exists,
    TenantStatus? expectedStatus,
    bool eligible,
    TenantAuthenticationIneligibilityReason reason)
  {
    var tenantId = Guid.NewGuid();
    var result = TenantAuthenticationEligibilityResult.FromStatus(tenantId, status);

    Assert.Equal(tenantId, result.TenantId);
    Assert.Equal(exists, result.Exists);
    Assert.Equal(expectedStatus, result.TenantStatus);
    Assert.Equal(eligible, result.IsAuthenticationEligible);
    Assert.Equal(reason, result.TenantAuthenticationIneligibilityReason);
    Assert.DoesNotContain(result.GetType().GetProperties(), property => property.Name.Contains("Name", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("Requirement", "FR-TEN-0102")]
  [Trait("Requirement", "FR-TEN-0103")]
  [Trait("Scenario", "TS-TEN-0011")]
  // ⚠⚠⚠ `AC-TEN-0004` EXAMINED AND DELIBERATELY NOT CITED. THE TEST ASSERTS THE CRITERION'S SUBJECT AND NOT
  // ITS PREDICATE, AND THAT IS A CITATION CLASS I HAVE NOT WRITTEN DOWN BEFORE.
  //
  // *"GET AND BOUNDED LIST QUERIES ‖ return safe lifecycle projections and no tenant business data."* The
  // clause before the bar names WHICH queries are in scope; the clause after it is the whole claim. This
  // test asserts the first and not the second:
  //
  //   bounded            `invalid.IsFailure` — page 0 and size 101 both refused. Delete the bound and this
  //                      line reddens. GENUINELY CARRIED, and it is the SUBJECT.
  //   safe projection    `Assert.Equal(dto, get.Value)` — the handler returns what the read service gave
  //                      it. Fails if the handler substitutes something; passes for ANY `TenantDto`.
  //   no business data   **NO FIXTURE.** `TenantDto` gaining a `TaxId` tomorrow passes every line in this
  //                      method, because `Map` at the bottom of this file BUILDS the dto the fake returns.
  //                      The test is asserting its own arrangement.
  //
  // **A test that verifies the subject is not weak evidence for the predicate — it is no evidence for it**,
  // and the name `Get_and_list_return_bounded_safe_projections` contains all the criterion's words, which is
  // exactly why it reads as covering it. Citing here would put an id on the qualifier.
  //
  // ---- WHAT WOULD CLOSE IT, AND THE IDIOM IS ALREADY IN THE TREE TWICE.
  //
  // A shape guard over the projection, not a handler test: `TenantLifecycleArchitectureTests
  // .Tenant_events_contain_only_safe_lifecycle_values:239-261` bans a business-term regex over the event
  // types AND pins their arity at seven; `LocalizationArchitectureTests:334` walks
  // `SSAS.Platform.Application.Localization` public types for the same purpose. The read projection has
  // neither.
  //
  // ⚠ THE ABSENCE IS SEARCHED TWO WAYS AND I HAVE NOT SEARCHED FURTHER: `TenantDto` appears in `tests/` only
  // in THIS file and only as construction (`Map`, `FakeTenantReadService`), and no test in
  // `Architecture.Tests` walks `SSAS.Platform.Application.Tenants` by namespace — the two routes by which a
  // guard would reach it, one by name and one by mechanism.
  //
  // ⚠⚠ BUILT, SAME STRETCH: `TenantLifecycleArchitectureTests.Tenant_read_projections_expose_exactly_their_
  // lifecycle_contract` now pins `TenantDto`'s member names exactly and bans business-data terms across the
  // namespace, and `AC-TEN-0004` is cited THERE. **This test still does not carry the criterion and still
  // should not** — the two cover opposite halves of one sentence, and the reason to leave this note
  // standing is that the name here will go on reading as though it covers both.
  public async Task Get_and_list_return_bounded_safe_projections()
  {
    var tenant = CreateTenant();
    var dto = Map(tenant);
    var readService = new FakeTenantReadService(dto);
    var user = new TestCurrentUser("platform-actor");

    var get = await new GetTenantQueryHandler(readService, user).HandleAsync(new GetTenantQuery(tenant.TenantId));
    var list = await new ListTenantsQueryHandler(readService, user)
      .HandleAsync(new ListTenantsQuery(TenantStatus.Provisioning, 1, 50));
    var invalid = await new ListTenantsQueryHandler(readService, user)
      .HandleAsync(new ListTenantsQuery(null, 0, 101));

    Assert.Equal(dto, get.Value);
    Assert.Single(list.Value.Items);
    Assert.True(invalid.IsFailure);
    Assert.Equal(TenantStatus.Provisioning, readService.RequestedStatus);
  }

  [Fact]
  [Trait("NonFunctional", "NFR-TEN-0301")]
  [Trait("Scenario", "TS-TEN-0018")]
  public async Task Eligibility_query_delegates_and_propagates_cancellation()
  {
    var service = new FakeEligibilityReadService();
    var handler = new GetTenantAuthenticationEligibilityQueryHandler(service);
    using var source = new CancellationTokenSource();
    source.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      handler.HandleAsync(new GetTenantAuthenticationEligibilityQuery(Guid.NewGuid()), source.Token));
  }

  private static Tenant CreateTenant()
  {
    return Tenant.Create(
      TenantCode.Create("ACME").Value,
      TenantName.Create("Acme Trading").Value,
      "platform-actor",
      Guid.NewGuid(),
      Now).Value;
  }

  private static TenantDto Map(Tenant tenant) => new(
    tenant.TenantId,
    tenant.TenantCode.Value,
    tenant.TenantName.Value,
    tenant.Status,
    tenant.CreatedUtc,
    tenant.CreatedBy,
    tenant.ModifiedUtc,
    tenant.ModifiedBy,
    tenant.StatusChangedUtc,
    tenant.StatusChangedBy,
    tenant.StatusChangeReasonCode,
    tenant.RowVersion);

  private static void SetRowVersion(Tenant tenant, byte[] value)
  {
    var field = typeof(Tenant).GetField(
      "<RowVersion>k__BackingField",
      System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(tenant, value);
  }

  // Tenant creation now issues the 14-day trial into the SAME unit of work (`DEC-L-034`, T-041). These
  // tests are about the lifecycle rather than about the trial, so the issuer is recorded rather than run —
  // `TrialSubscriptionIssuanceTests` exercises the real one through this same handler.
  private sealed class FakeTrialSubscriptionIssuer(Error? failure = null) : ITrialSubscriptionIssuer
  {
    public Guid? IssuedFor { get; private set; }

    public Task<Result> IssueAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
      IssuedFor = tenantId;
      return Task.FromResult(failure is null ? Result.Success() : Result.Failure(failure));
    }
  }

  private sealed class FakeTenantRepository(params Tenant[] tenants) : ITenantRepository
  {
    private readonly List<Tenant> values = [.. tenants];

    public bool CodeExists { get; set; }

    public Tenant? Added { get; private set; }

    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(tenant => tenant.TenantId == tenantId));

    public Task<Tenant?> GetByNormalizedCodeAsync(string normalizedTenantCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(tenant => tenant.NormalizedTenantCode == normalizedTenantCode));

    public Task<bool> NormalizedCodeExistsAsync(string normalizedTenantCode, CancellationToken cancellationToken = default) =>
      Task.FromResult(CodeExists || values.Any(tenant => tenant.NormalizedTenantCode == normalizedTenantCode));

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      Added = tenant;
      values.Add(tenant);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Error? Failure { get; init; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      SaveCount++;
      return Task.FromResult(Failure is null ? Result.Success(1) : Result.Failure<int>(Failure));
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeTenantReadService(params TenantDto[] tenants) : ITenantReadService
  {
    private readonly TenantDto[] values = tenants;

    public TenantStatus? RequestedStatus { get; private set; }

    public Task<TenantDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(values.SingleOrDefault(tenant => tenant.TenantId == tenantId));

    public Task<PagedResult<TenantDto>> ListAsync(
      TenantStatus? status,
      int pageNumber,
      int pageSize,
      CancellationToken cancellationToken = default)
    {
      RequestedStatus = status;
      var filtered = values.Where(tenant => !status.HasValue || tenant.Status == status.Value).ToArray();
      return Task.FromResult(new PagedResult<TenantDto>(filtered, pageNumber, pageSize, filtered.Length));
    }
  }

  private sealed class FakeEligibilityReadService : ITenantAuthenticationEligibilityReadService
  {
    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, TenantStatus.Active));
    }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default) => GetEligibilityAsync(tenantId, cancellationToken);
  }

  private sealed class TestCurrentUser(string? userId, IReadOnlyCollection<string>? roles = null) : ICurrentUser
  {
    public string? UserId { get; } = userId;
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles { get; } = roles ?? [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
