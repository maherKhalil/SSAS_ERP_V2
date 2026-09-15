using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.TenantLifecycle;

public sealed class TenantLifecycleDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0010")]
  [Trait("Acceptance", "AC-TEN-0002")]
  [Trait("Scenario", "TS-TEN-0002")]
  public void Tenant_code_trims_preserves_casing_and_normalizes_invariantly()
  {
    var code = TenantCode.Create("  Acme-tr  ").Value;

    Assert.Equal("Acme-tr", code.Value);
    Assert.Equal("ACME-TR", code.NormalizedValue);
    Assert.Equal(code, TenantCode.Create("acme-TR").Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [Trait("Decision", "DEC-TEN-0004")]
  public void Tenant_code_rejects_missing_values(string? value)
  {
    Assert.True(TenantCode.Create(value).IsFailure);
  }

  [Fact]
  [Trait("Scenario", "TS-TEN-0002")]
  public void Tenant_code_rejects_values_over_64_characters()
  {
    Assert.True(TenantCode.Create(new string('A', 65)).IsFailure);
    Assert.True(TenantCode.Create(new string('A', 64)).IsSuccess);
    Assert.False(typeof(TenantCode).GetProperty(nameof(TenantCode.Value))!.CanWrite);
    Assert.False(typeof(TenantCode).GetProperty(nameof(TenantCode.NormalizedValue))!.CanWrite);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [Trait("BusinessRule", "BRULE-TEN-0011")]
  public void Tenant_name_rejects_missing_values(string? value)
  {
    Assert.True(TenantName.Create(value).IsFailure);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0011")]
  [Trait("Acceptance", "AC-TEN-0003")]
  [Trait("Scenario", "TS-TEN-0003")]
  public void Tenant_name_trims_preserves_casing_and_is_not_an_identity()
  {
    var first = CreateTenant("ONE", "  Shared Name  ");
    var second = CreateTenant("TWO", "Shared Name");

    Assert.Equal("Shared Name", first.TenantName.Value);
    Assert.Equal("Shared Name", second.TenantName.Value);
    Assert.NotEqual(first.TenantId, second.TenantId);
    Assert.True(TenantName.Create(new string('N', 201)).IsFailure);
  }

  [Fact]
  [Trait("Requirement", "FR-TEN-0101")]
  [Trait("Acceptance", "AC-TEN-0001")]
  [Trait("Scenario", "TS-TEN-0001")]
  public void Creation_uses_server_identifier_provisioning_created_reason_and_safe_event()
  {
    var tenant = CreateTenant();
    var created = Assert.IsType<TenantCreated>(Assert.Single(tenant.DomainEvents));

    Assert.NotEqual(Guid.Empty, tenant.TenantId);
    Assert.Equal(TenantStatus.Provisioning, tenant.Status);
    Assert.False(tenant.IsAuthenticationEligible);
    Assert.Equal(TenantStatusChangeReason.Created, tenant.StatusChangeReasonCode);
    Assert.Equal(Now, tenant.CreatedUtc);
    Assert.Equal(Now, tenant.StatusChangedUtc);
    Assert.Equal("platform-actor", tenant.CreatedBy);
    Assert.Equal("platform-actor", tenant.StatusChangedBy);
    Assert.Null(tenant.ModifiedUtc);
    Assert.Equal(tenant.TenantId, created.TenantId);
    Assert.Equal(TenantStatus.Provisioning, created.NewStatus);
    Assert.Equal(TenantStatusChangeReason.Created, created.StatusChangeReason);
    Assert.Equal(Now, created.OccurredUtc);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0009")]
  [Trait("Decision", "DEC-TEN-0001")]
  [Trait("Scenario", "TS-TEN-0008")]
  public void Creation_factory_generates_immutable_identifier_and_rejects_null_value_objects()
  {
    var code = TenantCode.Create("ACME").Value;
    var name = TenantName.Create("Acme Trading").Value;

    var missingCode = Tenant.Create(null!, name, "actor", Guid.NewGuid(), Now);
    var missingName = Tenant.Create(code, null!, "actor", Guid.NewGuid(), Now);
    var factory = Assert.Single(typeof(Tenant).GetMethods()
      .Where(method => method is { Name: nameof(Tenant.Create), IsPublic: true, IsStatic: true }));

    Assert.Equal("Tenant.InvalidCode", missingCode.Error.Code);
    Assert.Equal("Tenant.InvalidName", missingName.Error.Code);
    Assert.DoesNotContain(factory.GetParameters(), parameter =>
      string.Equals(parameter.Name, "tenantId", StringComparison.OrdinalIgnoreCase));
    Assert.False(typeof(Tenant).GetProperty(nameof(Tenant.TenantId))!.CanWrite);
    Assert.NotEqual(CreateTenant().TenantId, CreateTenant().TenantId);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0002")]
  [Trait("Decision", "DEC-TEN-0009")]
  // ==================================================================================================
  // ⚠⚠⚠ DO NOT DELETE THIS TEST. IT LOOKS LIKE A TAUTOLOGY AND THREE ACCEPTANCE CRITERIA REST ON IT.
  // ==================================================================================================
  //
  // **DELETING THIS CONVERTS THREE COMPLETE CASE ANALYSES INTO SAMPLES, AND NOTHING WILL REDDEN.** The
  // three tests below go on passing, their traits go on claiming their criteria, and each one silently
  // stops proving the half of its criterion that is a claim about a COMPLEMENT:
  //
  //   `AC-TEN-0008`  *eligibility is true ONLY for Active* — `TenantLifecycleApplicationTests
  //                  .Eligibility_is_derived_exactly_and_has_no_name` covers four statuses plus null. That
  //                  EXHAUSTS the statuses only because the line below fixes them at four.
  //   `AC-TEN-0009`  *NO OTHER STATUS can use the reactivation operation* — `Every_unapproved_transition_
  //                  preserves_state_metadata_and_events` refuses `Reactivate` from three statuses. Three
  //                  refusals are the whole complement only because the complement is three.
  //   `AC-TEN-0016`  *returns EXACTLY five members* — closed instead by the member pin in
  //                  `TenantLifecycleArchitectureTests`, which is the same idiom on a different set.
  //   `AC-SUB-0031`  ***AND A FOURTH, FROM ANOTHER PACKAGE ENTIRELY*** — *"`TenantStatusChangeReason`
  //                  contains **no commercial member** — no `NonPayment`, no `Expired`, no
  //                  `SubscriptionLapsed`. Asserted over the enum, so adding one fails the build's guards
  //                  rather than review."* FP-014 keeps commerce out of the tenant lifecycle, and **the
  //                  reason vocabulary below is the only thing in the tree that stops a commercial member
  //                  being added.** A subscription package under delivery pressure is exactly where
  //                  `NonPayment` gets proposed, and it would arrive as a one-line enum addition.
  //
  // A fifth `TenantStatus` added after this test is gone would arrive with no row covering it, no refusal
  // covering it, and a green suite.
  //
  // ⚠⚠ AND THE FOURTH DEPENDANT SHARPENS THE WARNING RATHER THAN LENGTHENING IT: **three of the four are
  // FP-003 criteria a reader of this file might plausibly know about. The fourth is FP-014's, in a package
  // this file never mentions** — so the person weighing whether this test earns its place cannot see the
  // whole cost of removing it from anything in front of them. *That is the argument for listing dependants
  // AT the depended-on test rather than at the depending ones.*
  //
  // ⚠ THE REASON THIS IS WORTH SHOUTING IS THAT IT IS THE HIGHEST-VALUE TARGET IN A TIDY-UP. It asserts an
  // enum against its own names; it reads as ceremony; every argument for removing dead tests points here
  // first. **A TEST WHOSE WHOLE VALUE IS BEING DEPENDED ON HAS NO VISIBLE VALUE OF ITS OWN**, and the
  // person who deletes it will be reading THIS file, not the three that need it — which is why the warning
  // is here rather than three notes written for someone who is not in the room.
  //
  // ⚠⚠ IF THE VOCABULARY GENUINELY MUST CHANGE, that is fine and expected — change it here, then go to the
  // three sites above and add the row, the refusal, or the member. **The failure mode is not editing this
  // test; it is REMOVING it**, because editing forces the question and removing answers it silently.
  [Trait("Acceptance", "AC-SUB-0031")]
  public void Status_and_reason_vocabularies_are_exact()
  {
    Assert.Equal(
      ["Provisioning", "Active", "Suspended", "Archived"],
      Enum.GetNames<TenantStatus>());
    Assert.Equal(
      ["Created", "ProvisioningCompleted", "Administrative", "Security", "Compliance", "Operational", "CustomerClosure", "IssueResolved"],
      Enum.GetNames<TenantStatusChangeReason>());
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0003")]
  [Trait("Acceptance", "AC-TEN-0005")]
  [Trait("Acceptance", "AC-TEN-0007")]
  [Trait("Acceptance", "AC-TEN-0009")]
  [Trait("Scenario", "TS-TEN-0004")]
  // ⚠⚠⚠ TWO CRITERIA ADDED, AND `AC-TEN-0009` IS CARRIED BY A PAIR IN WHICH NEITHER HALF IS SUFFICIENT.
  //
  // *"A `Suspended` Tenant CAN be reactivated; NO OTHER STATUS can use the reactivation operation."* Two
  // claims pointing opposite ways, and they live in different tests:
  //
  //   the positive   `:150-152` here — Suspended, `Reactivate()` succeeds, status returns to Active.
  //   the negative   `Every_unapproved_transition_preserves_state_metadata_and_events` rows
  //                  `(Provisioning, "Reactivate")`, `(Active, "Reactivate")`, `(Archived, "Reactivate")`.
  //
  // **NEITHER SITE ALONE CARRIES THE CRITERION AND EACH LOOKS AS THOUGH IT DOES.** The negative rows read
  // as *no other status can reactivate* while being equally consistent with reactivation being refused
  // ALWAYS; the positive reads as *Suspended can reactivate* while saying nothing about exclusivity. So the
  // trait is on both, and deleting either one leaves a green suite and a half-proved criterion.
  //
  // ⚠ AND THE *NO OTHER STATUS* IS COMPLETE RATHER THAN SAMPLED, FOR THE THIRD TIME IN THIS PACKAGE:
  // `Status_and_reason_vocabularies_are_exact` asserts `Enum.GetNames<TenantStatus>()` EQUALS four names,
  // so three refusals plus one success EXHAUST the statuses. Same rule as `AC-TEN-0008`'s *only for Active*
  // and `AC-TEN-0016`'s *returns exactly*: **an enumeration closes a complement claim exactly when
  // something else pins the size of the set being enumerated.** All three now rest on a pin, though only
  // one of the pins was written for that purpose.
  //
  // ---- `AC-TEN-0007` IS CITED FOR ITS FIRST CLAUSE ONLY, AND THE SECOND IS A THREE-MEMBER SET.
  //
  // *"Suspending an `Active` Tenant MAKES CURRENT AUTHENTICATION ELIGIBILITY FALSE ‖ and blocks subsequent
  // TENANT SELECTION, NEW-SESSION, and REFRESH eligibility decisions."* Clause one is `:144-146` exactly —
  // an Active tenant, `Suspend()`, `Assert.False(tenant.IsAuthenticationEligible)`.
  //
  // Clause two names THREE decisions and a citation would claim all three. The mechanism exists for each,
  // and it is one mechanism rather than three — every route reaches `Tenant.IsAuthenticationEligible`
  // (`Tenant.cs:65`, `Status == Active`) through `IdentityTenantMembershipReadService:81`, which packages it
  // as `IdentityTenantMembershipEligibility.IsTenantEligible`:
  //
  //   tenant selection   `SelectTenantCommandHandler:52-60`, refuses on `IsEligible`
  //   new-session        `BeginTenantAccessCommandHandler:49-59`, refuses on `IsEligible` before the
  //                      session is created
  //   refresh            `RefreshAuthenticationSessionCommandHandler:44-48` and `:77-80`, REVOKES with
  //                      `AuthenticationSessionRevocationReason.TenantIneligible`
  //
  // (`IsEligible => Membership is not null && IsTenantEligible`.) **WHAT I HAVE ESTABLISHED IS THAT THE
  // PRODUCT DOES THIS, NOT THAT ANY TEST ASSERTS IT** — those three handlers are AUTH-package subjects and
  // I have not yet looked for their tests. Clause two is therefore UNCITED here rather than uncovered, and
  // the distinction matters because the work to close it is a search, not a build.
  //
  // ⚠⚠ A NAME SEARCH CANNOT MEASURE THIS AND NEARLY MISLED ME. `IsAuthenticationEligible` is declared on TWO
  // aggregates — `Tenant.cs:65` and `AuthenticationAccount.cs:77` — and MOST hits in `Authentication/` are
  // the ACCOUNT one, which has nothing to do with tenant status. Counting hits would have reported roughly
  // a dozen tenant-status consumers where there are three. **The population had to be found by following
  // `ITenantAuthenticationEligibilityReadService`, the mechanism, rather than the property name.**
  public void Every_approved_transition_updates_trusted_metadata_and_raises_safe_event()
  {
    var tenant = CreateTenant();
    tenant.ClearDomainEvents();

    Assert.True(tenant.Activate("actor-1", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
    Assert.Equal(TenantStatus.Active, tenant.Status);
    Assert.True(tenant.IsAuthenticationEligible);
    Assert.IsType<TenantActivated>(Assert.Single(tenant.DomainEvents));
    tenant.ClearDomainEvents();

    Assert.True(tenant.Suspend(TenantStatusChangeReason.Security, "actor-2", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
    Assert.Equal(TenantStatus.Suspended, tenant.Status);
    Assert.False(tenant.IsAuthenticationEligible);
    Assert.IsType<TenantSuspended>(Assert.Single(tenant.DomainEvents));
    tenant.ClearDomainEvents();

    Assert.True(tenant.Reactivate(TenantStatusChangeReason.IssueResolved, "actor-3", Guid.NewGuid(), Now.AddMinutes(3)).IsSuccess);
    Assert.Equal(TenantStatus.Active, tenant.Status);
    Assert.IsType<TenantReactivated>(Assert.Single(tenant.DomainEvents));
    tenant.ClearDomainEvents();

    Assert.True(tenant.Archive(TenantStatusChangeReason.CustomerClosure, "actor-4", Guid.NewGuid(), Now.AddMinutes(4)).IsSuccess);
    var archived = Assert.IsType<TenantArchived>(Assert.Single(tenant.DomainEvents));
    Assert.Equal(TenantStatus.Archived, tenant.Status);
    Assert.Equal("actor-4", tenant.ModifiedBy);
    Assert.Equal(Now.AddMinutes(4), tenant.ModifiedUtc);
    Assert.Equal(TenantStatusChangeReason.CustomerClosure, archived.StatusChangeReason);
  }

  [Theory]
  [InlineData(TenantStatus.Provisioning)]
  [InlineData(TenantStatus.Active)]
  [InlineData(TenantStatus.Suspended)]
  [Trait("Acceptance", "AC-TEN-0010")]
  public void Archive_is_allowed_from_every_nonterminal_status(TenantStatus status)
  {
    var tenant = CreateInStatus(status);

    Assert.True(tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(TenantStatus.Archived, tenant.Status);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0004")]
  [Trait("Acceptance", "AC-TEN-0006")]
  [Trait("Scenario", "TS-TEN-0005")]
  public void Invalid_repeated_and_archived_transitions_change_nothing()
  {
    var tenant = CreateTenant();
    Assert.True(tenant.Suspend(TenantStatusChangeReason.Security, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Activate("actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(tenant.Activate("actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsSuccess);
    tenant.ClearDomainEvents();

    Assert.True(tenant.Reactivate(TenantStatusChangeReason.IssueResolved, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.Equal(TenantStatus.Archived, tenant.Status);
    Assert.Empty(tenant.DomainEvents);
  }

  [Theory]
  [InlineData(TenantStatus.Provisioning, "Suspend")]
  [InlineData(TenantStatus.Provisioning, "Reactivate")]
  [InlineData(TenantStatus.Active, "Activate")]
  [InlineData(TenantStatus.Active, "Reactivate")]
  [InlineData(TenantStatus.Suspended, "Activate")]
  [InlineData(TenantStatus.Suspended, "Suspend")]
  [InlineData(TenantStatus.Archived, "Activate")]
  [InlineData(TenantStatus.Archived, "Suspend")]
  [InlineData(TenantStatus.Archived, "Reactivate")]
  [InlineData(TenantStatus.Archived, "Archive")]
  [Trait("BusinessRule", "BRULE-TEN-0003")]
  [Trait("Acceptance", "AC-TEN-0006")]
  [Trait("Acceptance", "AC-TEN-0009")]
  [Trait("Scenario", "TS-TEN-0005")]
  // ⚠⚠⚠ `AC-TEN-0009`'s NEGATIVE HALF IS HERE AND THE POSITIVE HALF IS NOT — SEE `Every_approved_
  // transition_updates_trusted_metadata_and_raises_safe_event`, WHICH CARRIES THE SAME TRAIT FOR THE OTHER
  // HALF. *"A Suspended Tenant CAN be reactivated; NO OTHER STATUS can use the reactivation operation."*
  //
  // The three `Reactivate` rows below — `Provisioning`, `Active`, `Archived` — are the *no other status*
  // half, and with `Status_and_reason_vocabularies_are_exact` pinning `TenantStatus` at four names they
  // EXHAUST the complement rather than sampling it.
  //
  // **BUT THEY ARE EQUALLY CONSISTENT WITH REACTIVATION BEING REFUSED FROM EVERY STATUS**, which would
  // satisfy every row here and break the criterion. The positive site is not a nicety; it is what makes
  // these rows mean *no OTHER status* rather than *no status*. ⚠ **A PAIR IN WHICH EACH HALF LOOKS
  // SUFFICIENT IS THE ONE THAT LOSES A HALF QUIETLY** — whoever deletes the other test gets a green suite
  // and a trait still sitting here, pointing at rows that no longer say what the id claims.
  public void Every_unapproved_transition_preserves_state_metadata_and_events(TenantStatus status, string operation)
  {
    var tenant = CreateInStatus(status);
    var previousModifiedUtc = tenant.ModifiedUtc;
    var previousModifiedBy = tenant.ModifiedBy;
    var previousChangedUtc = tenant.StatusChangedUtc;
    var previousChangedBy = tenant.StatusChangedBy;
    var previousReason = tenant.StatusChangeReasonCode;

    var result = operation switch
    {
      "Activate" => tenant.Activate("actor", Guid.NewGuid(), Now.AddHours(1)),
      "Suspend" => tenant.Suspend(TenantStatusChangeReason.Security, "actor", Guid.NewGuid(), Now.AddHours(1)),
      "Reactivate" => tenant.Reactivate(TenantStatusChangeReason.IssueResolved, "actor", Guid.NewGuid(), Now.AddHours(1)),
      "Archive" => tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now.AddHours(1)),
      _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown lifecycle operation.")
    };

    Assert.True(result.IsFailure);
    Assert.Equal(status, tenant.Status);
    Assert.Equal(previousModifiedUtc, tenant.ModifiedUtc);
    Assert.Equal(previousModifiedBy, tenant.ModifiedBy);
    Assert.Equal(previousChangedUtc, tenant.StatusChangedUtc);
    Assert.Equal(previousChangedBy, tenant.StatusChangedBy);
    Assert.Equal(previousReason, tenant.StatusChangeReasonCode);
    Assert.Empty(tenant.DomainEvents);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0009")]
  [Trait("Scenario", "TS-TEN-0007")]
  public void Created_reason_is_rejected_for_transitions_and_reactivation_uses_bounded_resolution_reasons()
  {
    var tenant = CreateInStatus(TenantStatus.Active);
    Assert.True(tenant.Suspend(TenantStatusChangeReason.Created, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Suspend(TenantStatusChangeReason.ProvisioningCompleted, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Suspend((TenantStatusChangeReason)999, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Suspend(TenantStatusChangeReason.Security, "actor", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(tenant.Reactivate(TenantStatusChangeReason.CustomerClosure, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Reactivate(TenantStatusChangeReason.ProvisioningCompleted, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(tenant.Reactivate(TenantStatusChangeReason.Operational, "actor", Guid.NewGuid(), Now).IsSuccess);

    var archive = CreateTenant();
    Assert.True(archive.Archive(TenantStatusChangeReason.Created, "actor", Guid.NewGuid(), Now).IsFailure);
    Assert.True(archive.Archive((TenantStatusChangeReason)999, "actor", Guid.NewGuid(), Now).IsFailure);
  }

  [Fact]
  [Trait("BusinessRule", "BRULE-TEN-0012")]
  [Trait("Scenario", "TS-TEN-0007")]
  public void Invalid_actor_is_rejected_without_mutation_or_event()
  {
    var tenant = CreateTenant();
    tenant.ClearDomainEvents();

    Assert.True(tenant.Activate("   ", Guid.NewGuid(), Now.AddMinutes(1)).IsFailure);
    Assert.True(tenant.Activate(new string('A', Tenant.ActorMaximumLength + 1), Guid.NewGuid(), Now.AddMinutes(1)).IsFailure);
    Assert.Equal(TenantStatus.Provisioning, tenant.Status);
    Assert.Null(tenant.ModifiedUtc);
    Assert.Empty(tenant.DomainEvents);
  }

  // ⚠ CITES `AC-TEN-0011`'s *DOMAIN OPERATION* SITE — *"No DOMAIN OPERATION, command, repository method,
  // API contract, or migration cascade physically deletes a Tenant."* `:278` bans the name over
  // `typeof(Tenant).GetMethods()`.
  //
  // ⚠⚠ AND IT IS THE STRONGER OF THE TWO SITES THAT CARRY THAT CLAUSE. `TenantLifecycleArchitectureTests.
  // Tenant_repository_and_source_expose_no_generic_query_or_physical_delete_boundary` reaches the same
  // clause by SOURCE TEXT — it greps files importing the Tenants namespaces. **This reads the COMPILED
  // TYPE**, so it cannot be defeated by a namespace alias, a partial class, or a file the path filter
  // misses. Neither subsumes the other: that one covers commands and call sites this cannot see, and this
  // covers a method this one's file filter would skip. Recorded at both ends.
  //
  // ⚠⚠⚠ ALSO CITES `AC-TEN-0018`'s TENANT-FILTER CLAUSE — *"…Tenant itself HAS NO TENANT QUERY FILTER,
  // while existing tenant-owned entities retain their isolation filters."* `:279` asserts `Tenant` does not
  // implement `ITenantOwnedEntity`, **and that interface is not a label — it is the SELECTOR**:
  // `PersistenceDbContext.ConfigureTenantFilter<TEntity>` applies the global filter to every
  // `ITenantOwnedEntity`. So not implementing it IS not having the filter, and this assertion is the
  // mechanism rather than a proxy for it.
  //
  // ⚠ THE REST OF `0018` IS NOT HERE and is a different kind of claim: *uses the existing Platform context,
  // schema, connection, migration history and Unit of Work*, and *existing tenant-owned entities RETAIN
  // their filters*. The second half is the anti-vacuity twin of this line — a change that dropped the
  // filter for everyone would satisfy `:279` perfectly — and it lives in the persistence guards, not here.
  [Fact]
  [Trait("Security", "SEC-TEN-0206")]
  [Trait("Scenario", "TS-TEN-0031")]
  [Trait("Acceptance", "AC-TEN-0011")]
  [Trait("Acceptance", "AC-TEN-0018")]
  public void Tenant_exposes_no_delete_or_tenant_owned_behavior()
  {
    var methods = typeof(Tenant).GetMethods().Select(method => method.Name).ToArray();

    Assert.DoesNotContain(methods, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(typeof(SSAS.BuildingBlocks.Domain.ITenantOwnedEntity), typeof(Tenant).GetInterfaces());
    // ⚠ `UpdateName` STAYS A STRING: a value search finds it NOWHERE in `src/`, so it names a method no
    // type declares and no witness can exist. The residual is real — a wrong word there is caught by
    // nothing. `Rename` DOES exist, on `Branch` in this same context and with the same kind (a
    // `Result`-returning rename on an aggregate), so it is bound.
    Assert.Null(typeof(Tenant).GetMethod("UpdateName"));
    Assert.Null(typeof(Tenant).GetMethod(nameof(SSAS.Platform.Domain.Branches.Branch.Rename)));
  }

  private static Tenant CreateTenant(string code = "ACME", string name = "Acme Trading")
  {
    return Tenant.Create(
      TenantCode.Create(code).Value,
      TenantName.Create(name).Value,
      "platform-actor",
      Guid.NewGuid(),
      Now).Value;
  }

  private static Tenant CreateInStatus(TenantStatus status)
  {
    var tenant = CreateTenant();
    if (status is TenantStatus.Active or TenantStatus.Suspended)
    {
      Assert.True(tenant.Activate("actor", Guid.NewGuid(), Now).IsSuccess);
    }

    if (status == TenantStatus.Suspended)
    {
      Assert.True(tenant.Suspend(TenantStatusChangeReason.Security, "actor", Guid.NewGuid(), Now).IsSuccess);
    }

    if (status == TenantStatus.Archived)
    {
      Assert.True(tenant.Archive(TenantStatusChangeReason.Administrative, "actor", Guid.NewGuid(), Now).IsSuccess);
    }

    tenant.ClearDomainEvents();
    return tenant;
  }
}
