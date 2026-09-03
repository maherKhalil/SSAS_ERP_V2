using System.Reflection;
using Microsoft.Extensions.Options;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure.PlatformSupport;

namespace SSAS.Platform.Tests.PlatformSupport;

// Phase 3B platform-support bootstrap (ADR-016 / DEC-TEN-0019/0021): the authority-administration
// permission, fail-closed options validation, and the deterministic genesis/recovery orchestrator.
public sealed class PlatformSupportBootstrapTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

  private static readonly PlatformPermissionCatalog Catalog = new();

  // ---- Permission (ADR-016 / DEC-TEN-0021) ----

  [Fact]
  public void Administer_platform_support_is_a_platform_support_scoped_catalog_permission()
  {
    Assert.True(Catalog.TryGet(PlatformPermissionNames.AdministerPlatformSupport, out var definition));
    Assert.Equal("Platform.Support.Administer", PlatformPermissionNames.AdministerPlatformSupport);
    Assert.Equal(SSAS.Platform.Domain.Enums.PermissionScope.PlatformSupport, definition.Scope);
  }

  [Fact]
  public void Administer_platform_support_can_be_granted_to_a_principal()
  {
    var principal = PlatformSupportPrincipal.Register(7).Value;
    Assert.True(Catalog.TryGet(PlatformPermissionNames.AdministerPlatformSupport, out var definition));

    Assert.True(principal.GrantPermission(definition, "actor", Now).IsSuccess);
    Assert.Contains(principal.ActivePermissions, permission => permission.Value == PlatformPermissionNames.AdministerPlatformSupport);
  }

  // ---- Options validation (ADR-016 / DEC-TEN-0021) ----

  private static ValidateOptionsResult Validate(PlatformSupportBootstrapOptions options) =>
    new PlatformSupportBootstrapOptionsValidator(Catalog).Validate(null, options);

  [Fact]
  public void Default_options_are_valid_and_inert()
  {
    // Unconfigured bootstrap (no subjects, default Administer-only grant set) must pass startup validation.
    var result = Validate(new PlatformSupportBootstrapOptions());

    Assert.True(result.Succeeded);
  }

  [Fact]
  public void A_well_formed_multi_subject_configuration_is_valid()
  {
    var result = Validate(new PlatformSupportBootstrapOptions
    {
      Subjects = ["local:alice", "local:bob"],
      InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.ViewTenants]
    });

    Assert.True(result.Succeeded);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData(" untrimmed")]
  [InlineData("untrimmed ")]
  public void Malformed_subjects_fail_validation(string subject)
  {
    var result = Validate(new PlatformSupportBootstrapOptions { Subjects = [subject] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void An_over_length_subject_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions { Subjects = [new string('a', 257)] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void Duplicate_subjects_fail_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions { Subjects = ["local:alice", "local:alice"] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void An_empty_initial_permission_set_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions { InitialPermissions = [] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void A_grant_set_missing_administer_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions { InitialPermissions = [PlatformPermissionNames.ViewTenants] });

    Assert.False(result.Succeeded);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0036")]
  // `AC-TEN-0036`'s TENANT-SCOPE CLAUSE — *"Bootstrap grants only known `PermissionScope.PlatformSupport`
  // permissions; an unknown or TENANT-SCOPED permission is rejected and no assignment is created."* A
  // tenant-scoped permission in the configured set fails validation. The UNKNOWN half is the next test,
  // which carries the same trait; **the two rejection reasons are different code paths and a criterion
  // naming both needs both.**
  public void A_tenant_scoped_initial_permission_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions
    {
      InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.ViewCompanies]
    });

    Assert.False(result.Succeeded);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0036")]
  // `AC-TEN-0036`'s UNKNOWN-PERMISSION CLAUSE. Paired with the tenant-scoped test above.
  //
  // ⚠ BOTH ARE CONFIGURATION-VALIDATION TESTS, SO WHAT THEY PROVE IS THAT SUCH A SET IS REFUSED BEFORE ANY
  // RUN — not that a run reaching the grant step would reject one. **`and no assignment is created` is
  // satisfied here VACUOUSLY, because validation fails before assignment is reached.** That is the correct
  // design and it is still a weaker witness than a run that got further.
  public void An_unknown_initial_permission_fails_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions
    {
      InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, "Platform.Unknown.Thing"]
    });

    Assert.False(result.Succeeded);
  }

  [Fact]
  public void Duplicate_initial_permissions_fail_validation()
  {
    var result = Validate(new PlatformSupportBootstrapOptions
    {
      InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.AdministerPlatformSupport]
    });

    Assert.False(result.Succeeded);
  }

  // ---- Orchestrator (ADR-016 / DEC-TEN-0019/0020/0021) ----

  [Fact]
  public async Task No_configured_subjects_short_circuits_without_any_persistence_access()
  {
    var authority = new FakeAuthorityState();
    var identities = new FakeIdentityRepository();
    var service = Build(new PlatformSupportBootstrapOptions(), authority, identities, new FakeAccountRepository(), new FakePrincipalRepository(), new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.NoCandidatesConfigured, outcome);
    Assert.Equal(0, authority.CallCount);
    Assert.Equal(0, identities.CallCount);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0035")]
  // `AC-TEN-0035`'s SECOND HALF — *"re-running bootstrap creates NO DUPLICATE principal or assignment and
  // DOES NOTHING when usable authority already exists."* `AuthorityAlreadyUsable` with `Added` null is that
  // clause. The audit half is on `Selection_is_deterministic_...`, which carries the same trait.
  //
  // ⚠ NOT CITED FOR `AC-TEN-0033`, THOUGH IT LOOKS LIKE THE OBVIOUS SITE. `0033` as it stands today is the
  // REFINED rule — bootstrap is inert only when general AND ADMINISTRATIVE authority are usable
  // (`DEC-TEN-0026`, Approved 2026-08-12). This test uses the one-argument `FakeAuthorityState(true)`, which
  // predates that distinction; the three tests under the `DEC-TEN-0026` heading below take both predicates
  // and are where `0033` is cited. **A test written against the narrow rule still passes under the wide one
  // and proves only the half they share.**
  public async Task Existing_usable_authority_makes_bootstrap_inert()
  {
    var authority = new FakeAuthorityState(true);
    var principals = new FakePrincipalRepository();
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      authority, EligibleWorld("local:alice"), EligibleAccounts("local:alice"), principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable, outcome);
    Assert.Null(principals.Added);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0048")]
  [Trait("Acceptance", "AC-TEN-0050")]
  [Trait("Acceptance", "AC-TEN-0035")]
  // THREE CRITERIA, AND THE FIXTURE IS WHAT MAKES EACH DISCRIMINATING RATHER THAN INCIDENTAL.
  //
  // `AC-TEN-0048` — *"a single bootstrap evaluation establishes EXACTLY ONE genesis principal — the FIRST
  // ELIGIBLE subject by ORDINAL comparison of the canonical subject (NEVER configuration insertion
  // order)."* ⚠ The subjects are configured `["local:bob", "local:alice"]` — **reverse ordinal order, and
  // both are eligible** — so choosing `alice` can only be ordinal comparison. Had they been configured
  // alphabetically the test would pass under either rule and prove neither.
  //
  // ⚠⚠ THE *EXACTLY ONE* HALF WAS NOT ASSERTABLE UNTIL THIS COMMIT. `Assert.Single(AddedPrincipals)` is new;
  // the fake previously kept only the last `AddAsync`, so a service establishing two principals passed
  // every line here. See the note on `FakePrincipalRepository` — **a count claim cannot be carried by a
  // one-element recorder**, and *exactly one* is a count claim wearing an identity assertion's clothes.
  //
  // `AC-TEN-0050` — *"Configured subjects OTHER than the selected one receive NO platform authority
  // automatically."* `local:bob` is eligible, configured FIRST, and gets nothing. That is the whole
  // criterion, and it rests on the same new `Assert.Single` — with a one-slot recorder, a bob principal
  // added before alice's was invisible.
  //
  // `AC-TEN-0035`'s AUDIT HALF — *"Genesis/recovery operations are audited with a DISTINGUISHABLE BOOTSTRAP
  // ACTOR."* `assignment.AssignedBy` is `platform-bootstrap:local:alice` on every assignment: prefixed so it
  // cannot collide with a human actor, and carrying WHICH subject seeded the plane. **The criterion's other
  // half — re-running creates no duplicate and does nothing when authority is usable — is at
  // `Existing_usable_authority_makes_bootstrap_inert`, which carries the same trait.**
  public async Task Selection_is_deterministic_ordinal_first_eligible_regardless_of_configuration_order()
  {
    // Both eligible; configured in reverse order. The ordinal-least subject (local:alice) must be chosen.
    var principals = new FakePrincipalRepository();
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:bob", "local:alice"] },
      new FakeAuthorityState(false), EligibleWorld("local:alice", "local:bob"), EligibleAccounts("local:alice", "local:bob"), principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.NotNull(principals.Added);
    // EXACTLY ONE — `AC-TEN-0048`'s cardinality and `AC-TEN-0050`'s "others get nothing" are the same
    // assertion seen from two sides, and neither was expressible before the recorder kept every add.
    Assert.Single(principals.AddedPrincipals);
    Assert.Equal(IdFor("local:alice"), principals.Added!.IdentityId);
    Assert.All(principals.Added.PermissionAssignments, assignment => Assert.Equal("platform-bootstrap:local:alice", assignment.AssignedBy));
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0031")]
  // `AC-TEN-0031` — *"The configured `AuthenticationSubject` must resolve to an EXISTING `Identity` … a
  // MISSING or ineligible subject creates no platform authority."* `local:alice` is deliberately absent from
  // the identity repository and is skipped rather than created.
  //
  // ⚠ THE CRITERION'S LAST SENTENCE — *"Bootstrap NEVER CREATES IDENTITIES"* — IS NOT ASSERTED ANYWHERE, AND
  // IT IS NOT ASSERTABLE THROUGH THIS FAKE. The identity repository double exposes no create path, so the
  // property holds by the SHAPE OF THE TEST DOUBLE rather than by anything the product is observed not to
  // do. **A capability the fake cannot offer is a capability the test cannot prove is unused** — the same
  // defect class as the one-slot recorder below, in the opposite direction.
  public async Task A_missing_first_candidate_is_skipped_for_the_next_eligible_one()
  {
    var identities = new FakeIdentityRepository();
    identities.Add("local:bob", IdFor("local:bob")); // local:alice is deliberately absent.
    var principals = new FakePrincipalRepository();
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice", "local:bob"] },
      new FakeAuthorityState(false), identities, EligibleAccounts("local:bob"), principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.Equal(IdFor("local:bob"), principals.Added!.IdentityId);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0031")]
  // `AC-TEN-0031`'s ELIGIBILITY HALF — *"must resolve to an … authentication-capable, ACTIVE
  // `AuthenticationAccount`; a missing or INELIGIBLE subject creates no platform authority."* `local:alice`
  // has an ineligible account and is skipped; `local:carol` seeds the plane. **`local:bob` is skipped for a
  // DIFFERENT reason — it already owns a principal — so the test carries two distinct exclusions and only
  // the first is this criterion's.**
  public async Task An_ineligible_account_and_an_already_owning_identity_are_both_skipped()
  {
    // local:alice's account is not authentication-eligible; local:bob already owns a principal;
    // only local:carol can seed authority.
    var accounts = new FakeAccountRepository();
    accounts.AddIneligible("local:alice", IdFor("local:alice"));
    accounts.AddEligible("local:bob", IdFor("local:bob"));
    accounts.AddEligible("local:carol", IdFor("local:carol"));
    var principals = new FakePrincipalRepository();
    principals.MarkExisting(IdFor("local:bob"));
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice", "local:bob", "local:carol"] },
      new FakeAuthorityState(false), EligibleWorld("local:alice", "local:bob", "local:carol"), accounts, principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.Equal(IdFor("local:carol"), principals.Added!.IdentityId);
  }

  [Fact]
  public async Task No_eligible_candidate_fails_closed_without_establishing_a_principal()
  {
    // Only a Disabled/existing principal case: the single configured identity already owns a principal,
    // so recovery is new-principal-only and finds nothing eligible.
    var principals = new FakePrincipalRepository();
    principals.MarkExisting(IdFor("local:alice"));
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      new FakeAuthorityState(false), EligibleWorld("local:alice"), EligibleAccounts("local:alice"), principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.NoEligibleCandidate, outcome);
    Assert.Null(principals.Added);
  }

  [Fact]
  public async Task Genesis_grants_the_full_configured_set_which_always_includes_administer()
  {
    var principals = new FakePrincipalRepository();
    var unitOfWork = new FakeUnitOfWork();
    var service = Build(
      new PlatformSupportBootstrapOptions
      {
        Subjects = ["local:alice"],
        InitialPermissions = [PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.ViewTenants]
      },
      new FakeAuthorityState(false), EligibleWorld("local:alice"), EligibleAccounts("local:alice"), principals, unitOfWork);

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.Equal(1, unitOfWork.SaveCount);
    var granted = principals.Added!.ActivePermissions.Select(permission => permission.Value).ToArray();
    Assert.Contains(PlatformPermissionNames.AdministerPlatformSupport, granted);
    Assert.Contains(PlatformPermissionNames.ViewTenants, granted);
    Assert.Equal(2, granted.Length);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0049")]
  // `AC-TEN-0049` — *"Concurrent bootstrap evaluations that both observe no usable authority CONVERGE, via
  // the authoritative unique `IdentityId`/active-assignment constraints, on EXACTLY ONE genesis/recovery
  // principal (the loser's duplicate is an idempotent race outcome); NO DISTRIBUTED LOCK is required."*
  //
  // ⚠ WHAT IS CARRIED IS THE LOSER'S BEHAVIOUR, WHICH IS THE HALF THAT CAN GO WRONG: the losing evaluation
  // meets the unique constraint and reconverges instead of failing or duplicating.
  //
  // ⚠⚠⚠ BUT THE CRITERION'S STATED MECHANISM IS SUPERSEDED, AND THE CITATION IS FOR THE OUTCOME ONLY.
  // `0049` says convergence happens *"VIA the authoritative unique `IdentityId`/active-assignment
  // constraints"* and that *"NO DISTRIBUTED LOCK is required"*. `PlatformSupportBootstrapService`'s own
  // header now says the opposite about which mechanism is primary: *"Convergence is provided by the recovery
  // serialization (`IPlatformSupportRecoverySerializer`) … IdentityId uniqueness remains as defense-in-depth
  // … it is NO LONGER THE PRIMARY multi-subject convergence mechanism."* And
  // `PlatformSupportRecoverySerializer` is **an exclusive lock on the platform-support principal table**,
  // taken inside the transaction — chosen because at genesis the table is empty, so there is no row to lock
  // and a candidate-keyed lock would let two workers holding two DIFFERENT locks both proceed.
  //
  // **So the trait claims the OUTCOME — exactly one principal survives a race — and not the route.** Whether
  // a single-database table lock falsifies *no distributed lock* is a reading of that phrase rather than a
  // measurement, and it is not mine to settle; the service author evidently judged it consistent, noting
  // *"no new locking primitive"*. Recorded here so the next reader compares the criterion to the code rather
  // than to this test.
  //
  // ⚠⚠ AND THIS IS A ROT KIND NO TEXT FILTER CAN FIND. Absences, phase markers, temporal antecedents and
  // quoted statuses all ANNOUNCE themselves with a word. **A superseded MECHANISM announces nothing** —
  // *"via the authoritative unique constraints"* contains no marker of any kind, and the only way to catch it
  // is to compare the criterion's stated how against the code's actual how.
  public async Task A_write_race_that_loses_the_unique_constraint_reconverges_on_the_winner()
  {
    // Pre-check sees no authority; the atomic insert loses the IdentityId uniqueness race; the live
    // re-read then observes the winner's authority, so this host converges rather than double-genesis.
    var authority = new FakeAuthorityState(false, true);
    var principals = new FakePrincipalRepository();
    var unitOfWork = new FakeUnitOfWork(IdentityAccessErrors.UniqueConstraintViolation);
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      authority, EligibleWorld("local:alice"), EligibleAccounts("local:alice"), principals, unitOfWork);

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable, outcome);
    // Both predicates are evaluated at the pre-check and again at the post-failure re-read (DEC-TEN-0026).
    Assert.Equal(2, authority.GeneralCallCount);
    Assert.Equal(2, authority.AdministrativeCallCount);
  }

  [Fact]
  public async Task A_write_race_that_loses_but_still_sees_no_authority_fails_closed()
  {
    var authority = new FakeAuthorityState(false, false);
    var unitOfWork = new FakeUnitOfWork(IdentityAccessErrors.UniqueConstraintViolation);
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      authority, EligibleWorld("local:alice"), EligibleAccounts("local:alice"), new FakePrincipalRepository(), unitOfWork);

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.NoEligibleCandidate, outcome);
  }

  // ---- DEC-TEN-0026 administrative recovery predicate ----
  //
  // ⚠⚠⚠ `AC-TEN-0033` IS CITED ACROSS THESE THREE, AND ONLY AGAINST THE **REFINED** RULE. The criterion's
  // first sentence reads *"bootstrap … is INERT ONCE USABLE AUTHORITY EXISTS"*, and its parenthetical adds
  // *"recovery is ADDITIONALLY ELIGIBLE when usable authority exists but no usable ADMINISTRATIVE authority
  // exists"* — `DEC-TEN-0026`, which `decisions-approved.md:11` records as **Approved for Implementation on
  // 2026-08-12**.
  //
  // ⚠⚠ THE CRITERION'S OWN PARENTHETICAL STILL CALLS THAT DECISION *PROPOSED*, AND THE STATUS WORD IS THREE
  // WEEKS STALE. That matters here more than anywhere else in the package: **under the unrefined first
  // sentence, `Administrative_loss_triggers_recovery_even_though_general_authority_survives` below reads as
  // a VIOLATION — general authority is usable and bootstrap is not inert.** A disposal against the narrow
  // text would have reported correct code as a product defect. `decisions-approved.md` (live tree) is the
  // authority; `acceptance-criteria.md`'s status word is not.
  //
  // THE TRIO IS A COMPLETE CASE ANALYSIS OVER THE TWO PREDICATES, WHICH IS WHY IT CARRIES THE REFINEMENT
  // RATHER THAN ILLUSTRATING IT:
  //   general TRUE  + administrative TRUE   inert            the ONLY inert state
  //   general TRUE  + administrative FALSE  recovery         the DEC-TEN-0026 case exactly
  //   general TRUE  + administrative FALSE  fails closed     …with no eligible configured subject
  // **The second row is the entire refinement**: general authority survives and recovery engages anyway.
  //
  // ⚠ `general FALSE` is not a row here because it is the ORIGINAL genesis path, covered above. So the
  // analysis is complete over the states the refinement introduced, not over the whole matrix — stated
  // rather than implied, because *complete* is a claim about a set and the set here is the two-predicate
  // space with one axis already covered elsewhere.
  [Fact]
  [Trait("Decision", "DEC-TEN-0026")]
  [Trait("Acceptance", "AC-TEN-0033")]
  public async Task Bootstrap_is_inert_only_when_both_general_and_administrative_authority_are_usable()
  {
    // general TRUE + administrative TRUE is the ONLY inert state.
    var principals = new FakePrincipalRepository();
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      new FakeAuthorityState([true], [true]), EligibleWorld("local:alice"), EligibleAccounts("local:alice"),
      principals, new FakeUnitOfWork());

    Assert.Equal(PlatformSupportBootstrapOutcome.AuthorityAlreadyUsable, await service.RunAsync());
    Assert.Null(principals.Added);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0026")]
  [Trait("Acceptance", "AC-TEN-0033")]
  [Trait("Acceptance", "AC-TEN-0051")]
  // ⚠ ALSO CARRIES `AC-TEN-0051`'s POSITIVE HALF — *"if another eligible configured subject owns no
  // principal, bootstrap establishes that subject as a NEW `Active` recovery principal"*. A new principal is
  // added for `local:alice` and granted `Administer`. **The criterion's OTHER half — that a `Disabled`
  // principal is never re-enabled and REMAINS `Disabled` — is NOT here: this fixture has no disabled
  // principal at all, so nothing in it could observe a re-enable.** Recorded rather than glossed.
  public async Task Administrative_loss_triggers_recovery_even_though_general_authority_survives()
  {
    // The DEC-TEN-0026 case: a surviving non-admin principal keeps general authority TRUE, but with no usable
    // Administer anywhere the plane can never be administered again — recovery must engage, not stay inert.
    var principals = new FakePrincipalRepository();
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      new FakeAuthorityState([true], [false]), EligibleWorld("local:alice"), EligibleAccounts("local:alice"),
      principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.GenesisEstablished, outcome);
    Assert.NotNull(principals.Added);
    // Recovery establishes a NEW configured principal and grants it Administer.
    Assert.Equal(IdFor("local:alice"), principals.Added!.IdentityId);
    Assert.Contains(
      principals.Added.PermissionAssignments,
      assignment => assignment.PermissionName.Value == PlatformPermissionNames.AdministerPlatformSupport);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0026")]
  [Trait("Acceptance", "AC-TEN-0033")]
  [Trait("Acceptance", "AC-TEN-0052")]
  // `AC-TEN-0052`'s FAIL-CLOSED CLAUSE — *"bootstrap fails closed (no implicit re-enable, no duplicate
  // principal)"*. `NoEligibleCandidate` with `Added` null is exactly that, and it is the stronger of the two
  // fail-closed sites because general authority SURVIVES here: the service has a live principal in front of
  // it and still refuses to elevate it.
  //
  // ⚠⚠ `0052`'s SECOND CLAUSE IS UNCOVERED AND NAMED: *"and EMITS AN OPERATOR DIAGNOSTIC that no eligible
  // recovery subject exists."* Nothing here observes a diagnostic — the outcome enum is the only channel
  // asserted, and an enum value is not an operator-facing message. **A silent fail-closed and a diagnosed
  // one are the same green.**
  public async Task Administrative_loss_without_an_eligible_configured_subject_fails_closed()
  {
    // No eligible configured subject: recovery must NOT elevate the surviving non-admin principal, must not
    // re-enable anything, and must fail closed.
    var principals = new FakePrincipalRepository();
    var service = Build(
      new PlatformSupportBootstrapOptions { Subjects = ["local:alice"] },
      new FakeAuthorityState([true], [false]), EligibleWorld(), EligibleAccounts(),
      principals, new FakeUnitOfWork());

    var outcome = await service.RunAsync();

    Assert.Equal(PlatformSupportBootstrapOutcome.NoEligibleCandidate, outcome);
    Assert.Null(principals.Added);
  }

  // ---- Fakes and builders ----

  private static PlatformSupportBootstrapService Build(
    PlatformSupportBootstrapOptions options,
    FakeAuthorityState authority,
    FakeIdentityRepository identities,
    FakeAccountRepository accounts,
    FakePrincipalRepository principals,
    FakeUnitOfWork unitOfWork) =>
    new(Options.Create(options), identities, accounts, principals, authority, new FakeRecoverySerializer(),
      Catalog, unitOfWork, new StubClock());

  private static long IdFor(string subject) => Math.Abs((long)subject.GetHashCode(StringComparison.Ordinal)) + 1;

  private static FakeIdentityRepository EligibleWorld(params string[] subjects)
  {
    var repository = new FakeIdentityRepository();
    foreach (var subject in subjects)
    {
      repository.Add(subject, IdFor(subject));
    }

    return repository;
  }

  private static FakeAccountRepository EligibleAccounts(params string[] subjects)
  {
    var repository = new FakeAccountRepository();
    foreach (var subject in subjects)
    {
      repository.AddEligible(subject, IdFor(subject));
    }

    return repository;
  }

  private static Identity IdentityWith(string subject, long id)
  {
    var identity = Identity.Create(AuthenticationSubject.Create(subject).Value);
    SetId(identity, id);
    return identity;
  }

  private static AuthenticationAccount EligibleAccount(string subject, long identityId)
  {
    var account = AuthenticationAccount.CreatePending(identityId, LoginEmail.Create($"{Sanitize(subject)}@example.com").Value);
    Assert.True(account.CompleteInitialSetup("hash", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(account.IsAuthenticationEligible);
    return account;
  }

  private static AuthenticationAccount IneligibleAccount(long identityId, string subject)
  {
    // PendingSetup: no password and unverified email, so not authentication-eligible.
    var account = AuthenticationAccount.CreatePending(identityId, LoginEmail.Create($"{Sanitize(subject)}@example.com").Value);
    Assert.False(account.IsAuthenticationEligible);
    return account;
  }

  private static string Sanitize(string subject) => subject.Replace(":", "-", StringComparison.Ordinal);

  private static void SetId(object entity, long id)
  {
    var field = typeof(Entity<long>).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field!.SetValue(entity, id);
  }

  // Models the two DEC-TEN-0026 predicates independently. The single-sequence constructor mirrors
  // administrative onto general (the fully-authorised / fully-absent states); the two-sequence constructor
  // expresses the admin-loss state where general authority survives but administrative authority does not.
  private sealed class FakeAuthorityState : IPlatformSupportAuthorityStateReadService
  {
    private readonly bool[] general;
    private readonly bool[] administrative;

    public FakeAuthorityState(params bool[] results)
      : this(results, results)
    {
    }

    public FakeAuthorityState(bool[] general, bool[] administrative)
    {
      this.general = general.Length == 0 ? [false] : general;
      this.administrative = administrative.Length == 0 ? [false] : administrative;
    }

    public int GeneralCallCount { get; private set; }

    public int AdministrativeCallCount { get; private set; }

    public int CallCount => GeneralCallCount + AdministrativeCallCount;

    public Task<bool> HasUsablePlatformAuthorityAsync(CancellationToken cancellationToken = default)
    {
      var index = Math.Min(GeneralCallCount, general.Length - 1);
      GeneralCallCount++;
      return Task.FromResult(general[index]);
    }

    public Task<bool> HasUsablePlatformAdministrativeAuthorityAsync(CancellationToken cancellationToken = default)
    {
      var index = Math.Min(AdministrativeCallCount, administrative.Length - 1);
      AdministrativeCallCount++;
      return Task.FromResult(administrative[index]);
    }
  }

  private sealed class FakeIdentityRepository : IIdentityRepository
  {
    private readonly Dictionary<string, Identity> bySubject = new(StringComparer.Ordinal);

    public int CallCount { get; private set; }

    public void Add(string subject, long id) => bySubject[subject] = IdentityWith(subject, id);

    public Task<Identity?> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default)
    {
      CallCount++;
      return Task.FromResult(bySubject.GetValueOrDefault(subject));
    }

    public Task<Identity?> GetByIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<bool> SubjectExistsAsync(string subject, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task AddAsync(Identity identity, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeAccountRepository : IAuthenticationAccountRepository
  {
    private readonly Dictionary<long, AuthenticationAccount> byIdentityId = [];

    public void AddEligible(string subject, long identityId) => byIdentityId[identityId] = EligibleAccount(subject, identityId);

    public void AddIneligible(string subject, long identityId) => byIdentityId[identityId] = IneligibleAccount(identityId, subject);

    public Task<AuthenticationAccount?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(byIdentityId.GetValueOrDefault(identityId));

    public Task<AuthenticationAccount?> GetByIdAsync(long authenticationAccountId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AuthenticationAccount?> GetByIdForUpdateAsync(long authenticationAccountId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AuthenticationAccount?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AuthenticationAccount?> GetByNormalizedLoginEmailAsync(string normalizedLoginEmail, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task ReloadAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task AddAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakePrincipalRepository : IPlatformSupportPrincipalRepository
  {
    private readonly HashSet<long> existingIdentityIds = [];

    // ⚠⚠⚠ `Added` WAS A SINGLE SLOT AND THAT MADE A CARDINALITY CLAIM UNASSERTABLE. `AddAsync` overwrote it,
    // so a service that established TWO principals left the LAST one here and every existing assertion still
    // passed. **`AC-TEN-0048` says a single evaluation establishes EXACTLY ONE genesis principal and
    // `AC-TEN-0050` says the other configured subjects receive no authority — neither is a claim a
    // one-element recorder can carry**, because both are about HOW MANY, and the recorder discards that.
    //
    // Same family as the derived-field fake in `AuthenticationSessionApplicationTests`: **the fake's SHAPE,
    // not the test's assertions, is what bounded what could be proved.** A recorder that keeps only the last
    // value silently converts every count assertion into a last-write assertion.
    //
    // `Added` is kept as the last-write convenience so every pre-existing test reads unchanged; assertions
    // about HOW MANY use `AddedPrincipals`.
    public List<PlatformSupportPrincipal> AddedPrincipals { get; } = [];

    public PlatformSupportPrincipal? Added => AddedPrincipals.Count == 0 ? null : AddedPrincipals[^1];

    public void MarkExisting(long identityId) => existingIdentityIds.Add(identityId);

    public Task<bool> ExistsForIdentityAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(existingIdentityIds.Contains(identityId));

    public Task AddAsync(PlatformSupportPrincipal principal, CancellationToken cancellationToken = default)
    {
      AddedPrincipals.Add(principal);
      return Task.CompletedTask;
    }

    public Task<PlatformSupportPrincipal?> GetByIdAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<PlatformSupportPrincipal?> GetByIdForUpdateAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<PlatformSupportPrincipal?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<PlatformSupportPrincipal?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeUnitOfWork(Error? failWith = null) : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(failWith is null ? Result.Success(1) : Result.Failure<int>(failWith));
    }

    // Recovery now runs inside a transaction that carries the cross-candidate serialization, so the fake
    // supplies a no-op transaction. Real convergence is proven only by the SQL Server tests.
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<ITransaction>(new FakeTransaction());
  }

  private sealed class FakeTransaction : ITransaction
  {
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  // In-memory fake always serializes successfully; genuine cross-candidate convergence is a database
  // property and is proven exclusively by PlatformSupportBootstrapSqlServerTests.
  private sealed class FakeRecoverySerializer : IPlatformSupportRecoverySerializer
  {
    public Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
  }

  private sealed class StubClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }
}
