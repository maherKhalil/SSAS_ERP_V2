using System.Reflection;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Identity;

namespace SSAS.Platform.Tests.Authentication;

[Trait("Scenario", "TS-AUTH-0020")]
[Trait("Scenario", "TS-AUTH-0021")]
[Trait("Scenario", "TS-AUTH-0024")]
[Trait("Scenario", "TS-AUTH-0031")]
[Trait("Scenario", "TS-AUTH-0032")]
[Trait("Scenario", "TS-AUTH-0038")]
[Trait("Scenario", "TS-AUTH-0042")]
[Trait("Scenario", "TS-AUTH-0075")]
[Trait("Scenario", "TS-AUTH-0076")]
[Trait("Scenario", "TS-AUTH-0083")]
[Trait("Scenario", "TS-AUTH-0088")]
[Trait("Scenario", "TS-AUTH-0089")]
// ---- ⚠ `AC-AUTH-0024` AND `AC-AUTH-0032` MOVED DOWN TO THE METHODS THAT CARRY THEM.
//
// They were the only two criteria in this file cited at CLASS scope, while seven others (`AC-AUTH-0002`,
// `0003`, `0007`, `0008`, `0010`, `0023`, `0028`) already sat on methods with their prose quoted beside
// them. **A class-scoped trait claimed both criteria of all fourteen methods here** — including the client-id
// syntax theory and three suspended-tenant cases that bear on neither.
public sealed class AuthenticationSessionApplicationTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
  private static readonly AuthenticationClientId Client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
  private static readonly string[] VerifiedIdentityPropertyNames = ["IdentityId", "SecurityVersion"];

  // `AC-AUTH-0024`'s FIRST CLAUSE — *"Successful credential verification yields only a
  // NON-USER-CONSTRUCTIBLE `VerifiedIdentity` capability"*. `Assert.Empty(publicConstructors)` is the clause
  // itself; the exact property list keeps *narrow* honest, and being compared against a **non-empty** expected
  // array it is anti-vacuous by construction — a reflection walk returning nothing fails it.
  //
  // The second clause, *"and session creation revalidates its `SecurityVersion`"*, is carried by
  // `Stale_verified_identity_is_rejected_before_tenant_state_is_created`, tagged there. **Two clauses, two
  // methods, both tagged** — which is what the class-scoped trait this replaces could not say.
  [Fact]
  [Trait("Acceptance", "AC-AUTH-0024")]
  public void Verified_identity_is_a_narrow_internal_capability()
  {
    var publicConstructors = typeof(VerifiedIdentity).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
    var properties = typeof(VerifiedIdentity).GetProperties().Select(property => property.Name).Order().ToArray();

    Assert.Empty(publicConstructors);
    Assert.Equal(VerifiedIdentityPropertyNames, properties);
    Assert.DoesNotContain("17", new VerifiedIdentity(17, 3).ToString(), StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(null, false)]
  [InlineData("", false)]
  [InlineData(" ssas-erp-web", false)]
  [InlineData("ssas-erp-web", true)]
  public void Client_id_syntax_rejects_blank_or_whitespace_padded_values(string? value, bool expected)
  {
    Assert.Equal(expected, AuthenticationClientId.Create(value).IsSuccess);
  }

  [Fact]
  public void V1_client_allowlist_is_ordinal_and_maximum_length_is_enforced()
  {
    var registry = new AllowedClientRegistry();

    Assert.True(registry.IsAllowed(AuthenticationClientId.Create("ssas-erp-web").Value));
    Assert.False(registry.IsAllowed(AuthenticationClientId.Create("SSAS-ERP-WEB").Value));
    Assert.False(registry.IsAllowed(AuthenticationClientId.Create("unknown-client").Value));
    Assert.True(AuthenticationClientId.Create(new string('a', AuthenticationClientId.MaximumLength + 1)).IsFailure);
  }

  [Fact]
  public async Task Begin_tenant_access_returns_no_membership_without_creating_authentication_state()
  {
    var fixture = new Fixture();

    var result = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));

    Assert.True(result.IsSuccess);
    Assert.IsType<NoEligibleMembership>(result.Value);
    Assert.Empty(fixture.Sessions.Values);
    Assert.Empty(fixture.Selections.Values);
  }

  [Fact]
  // ⚠ CITED BY ITEM 215. `AC-AUTH-0002` -- *"one active membership is selected automatically"* -- was one of
  // item 208's 32 uncited criteria, found by spot-check and confirmed here against the BODY: one eligible
  // membership in, `TenantSelectedAutomatically` out, and the tenant matches.
  [Trait("Acceptance", "AC-AUTH-0002")]
  // ⚠ ALSO CITES `AC-IAM-0006` — *"When an identity has EXACTLY ONE active tenant membership, selection is
  // completed automatically and no selection UI is required."* Same behaviour, two packages' criteria:
  // `FP-002` cares that a session is created, `FP-001` cares that the user is not asked. **`FP-001` is not
  // named anywhere in this file's subject, which is why the trait is worth more than the comment.**
  //
  // ⚠⚠ AND IT IS THE THREE-WAY POPULATION THAT MAKES THIS MEAN *automatically* RATHER THAN *always*:
  //
  //   ZERO memberships   `Begin_tenant_access_returns_no_membership_without_creating_authentication_state`
  //   ONE  membership    here — `TenantSelectedAutomatically`, and a session EXISTS
  //   MANY memberships   `Begin_tenant_access_creates_single_use_selection_proof_for_multiple_memberships`
  //                      — `TenantSelectionRequired`, and `Sessions` is EMPTY
  //
  // **A handler that auto-selected the first of many would satisfy this test alone.** The *many* row is what
  // makes *exactly one* a condition rather than a description of the fixture.
  //
  // Key is `Acceptance` to match this file, not `Criterion`: both carry `AC-` ids repo-wide and nothing
  // validates either, so local consistency is the only thing a reader can rely on.
  [Trait("Acceptance", "AC-IAM-0006")]
  public async Task Begin_tenant_access_automatically_selects_one_revalidated_membership()
  {
    var fixture = new Fixture();
    var membership = fixture.AddEligibleMembership();

    var result = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));

    Assert.True(result.IsSuccess);
    var automatic = Assert.IsType<TenantSelectedAutomatically>(result.Value);
    Assert.Equal(membership.TenantId, automatic.Session.TenantId);
    Assert.Single(fixture.Sessions.Values);
    Assert.Single(fixture.Sessions.Values[0].RefreshTokenRecords);
  }

  // ⚠ CITES `AC-IAM-0007` — *"When an identity has multiple active memberships, a tenant must be selected
  // BEFORE A TENANT-SCOPED TOKEN IS ISSUED."* The criterion has two halves and this test carries both, but
  // only one of them is in the assertions a reader notices:
  //
  //   *a tenant must be selected*   `TenantSelectionRequired` with both memberships offered
  //   *before a token is issued*    **`Assert.Empty(fixture.Sessions.Values)`** — no session, so nothing
  //                                 tenant-scoped exists yet
  //
  // ⚠⚠ **THE EMPTY-SESSIONS LINE IS THE WHOLE SECOND CLAUSE AND READS AS BOOKKEEPING.** A handler that
  // returned the selection prompt AND created a session would satisfy every other assertion here while
  // issuing exactly the token the criterion forbids. **Do not delete it as redundant with the prompt.**
  //
  // Paired with `Begin_tenant_access_automatically_selects_one_revalidated_membership` (`AC-IAM-0006`):
  // that one asserts a session EXISTS for a single membership, this one that none exists for several.
  // **Neither alone separates a working rule from a handler stuck on one branch.**
  [Fact]
  [Trait("Acceptance", "AC-IAM-0007")]
  [Trait("Acceptance", "AC-AUTH-0003")]
  // `AC-AUTH-0003` — *"Multiple memberships require a SHORT-LIVED selection transaction."* Two eligible
  // memberships produce exactly one selection transaction and **no session**, which is the *require* half:
  // a handler that auto-selected one of many would leave `Sessions` non-empty.
  //
  // ⚠ *SHORT-LIVED* IS NOT WITNESSED HERE and this fixture cannot witness it — no clock advances. The
  // five-minute lifetime belongs to `AC-AUTH-0028`, and the nearest thing to a witness is
  // `Tenant_selection_creates_one_session_and_cannot_be_replayed`, which pins SINGLE-USE rather than
  // EXPIRY. **Two different ways for a proof to stop working, and only one of them is tested.**
  public async Task Begin_tenant_access_creates_single_use_selection_proof_for_multiple_memberships()
  {
    var fixture = new Fixture();
    fixture.AddEligibleMembership("Tenant One");
    fixture.AddEligibleMembership("Tenant Two");

    var result = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));

    Assert.True(result.IsSuccess);
    var selection = Assert.IsType<TenantSelectionRequired>(result.Value);
    Assert.Equal(2, selection.Memberships.Count);
    Assert.Single(fixture.Selections.Values);
    Assert.True(selection.SelectionProof.IsAvailable);
    Assert.Empty(fixture.Sessions.Values);
  }

  [Fact]
  [Trait("Acceptance", "AC-AUTH-0028")]
  // `AC-AUTH-0028`, quoted to its terminal full stop — *"A tenant-selection proof is persisted only as
  // selector plus exact 32-byte hash, uses the canonical 76-character format, lasts five minutes, is
  // single-use, and is consumed only with successful session creation."* FIVE clauses; this test carries
  // TWO of them:
  //
  //   single-use                    the replay fails and `Sessions` still holds exactly one
  //   consumed only WITH successful `ConsumedUtc` is set on the transaction that produced the session —
  //   session creation              the two are asserted together, which is what makes it *with* rather
  //                                 than *before*
  //
  // ⚠ NOT WITNESSED HERE, and named rather than left to a reader to discover: **selector-plus-32-byte-hash
  // persistence** (a storage-shape claim — the SQL Server suite asserts `binary(32)`), **the canonical
  // 76-character format**, and **the five-minute lifetime** — no clock advances in this fixture, so
  // *lasts five minutes* is unobservable here in either direction.
  public async Task Tenant_selection_creates_one_session_and_cannot_be_replayed()
  {
    var fixture = new Fixture();
    var selected = fixture.AddEligibleMembership("Tenant One");
    fixture.AddEligibleMembership("Tenant Two");
    var begin = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));
    var required = Assert.IsType<TenantSelectionRequired>(begin.Value);
    var rawProof = required.SelectionProof.RevealOnce().Value;
    var handler = fixture.SelectHandler();

    var first = await handler.HandleAsync(new SelectTenantCommand(
      new SensitiveAuthenticationTokenInput(rawProof), Client, selected.TenantUserId, selected.TenantId));
    var replay = await handler.HandleAsync(new SelectTenantCommand(
      new SensitiveAuthenticationTokenInput(rawProof), Client, selected.TenantUserId, selected.TenantId));

    Assert.True(first.IsSuccess);
    Assert.True(replay.IsFailure);
    Assert.Single(fixture.Sessions.Values);
    Assert.NotNull(fixture.Selections.Values.Single().ConsumedUtc);
  }

  // `AC-AUTH-0024`'s SECOND CLAUSE — *"and session creation revalidates its `SecurityVersion`"*. The command
  // carries `SecurityVersion + 1`, so the capability is well-formed and merely STALE: the refusal can only
  // come from revalidation against persisted state, not from a malformed input.
  //
  // ⚠ The two `Assert.Empty` calls are the *"before tenant state is created"* half and they are not
  // decoration — a handler that revalidated **after** writing the selection would still return the failure
  // and pass the first two assertions.
  [Fact]
  [Trait("Acceptance", "AC-AUTH-0024")]
  public async Task Stale_verified_identity_is_rejected_before_tenant_state_is_created()
  {
    var fixture = new Fixture();
    fixture.AddEligibleMembership();

    var result = await fixture.BeginHandler().HandleAsync(
      new BeginTenantAccessCommand(new VerifiedIdentity(fixture.Account.IdentityId, fixture.Account.SecurityVersion + 1), Client));

    Assert.True(result.IsFailure);
    Assert.Equal("Authentication.Failed", result.Error.Code);
    Assert.Empty(fixture.Sessions.Values);
    Assert.Empty(fixture.Selections.Values);
  }

  // `AC-AUTH-0032` — *"Session creation never leaves more than ten active unexpired sessions for an Identity
  // and revokes the oldest by `CreatedUtc` then `AuthenticationSessionId` using `SessionLimitExceeded`."*
  // Three of its four elements have a fixture here: the cap is `Assert.Equal(10, …Count(active for this
  // identity))` — **a cardinality bind, so a handler that revoked two would fail it as loudly as one that
  // revoked none**; the oldest is id 100, seeded at `Now.AddMinutes(0)`; and the reason is asserted by value.
  // The unrelated identity's session proves the sweep is scoped rather than global.
  //
  // ⚠⚠ **NOT WITNESSED: the tie-break.** *"by `CreatedUtc` THEN `AuthenticationSessionId`"* needs two
  // sessions sharing an instant, and the fixture gives every session a distinct `AddMinutes(index)`. **The
  // secondary key is exercised by nothing**, so a comparer that dropped it would stay green here.
  //
  // ⚠ Moved from a class-level trait, which claimed this criterion of all fourteen methods in the file.
  [Fact]
  [Trait("Acceptance", "AC-AUTH-0032")]
  public async Task Eleventh_session_revokes_deterministic_oldest_and_leaves_other_identity_unchanged()
  {
    var fixture = new Fixture();
    var membership = fixture.AddEligibleMembership();
    for (var index = 0; index < 10; index++)
    {
      fixture.Sessions.Values.Add(NewPersistedSession(100 + index, fixture.Account.IdentityId, membership, Now.AddMinutes(index)));
    }

    var unrelated = NewPersistedSession(999, fixture.Account.IdentityId + 1, membership with { IdentityId = fixture.Account.IdentityId + 1 }, Now.AddMinutes(-1));
    fixture.Sessions.Values.Add(unrelated);

    var created = await fixture.Creator.CreateAsync(fixture.Account, membership, Client, Now.AddHours(1), default);

    Assert.True(created.IsSuccess);
    Assert.Equal(AuthenticationSessionStatus.Revoked, fixture.Sessions.Values.Single(session => session.Id == 100).Status);
    Assert.Equal(AuthenticationSessionRevocationReason.SessionLimitExceeded, fixture.Sessions.Values.Single(session => session.Id == 100).RevocationReason);
    Assert.Equal(AuthenticationSessionStatus.Active, unrelated.Status);
    Assert.Equal(10, fixture.Sessions.Values.Count(session => session.IdentityId == fixture.Account.IdentityId && session.Status == AuthenticationSessionStatus.Active));
  }

  [Fact]
  [Trait("Acceptance", "AC-AUTH-0007")]
  [Trait("Acceptance", "AC-AUTH-0008")]
  // ==================================================================================================
  // `AC-AUTH-0007` — *"Successful refresh invalidates the submitted refresh token."*
  // `AC-AUTH-0008` — *"Reuse revokes the approved scope and requires reauthentication."*
  // ==================================================================================================
  //
  // One fixture, two criteria, because the second is only reachable through the first: the reuse can only
  // be DETECTED if the successful refresh invalidated the token it consumed.
  //
  //   0007  `Assert.NotNull(predecessor.ConsumedUtc)` — added here. ⚠ **Before it, 0007 was witnessed only
  //         INDIRECTLY, by the second call failing** — which is evidence that reuse is refused, not that
  //         the submitted token was invalidated. *A refusal has many possible causes and the criterion
  //         names one.*
  //
  //         ⚠⚠ AND IT CANNOT BE SHOWN LOAD-BEARING BY A PLANT, WHICH IS WORTH SAYING RATHER THAN LEAVING
  //         AS AN UNCLAIMED GAP. Consumption is WHY the reuse is detected, so any plant that nulls
  //         `ConsumedUtc` also makes the second call SUCCEED — and `Assert.True(reuse.IsFailure)` fails
  //         first, three lines earlier. **There is no state in the current implementation where this
  //         assertion fails and the others pass**, which by the usual test makes it emphasis.
  //
  //         *It is kept anyway, and the reason is specific rather than sentimental:* **it pins WHICH
  //         MECHANISM invalidates the token.** Move reuse detection to another carrier — a separate used
  //         flag, a revocation row — and the reuse assertion still passes while this one fails. So it
  //         constrains the implementation to the one the criterion names, and that is content, not
  //         decoration. Labelled so nobody reads it as an independently verified leg.
  //   0008  the session becomes `Compromised` and the SUCCESSOR carries `RevokedUtc` — the "approved
  //         scope" being the whole token family, not merely the reused token.
  //
  // ⚠⚠ 0008's SECOND CLAUSE IS NOT WITNESSED HERE: *"and requires reauthentication."* A `Compromised`
  // session cannot refresh, so the property plausibly follows — **but no test in this file drives a
  // subsequent login, and "plausibly follows" is the reasoning a citation is supposed to replace.**
  public async Task Successful_refresh_rotates_once_and_verified_predecessor_reuse_compromises_session()
  {
    var fixture = new Fixture();
    var membership = fixture.AddEligibleMembership();
    var session = NewPersistedSession(501, fixture.Account.IdentityId, membership, Now);
    var generated = fixture.TokenService.GenerateRefreshToken(session.Id, session.TokenFamilyId, Client);
    var raw = generated.SensitiveToken.RevealOnce().Value;
    var predecessor = session.CreateInitialRefreshToken(generated.PublicId, generated.SecretHash, Now, Guid.NewGuid());
    SetId(predecessor, 601);
    fixture.Sessions.Values.Add(session);
    fixture.Sessions.Locator = new RefreshTokenSessionLocator(session.Id, session.IdentityId, session.TenantUserId, session.TenantId);
    var handler = fixture.RefreshHandler();
    var command = new RefreshAuthenticationSessionCommand(new SensitiveAuthenticationTokenInput(raw), Client);

    var first = await handler.HandleAsync(command);
    var reuse = await handler.HandleAsync(command);

    Assert.True(first.IsSuccess);
    Assert.True(reuse.IsFailure);
    Assert.Equal("AuthenticationSession.RefreshFailed", reuse.Error.Code);
    Assert.Equal(AuthenticationSessionStatus.Compromised, session.Status);
    Assert.Equal(2, session.RefreshTokenRecords.Count);
    Assert.NotNull(session.RefreshTokenRecords.Single(token => token.PublicId != predecessor.PublicId).RevokedUtc);
    // `AC-AUTH-0007` directly: the SUBMITTED token was invalidated by the successful refresh, rather than
    // the reuse merely having been refused for some other reason.
    Assert.NotNull(predecessor.ConsumedUtc);
  }

  // ==================================================================================================
  // ⚠⚠⚠ THE THREE TESTS BELOW EXIST BECAUSE `AC-TEN-0007`'s SECOND CLAUSE HAD NO WITNESS ANYWHERE, AND
  // THE REASON IS THIS FILE'S OWN FAKE. MEASURED BEFORE THEY WERE WRITTEN.
  // ==================================================================================================
  //
  // *"Suspending an Active Tenant makes current authentication eligibility false AND BLOCKS SUBSEQUENT
  // TENANT SELECTION, NEW-SESSION, AND REFRESH ELIGIBILITY DECISIONS."* Three decisions, one per test
  // below, **because a criterion naming three things is a SET and one test would claim all three.**
  //
  // WHAT THE PLANTS SHOWED, AT TASK SCOPE, BEFORE THESE EXISTED:
  //   `IsEligible => Membership is not null && IsTenantEligible` cut to `=> Membership is not null`,
  //   disabling the gate for SELECTION and NEW-SESSION at once   ---> GATE GREEN, zero of 3,291
  //   the refresh gate's condition made dead                     ---> GATE GREEN, zero of 3,291
  //
  // **A suspended tenant could be selected into, could start a session, and could refresh indefinitely,
  // and nothing said a word.** The guard was correct and unobserved — armed, not current.
  //
  // ⚠ THE CAUSE WAS NOT A MISSING TEST, IT WAS THE FAKE. `FakeMembershipService` returned
  // `(membership, membership is not null)`, deriving the TENANT'S STATUS from WHETHER THE USER IS A
  // MEMBER. `A && B` where `B` is defined as `A` is just `A`, so the tenant half was not weakly covered
  // here — **it could not be expressed.** See the note on that class for why the two fields must stay
  // independent.
  //
  // ⚠⚠ THE ABSENCE WAS BOUNDED THREE WAYS BEFORE BUILDING, AND THE INTEGRATION ONE IS THE STRONG FORM.
  // By NAME: nothing in `tests/` references `AuthenticationSessionRevocationReason.TenantIneligible` or
  // `IsTenantEligible` — and the only `TenantIneligible` matches in the tree are
  // `LocalizationErrors.TenantIneligible`, **a different symbol in a different package that would have
  // read as coverage to a name census.** By MECHANISM: six test files reach these handlers; three are
  // structural Architecture tests and the rest arrive through this fake. By CONSTRUCTION at the
  // Integration layer: `PlatformAuthenticationPersistenceTests` uses the REAL eligibility read service
  // against a real database, so a tenant's actual status would matter there — and all three of its
  // tenants are built with `Tenant.Create` (always `Provisioning`) and then `Activate`d by hand at
  // `:704`, `:778` and `:902`, with no `Suspend` or `Archive` anywhere in the file. **Not "the token does
  // not appear" — every tenant it builds is walked to Active deliberately, so the suspended state is not
  // expressible there without new code.**
  //
  // ---- ⚠⚠⚠ PLANT MATRIX, RE-RUN AFTER THESE THREE EXISTED. THE POINT IS DISJOINTNESS, NOT REDNESS.
  //
  // Two plants, three tests. A single plant reddening all three would mean one control counted three
  // times; what is wanted is that **each test observes ITS OWN decision and is blind to the others**,
  // because the criterion names three decisions and a set is only covered member by member.
  //
  //                                              PLANT A                  PLANT B
  //                                              `IsEligible` gate cut    refresh gate made dead
  //   `..._at_tenant_selection`                  RED                      green
  //   `..._single_membership_..._automatically`  RED                      green
  //   `..._revokes_the_session_..._refresh`      green                    RED
  //   every other test in 3,294                  green                    green
  //
  // Plant A reaches selection and new-session because both consult `IsEligible`; refresh reads
  // `IsTenantEligible` directly and is correctly untouched by it. **The GREEN in each row is worth as much
  // as the RED** — it is what says these are three witnesses rather than one witness counted three times.
  //
  // ⚠ AND ONE GATE-READING WARNING FOR WHOEVER RE-RUNS THESE. My first attempt at plant B was `if
  // (false)`, which produced **GATE RED WITH EVERY SUITE PASSING** — red on compiler warning CS0162,
  // unreachable code. THE COLOUR SAID CAUGHT AND THE TEXT SAID UNREACHABLE CODE, and stopping at the
  // colour would have produced the exact opposite finding. **A plant that trips a warning is void the way
  // a plant that fails to compile is void, and it is worse, because a build failure announces itself
  // while this announces itself as a caught defect.** Plant B above instead uses a condition already false
  // at that line — `Membership` is non-null by `:72` — which kills the branch with nothing constant in it.
  [Fact]
  [Trait("Acceptance", "AC-TEN-0007")]
  [Trait("Acceptance", "AC-AUTH-0023")]
  // ==================================================================================================
  // `AC-AUTH-0023`, QUOTED TO ITS TERMINAL FULL STOP — *"Tenant resolution, selection, session creation,
  // and refresh use FP-003 eligibility; only an existing Active Tenant is eligible."*
  // ==================================================================================================
  //
  // FOUR NAMED FLOWS, AND THE THREE TESTS BELOW ALREADY COVER THREE OF THEM WITH A DISJOINTNESS MATRIX
  // (see the plant matrix above): selection here, session creation on the auto-select twin, refresh on the
  // third. **The criterion names a SET of flows, and a set is only covered member by member** — which is
  // exactly why those three exist separately rather than as one test.
  //
  //   RESOLUTION       `Login_offering_multiple_memberships_omits_a_deactivated_one` (E2E) for the
  //                    membership half; the tenant half is the `Tenants.Where(Status == Active)` join,
  //                    exercised through the single-membership twin below.
  //   SELECTION        this test
  //   SESSION CREATION `..._where_a_single_membership_would_be_selected_automatically`
  //   REFRESH          `..._revokes_the_session_when_a_refresh_is_attempted`
  //
  // ⚠ *ONLY AN EXISTING ACTIVE TENANT* HAS TWO WORDS AND ONLY ONE IS WITNESSED. *Active* is these three
  // tests. **EXISTING is not** — no fixture presents a tenant id that resolves to no row, and the
  // `TenantEligible` flag models a tenant that exists and is ineligible. *A non-existent tenant and a
  // suspended one reach the same refusal by different paths, and only one of them is tested.*
  //
  // ⚠⚠ THE CROSS-PACKAGE CITATION IS THE POINT, NOT AN AFTERTHOUGHT: these tests were written for
  // `AC-TEN-0007` and this file's subject never mentions FP-002. **A reader auditing AUTH's coverage would
  // not find them by name, by file, or by package** — which is the concentration hazard cross-package
  // traits create and also the thing that fixes it.
  public async Task Suspended_tenant_is_refused_at_tenant_selection()
  {
    var fixture = new Fixture();
    var selected = fixture.AddEligibleMembership("Tenant One");
    fixture.AddEligibleMembership("Tenant Two");
    var begin = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));
    var required = Assert.IsType<TenantSelectionRequired>(begin.Value);
    var rawProof = required.SelectionProof.RevealOnce().Value;

    // Suspended AFTER a valid proof was issued — the tenant was eligible when the user began.
    fixture.Memberships.TenantEligible = false;

    var result = await fixture.SelectHandler().HandleAsync(new SelectTenantCommand(
      new SensitiveAuthenticationTokenInput(rawProof), Client, selected.TenantUserId, selected.TenantId));

    Assert.True(result.IsFailure);
    Assert.Empty(fixture.Sessions.Values);

    // ⚠ THE PAIRED POSITIVE IS `Tenant_selection_creates_one_session_and_cannot_be_replayed`, which runs
    // these exact steps with `TenantEligible` at its default of `true` and gets a session. Without it,
    // this test is equally satisfied by selection failing for every tenant.
  }

  // ⚠⚠⚠ THE FLIP IS INSIDE THIS TEST RATHER THAN IN A NEIGHBOUR, BECAUSE `NoEligibleMembership` IS
  // RETURNED FOR TWO DIFFERENT CAUSES AND THE ASSERTION ALONE CANNOT SAY WHICH FIRED.
  // `BeginTenantAccessCommandHandler` returns it at `:43` for ZERO memberships and again at `:56` for an
  // INELIGIBLE TENANT. `Begin_tenant_access_returns_no_membership_without_creating_authentication_state`
  // already covers the first. **A handler that returned `NoEligibleMembership` unconditionally would
  // satisfy both that test and the first half of this one**, so the second half flips the single variable
  // and requires the outcome to change.
  [Fact]
  [Trait("Acceptance", "AC-TEN-0007")]
  [Trait("Acceptance", "AC-AUTH-0023")]
  public async Task Suspended_tenant_is_refused_where_a_single_membership_would_be_selected_automatically()
  {
    var fixture = new Fixture();
    fixture.AddEligibleMembership();
    fixture.Memberships.TenantEligible = false;

    var suspended = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));

    Assert.IsType<NoEligibleMembership>(suspended.Value);
    Assert.Empty(fixture.Sessions.Values);
    Assert.Single(fixture.Memberships.Values);

    // ONE VARIABLE CHANGES. The membership, the account and the client are the same objects.
    fixture.Memberships.TenantEligible = true;

    var active = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));

    Assert.IsType<TenantSelectedAutomatically>(active.Value);
    Assert.Single(fixture.Sessions.Values);
  }

  // ⚠ NO AMBIGUITY TO RESOLVE HERE: the revocation reason is a unique observable. Refresh does not merely
  // refuse a suspended tenant, it REVOKES the session, and `TenantIneligible` is reachable from exactly
  // one branch — `RefreshAuthenticationSessionCommandHandler:77-80`. Asserting the reason rather than the
  // failure is what makes this about tenant status rather than about refresh failing for any of its six
  // other reasons.
  [Fact]
  [Trait("Acceptance", "AC-TEN-0007")]
  [Trait("Acceptance", "AC-AUTH-0023")]
  public async Task Suspended_tenant_revokes_the_session_when_a_refresh_is_attempted()
  {
    var fixture = new Fixture();
    var membership = fixture.AddEligibleMembership();
    var session = NewPersistedSession(801, fixture.Account.IdentityId, membership, Now);
    var generated = fixture.TokenService.GenerateRefreshToken(session.Id, session.TokenFamilyId, Client);
    var raw = generated.SensitiveToken.RevealOnce().Value;
    var initial = session.CreateInitialRefreshToken(generated.PublicId, generated.SecretHash, Now, Guid.NewGuid());
    SetId(initial, 901);
    fixture.Sessions.Values.Add(session);
    fixture.Sessions.Locator = new RefreshTokenSessionLocator(session.Id, session.IdentityId, session.TenantUserId, session.TenantId);

    // The session was created while the tenant was eligible; it is suspended between issue and refresh.
    fixture.Memberships.TenantEligible = false;

    var result = await fixture.RefreshHandler().HandleAsync(
      new RefreshAuthenticationSessionCommand(new SensitiveAuthenticationTokenInput(raw), Client));

    Assert.True(result.IsFailure);
    Assert.Equal(AuthenticationSessionStatus.Revoked, session.Status);
    Assert.Equal(AuthenticationSessionRevocationReason.TenantIneligible, session.RevocationReason);
  }

  [Fact]
  // Key is `Acceptance`, not `Criterion`: **this file states its own convention at `AC-IAM-0006` and gives
  // the reason** — both keys carry `AC-` ids repo-wide, nothing validates either, so local consistency is
  // all a reader can rely on. My first version of this citation used `Criterion` and was the only one of
  // nine in the file; corrected rather than left as the exception that starts the drift.
  [Trait("Acceptance", "AC-AUTH-0010")]
  // ==================================================================================================
  // `AC-AUTH-0010` — *"Current-session logout does not revoke unrelated sessions."*
  // ==================================================================================================
  //
  // ⚠⚠⚠ THIS TEST WAS NAMED *"revokes ONLY the bound session"* AND ITS FIXTURE HELD EXACTLY ONE SESSION.
  // Every assertion was about that session's status and reason. **With one session in the world there is
  // no unrelated session to leave alone, so the word ONLY was unobservable** — a logout that revoked every
  // session for the identity passed every line.
  //
  // ***AN "ONLY" OVER A POPULATION OF ONE IS NOT A WEAK ASSERTION, IT IS NO ASSERTION*** — the same shape
  // as a permission test whose caller holds no permissions, arriving here through a fixture's SIZE rather
  // than through its contents. The name was accurate about intent and silent about the arrangement.
  //
  // A second Active session for the same identity is now present and required to survive. ⚠ It is for the
  // SAME identity deliberately: a session belonging to somebody else would be left alone by any
  // implementation that filters by identity at all, and the mistake this criterion guards against —
  // wiring `AC-AUTH-0011`'s logout-all into this handler — filters by exactly that.
  //
  // ⚠ `ListActiveByIdentityForUpdateAsync` already exists on the repository for the session-limit rule, so
  // the wrong implementation is one line away and would read as a feature.
  public async Task Current_session_logout_revokes_only_the_bound_session_and_is_terminally_idempotent()
  {
    var fixture = new Fixture();
    var membership = fixture.AddEligibleMembership();
    var session = NewPersistedSession(701, fixture.Account.IdentityId, membership, Now);
    var sibling = NewPersistedSession(702, fixture.Account.IdentityId, membership, Now);
    fixture.Sessions.Values.Add(session);
    fixture.Sessions.Values.Add(sibling);
    var current = new FakeCurrentAuthenticationSession(new CurrentAuthenticationSession(
      fixture.Account.IdentityId, membership.TenantId, membership.TenantUserId,
      session.Id, Client, fixture.Account.SecurityVersion));
    var handler = new RevokeCurrentAuthenticationSessionCommandHandler(
      current, fixture.Accounts, fixture.Sessions, fixture.UnitOfWork, new TestClock());

    var first = await handler.HandleAsync(new RevokeCurrentAuthenticationSessionCommand());
    var second = await handler.HandleAsync(new RevokeCurrentAuthenticationSessionCommand());

    Assert.True(first.IsSuccess);
    Assert.True(second.IsSuccess);
    Assert.Equal(AuthenticationSessionStatus.Revoked, session.Status);
    Assert.Equal(AuthenticationSessionRevocationReason.UserLogout, session.RevocationReason);

    // The criterion's actual subject. Both are asserted: a handler that revoked the sibling would set its
    // status, and one that "revoked" it without a reason would set neither.
    Assert.Equal(AuthenticationSessionStatus.Active, sibling.Status);
    Assert.Null(sibling.RevocationReason);
  }

  private static AuthenticationSession NewPersistedSession(
    long id,
    long identityId,
    EligibleTenantMembership membership,
    DateTimeOffset createdUtc)
  {
    var session = AuthenticationSession.Create(
      identityId,
      membership.TenantUserId,
      membership.TenantId,
      Client.Value,
      Guid.NewGuid(),
      1,
      createdUtc,
      createdUtc.AddDays(30),
      createdUtc.AddDays(90));
    SetId(session, id);
    return session;
  }

  private sealed class Fixture
  {
    public Fixture()
    {
      Account = AuthenticationAccount.CreatePending(17, LoginEmail.Create("session.user@example.com").Value);
      SetId(Account, 7);
      Assert.True(Account.CompleteInitialSetup("test-password-hash", Guid.NewGuid(), Now).IsSuccess);
      Accounts.Value = Account;
      Capability = new VerifiedIdentity(Account.IdentityId, Account.SecurityVersion);
      Creator = new AuthenticationSessionCreator(Sessions, UnitOfWork, TokenService,
        new FakeClaimsProvider(), new FakeAccessTokenIssuer(), Policy);
    }

    public AuthenticationAccount Account { get; }
    public VerifiedIdentity Capability { get; }
    public FakeAccountRepository Accounts { get; } = new();
    public FakeSelectionRepository Selections { get; } = new();
    public FakeMembershipService Memberships { get; } = new();
    public FakeSessionRepository Sessions { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();
    public AuthenticationTokenService TokenService { get; } = new();
    public AuthenticationPolicy Policy { get; } = new();
    public AuthenticationSessionCreator Creator { get; }

    public EligibleTenantMembership AddEligibleMembership(string name = "Tenant")
    {
      var membership = new EligibleTenantMembership(Account.IdentityId, 30 + Memberships.Values.Count, Guid.NewGuid(), name);
      Memberships.Values.Add(membership);
      return membership;
    }

    public BeginTenantAccessCommandHandler BeginHandler() => new(
      Accounts,
      Selections,
      Memberships,
      new AllowedClientRegistry(),
      TokenService,
      Creator,
      UnitOfWork,
      Policy,
      new TestClock());

    public RefreshAuthenticationSessionCommandHandler RefreshHandler() => new(
      Accounts,
      Sessions,
      Memberships,
      new AllowedClientRegistry(),
      TokenService,
      new FakeClaimsProvider(),
      new FakeAccessTokenIssuer(),
      UnitOfWork,
      Policy,
      new TestClock());

    public SelectTenantCommandHandler SelectHandler() => new(
      Accounts,
      Selections,
      Memberships,
      new AllowedClientRegistry(),
      TokenService,
      Creator,
      UnitOfWork,
      new TestClock());
  }

  private sealed class FakeAccountRepository : IAuthenticationAccountRepository
  {
    public AuthenticationAccount? Value { get; set; }
    public Task<AuthenticationAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Value?.Id == id ? Value : null);
    public Task<AuthenticationAccount?> GetByIdentityIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Value?.IdentityId == id ? Value : null);
    public Task<AuthenticationAccount?> GetByIdForUpdateAsync(long id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
    public Task<AuthenticationAccount?> GetByIdentityIdForUpdateAsync(long id, CancellationToken cancellationToken = default) => GetByIdentityIdAsync(id, cancellationToken);
    public Task<AuthenticationAccount?> GetByNormalizedLoginEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(Value?.NormalizedLoginEmail == email ? Value : null);
    public Task ReloadAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  // ⚠⚠⚠ THIS FAKE USED TO COLLAPSE TWO INDEPENDENT CONDITIONS INTO ONE, AND THAT MADE A PRODUCTION GUARD
  // UNREACHABLE FROM EVERY TEST IN THIS FILE.
  //
  // It returned `new IdentityTenantMembershipEligibility(membership, membership is not null)` — **the
  // TENANT'S STATUS was set to WHETHER THE USER IS A MEMBER**, which are unrelated facts. Since
  // `IsEligible => Membership is not null && IsTenantEligible`, that degenerates to `Membership is not
  // null`, so the tenant-status half of the condition was not weakly covered here: **IT COULD NOT BE
  // EXPRESSED.** Deleting the tenant check from `SelectTenantCommandHandler`, from
  // `BeginTenantAccessCommandHandler` and from `RefreshAuthenticationSessionCommandHandler` left the whole
  // gate GREEN — measured, see the three tests above.
  //
  // ⚠ AND THE STUB IS THE REASON A SHARED MECHANISM HAD NO WITNESSES. `IsTenantEligible` is reached by three
  // handlers, which ought to accumulate coverage from three directions; every one of them arrived through
  // this fake. **A collaborator that is stubbed identically by all of its consumers is not shared for
  // coverage purposes — it is absent from all of them.**
  //
  // `TenantEligible` now defaults to `true`, so every pre-existing test in this file behaves exactly as
  // before, and the null-membership branch mirrors `IdentityTenantMembershipReadService:69-81` — which
  // returns `(null, false)` for a missing membership and the TENANT'S ACTUAL STATUS otherwise.
  private sealed class FakeMembershipService : IIdentityTenantMembershipReadService
  {
    public List<EligibleTenantMembership> Values { get; } = [];

    public bool TenantEligible { get; set; } = true;

    public Task<IReadOnlyList<EligibleTenantMembership>> ListEligibleMembershipsAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<EligibleTenantMembership>>(Values.Where(value => value.IdentityId == identityId).ToArray());
    public Task<IdentityTenantMembershipEligibility> GetMembershipEligibilityForUpdateAsync(long identityId, long tenantUserId, Guid tenantId, CancellationToken cancellationToken = default)
    {
      var membership = Values.SingleOrDefault(value => value.IdentityId == identityId && value.TenantUserId == tenantUserId && value.TenantId == tenantId);
      return Task.FromResult(membership is null
        ? new IdentityTenantMembershipEligibility(null, false)
        : new IdentityTenantMembershipEligibility(membership, TenantEligible));
    }
  }

  private sealed class FakeSessionRepository : IAuthenticationSessionRepository
  {
    public List<AuthenticationSession> Values { get; } = [];
    public RefreshTokenSessionLocator? Locator { get; set; }
    public Task<AuthenticationSession?> GetByIdForUpdateAsync(long id, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(value => value.Id == id));
    public Task<RefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(Guid publicId, CancellationToken cancellationToken = default) => Task.FromResult(Locator);
    public Task<AuthenticationSession?> GetByRefreshTokenForUpdateAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Values.SingleOrDefault(value => value.Id == id));
    public Task<IReadOnlyList<AuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(long identityId, DateTimeOffset utcNow, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<AuthenticationSession>>(Values.Where(value => value.IdentityId == identityId && value.IsUsable(utcNow)).ToArray());
    public Task<IReadOnlyList<AuthenticationSession>> ListActiveByIdentityForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<AuthenticationSession>>(Values.Where(value => value.IdentityId == identityId && value.Status == AuthenticationSessionStatus.Active).ToArray());
    public Task AddAsync(AuthenticationSession session, CancellationToken cancellationToken = default)
    {
      SetId(session, Values.Count == 0 ? 1 : Values.Max(value => value.Id) + 1);
      Values.Add(session);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeSelectionRepository : ITenantSelectionTransactionRepository
  {
    public List<TenantSelectionTransaction> Values { get; } = [];
    public Task<long?> GetIdentityIdByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(value => value.PublicId == publicId)?.IdentityId);
    public Task<TenantSelectionTransaction?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(value => value.PublicId == publicId));
    public Task AddAsync(TenantSelectionTransaction transaction, CancellationToken cancellationToken = default)
    {
      SetId(transaction, Values.Count + 1);
      Values.Add(transaction);
      return Task.CompletedTask;
    }
  }

  private sealed class AllowedClientRegistry : IAuthenticationClientRegistry
  {
    public bool IsAllowed(AuthenticationClientId clientId) => clientId == Client;
  }

  private sealed class FakeClaimsProvider : IAccessTokenClaimsProvider
  {
    public Task<Result<AccessTokenClaims>> GetClaimsAsync(long authenticationSessionId, long identityId,
      long tenantUserId, Guid tenantId, AuthenticationClientId clientId, long securityVersion,
      CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(new AccessTokenClaims(
        "test-subject", identityId, tenantId, tenantUserId, authenticationSessionId, clientId, securityVersion, [], [])));
  }

  private sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
  {
    public Result<IssuedAccessToken> Issue(AccessTokenClaims claims, DateTimeOffset issuedUtc) =>
      Result.Success(new IssuedAccessToken(new SensitiveAccessToken("test-access-token"), issuedUtc.AddMinutes(15)));

    public Result<IssuedAccessToken> Issue(PlatformAccessTokenClaims claims, DateTimeOffset issuedUtc) =>
      Result.Success(new IssuedAccessToken(new SensitiveAccessToken("test-platform-access-token"), issuedUtc.AddMinutes(15)));
  }

  private sealed class FakeCurrentAuthenticationSession(CurrentAuthenticationSession value)
    : ICurrentAuthenticationSession
  {
    public CurrentAuthenticationSession? Value { get; } = value;
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(1));
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.FromResult<ITransaction>(new FakeTransaction());
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

  private static void SetId(object entity, long id)
  {
    var field = typeof(Entity<long>).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(entity, id);
  }
}
