using SSAS.BuildingBlocks.Api.Transport;
using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Infrastructure.Localization;

namespace SSAS.Architecture.Tests;

public sealed class LocalizationArchitectureTests
{
  // ⚠⚠ EXAMINED FOR `AC-LOC-0022` AND DELIBERATELY NOT CITED, WITH THE REASON, SO NOBODY RE-DERIVES IT.
  //
  // The criterion reads *"DOMAIN/APPLICATION remain provider/framework-neutral and bounded."* **This test's
  // subject is `typeof(ResourceKey).Assembly` — `SSAS.BuildingBlocks.Localization`, a THIRD assembly the
  // criterion does not name.** Its bans are also a different set (`SSAS.Platform` is forbidden here and is
  // the Domain's own prefix), which is what a shared-kernel rule looks like rather than a layering one.
  //
  // Citing `AC-LOC-0022` here would be well-formed, resolvable and WRONG — a real criterion this test does
  // not satisfy — and it would inflate the count toward the clause it does not cover. Recorded as
  // EXAMINED-BUT-UNRESOLVED rather than uncovered: a criterion for the building-blocks boundary may exist
  // and was not searched for.
  [Fact]
  public void Localization_building_blocks_has_no_platform_persistence_http_or_cache_dependency()
  {
    // ⚠⚠ FIVE BANNED PREFIXES, TWO KINDS, AND THE SPLIT IS STRUCTURAL (272).
    //
    // DECLARABLE: this repository really does declare each of these somewhere, so DECLARED is the stronger
    // reading — it catches the capability when the `.csproj` merges, which the emitted read cannot see
    // until a type is first used.
    var declarable = new[] { "SSAS.Platform", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore" };

    // ⚠⚠⚠ TRANSITIVE OR FRAMEWORK ONLY: `Microsoft.Data.SqlClient` arrives through
    // `EntityFrameworkCore.SqlServer`, and `Microsoft.Extensions.Caching` through the framework — neither
    // appears in any of this repository's eleven `PackageReference` lines. **A declared check on either
    // would pass vacuously.** Emitted is the correct instrument for these two branches, deliberately.
    var transitiveOnly = new[] { "Microsoft.Data.SqlClient", "Microsoft.Extensions.Caching" };

    var forbidden = declarable.Concat(transitiveOnly).ToArray();

    // One exercise per declarable branch, DERIVED FROM `declarable` rather than restated beside it (278):
    // hardcoded control terms cannot notice a term ADDED to the ban.
    //
    // ⚠⚠ AND THIS METHOD DELIBERATELY DOES **NOT** ROUTE THROUGH `ForbiddenDeclarations`, THOUGH THE HELPER
    // IS RIGHT THERE IN THIS FILE. That helper matches by `Contains`, because the OTHER two methods here
    // ban bare segments. These three terms are FULLY-QUALIFIED PREFIXES matched by `StartsWith`, and
    // routing them through it would WIDEN the rule — `Microsoft.AspNetCore` would start matching anything
    // containing it. **That is a change to what is banned, not to how it is measured**, and coupling the
    // control to the instrument is not worth silently altering the ban to get it.
    var witnessOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["SSAS.Platform"] = "SSAS.Host.API",
      ["Microsoft.AspNetCore"] = "SSAS.Host.API",
      ["Microsoft.EntityFrameworkCore"] = "SSAS.BuildingBlocks.Infrastructure"
    };

    Assert.All(declarable, term =>
    {
      Assert.True(witnessOf.TryGetValue(term, out var witness),
        $"'{term}' is banned but no project is named as its declared witness. Add one, or move the term " +
        "to `transitiveOnly` with grounds — an unwitnessed term bans nothing and reads as coverage.");
      Assert.Contains(
        DeclaredDependencies.Of(witness!), name => name.StartsWith(term, StringComparison.Ordinal));
    });

    var violations = typeof(ResourceKey).Assembly.GetReferencedAssemblies()
      .Where(reference => forbidden.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
      .Select(reference => reference.Name)
      .ToArray();

    // The control the two transitive branches depend on, no declared witness being possible for them.
    Assert.NotEmpty(typeof(ResourceKey).Assembly.GetReferencedAssemblies());

    Assert.Empty(violations);

    Assert.DoesNotContain(
      DeclaredDependencies.Of(typeof(ResourceKey).Assembly),
      name => declarable.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
  }

  // ================================================================================================
  // ⚠⚠⚠ THIS FILE MATCHES BY `Contains`, AND THAT IS LOAD-BEARING (275)
  // ================================================================================================
  //
  // Every other converted guard in this repository bans a FULLY-QUALIFIED PREFIX and matches with
  // `StartsWith`. **These terms are bare SEGMENTS.** Every assembly here is prefixed `SSAS.` or
  // `Microsoft.`, so `Infrastructure`, `EntityFrameworkCore`, `AspNetCore` and `Data.SqlClient` can only
  // ever match in the MIDDLE of a name.
  //
  // ⚠ SO A `StartsWith` READ WOULD NOT BAN A DIFFERENT SET — IT WOULD BAN THE EMPTY SET. All four
  // predicates would match nothing and every `Assert.Empty` would pass on any input. **Do not "restore
  // consistency" with the rest of the sweep here: it would silently empty all four predicates while the
  // emitted half kept supplying a passing result, so the dead assertion would read as a second opinion.**
  //
  // Tightening the terms to prefixes is a decision about the RULE, not the instrument, and is deliberately
  // not folded in — `Contains("Infrastructure")` would also match a hypothetical `InfrastructureSupport`,
  // which nobody has hit. Recorded, not changed.
  //
  // ---- DECLARED AND EMITTED, WITH ONE BRANCH THAT CANNOT BE DECLARED.
  //
  // `Data.SqlClient` reaches this tree TRANSITIVELY through `EntityFrameworkCore.SqlServer` and appears in
  // no project file — so a declared read on it would pass vacuously and emitted is the correct instrument
  // for that branch alone (`272` category 3). The other three are declarable and each has a real witness.
  // ⚠ CITES THE FIRST CLAUSE OF `AC-LOC-0022` — *"Domain/Application remain PROVIDER/FRAMEWORK-NEUTRAL and
  // bounded."* This is the neutrality half, and its subject is exactly the criterion's: the assemblies of
  // `TenantLocalizationOverride` (Domain) and `LocalizationTextResolver` (Application), declared and
  // emitted. The boundedness half is
  // `Localization_application_boundaries_expose_neither_queryables_nor_persistence_types`.
  [Fact]
  [Trait("Criterion", "AC-LOC-0022")]
  public void Platform_localization_domain_and_application_respect_layer_boundaries()
  {
    var declarable = new[] { "Infrastructure", "EntityFrameworkCore", "AspNetCore" };
    var transitiveOnly = new[] { "Data.SqlClient" };
    var forbidden = declarable.Concat(transitiveOnly).ToArray();

    var domain = typeof(TenantLocalizationOverride).Assembly;
    var application = typeof(LocalizationTextResolver).Assembly;

    // One exercise per declarable term, each proving the term finds a real declaration THROUGH THE HELPER
    // THE BANS BELOW USE — not through an inline copy of its predicate, which would not witness the
    // `Contains`/`StartsWith` swap the header warns about.
    //
    // ⚠ `AspNetCore`'s witnesses are `FrameworkReference` elements, which the helper could not read until
    // `ba94176` — this control would have failed before that fix, which is how the hole was found.
    // ⚠ AND DERIVED FROM `declarable` (278). These three were written a term at a time, which meant a
    // FOURTH declarable term would have been witnessed by nothing while all three stayed green.
    var witnessOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["Infrastructure"] = "SSAS.Host.API",
      ["AspNetCore"] = "SSAS.Host.API",
      ["EntityFrameworkCore"] = "SSAS.BuildingBlocks.Infrastructure"
    };

    Assert.All(declarable, term =>
    {
      Assert.True(witnessOf.TryGetValue(term, out var witness),
        $"'{term}' is banned but no project is named as its declared witness. Add one, or move the term " +
        "to `transitiveOnly` with grounds — an unwitnessed term bans nothing and reads as coverage.");
      Assert.NotEmpty(ForbiddenDeclarations(witness!, [term]));
    });

    // ⚠⚠ AND A TERM CONTROL IS NOT AN INPUT CONTROL. The exercises above prove the predicate can match
    // SOMEWHERE — against `Host.API`, an assembly this test does not examine. If either assembly below had
    // an unreadable project file the ban would hold over an empty set and all three term controls would
    // still pass. These two legs are the inputs actually being judged.
    Assert.NotEmpty(DeclaredDependencies.Of(domain));
    Assert.NotEmpty(DeclaredDependencies.Of(application));

    Assert.Empty(ForbiddenReferences(domain, forbidden));
    Assert.Empty(ForbiddenReferences(application, forbidden));

    Assert.Empty(ForbiddenDeclarations(domain, declarable));
    Assert.Empty(ForbiddenDeclarations(application, declarable));
  }

  // ⚠ CITES THE SECOND CLAUSE OF `AC-LOC-0022` — *"Domain/Application remain provider/framework-neutral
  // and BOUNDED."* The neutrality half is
  // `Platform_localization_domain_and_application_respect_layer_boundaries`; this is the boundedness half,
  // over the nine Application boundary interfaces. The two split the criterion and neither covers it alone.
  //
  // ⚠⚠ AND THE FLOOR BELOW WAS ADDED WITH THIS CITATION, NOT BEFORE IT. Four `DoesNotContain` bans over a
  // DERIVED signature list all pass over an EMPTY one, and `GetMethods()` returning nothing — an interface
  // restructured, a type renamed out of the array — reads exactly like compliance. **`:258` in this same
  // file already carries that floor with the reason written out; this one did not.**
  //
  // ⚠⚠⚠ IT IS PART OF THE CITATION RATHER THAN A SEPARATE TIDY-UP: attaching a criterion id to a test that
  // can pass over an empty population publishes it as *covered* while nothing is being read. A citation is
  // a claim, so the anti-vacuity control is what makes the claim true.
  //
  // ⚠ MEASURED BOTH WAYS, 2026-09-03, by forcing the walk empty with a `.Take(0)`:
  //
  //   empty walk, floor REMOVED   → PASSED. All four bans vacuous, exactly as the header says.
  //   empty walk, floor PRESENT   → FAILED, `Assert.NotEmpty() Failure: Collection was empty`.
  //
  // So the floor is the whole difference, and it has now been observed to fail for the reason it claims.
  [Fact]
  [Trait("Criterion", "AC-LOC-0022")]
  public void Localization_application_boundaries_expose_neither_queryables_nor_persistence_types()
  {
    var boundaryTypes = new[]
    {
      typeof(ITenantLocalizationSettingsRepository),
      typeof(ITenantLocalizationOverrideRepository),
      typeof(ITenantLocalizationOverrideReadService),
      typeof(ITenantLocalizationAdministrationReadService),
      typeof(ITenantLocalizationVersionReader),
      typeof(ILocalizationTextResolver),
      typeof(ILocalizationTenantCache),
      typeof(ILocalizationManagementAuditReadiness),
      typeof(IRequestTenantEligibility)
    };
    var signatures = boundaryTypes.SelectMany(type => type.GetMethods())
      .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
      .Select(type => type.ToString())
      .ToArray();

    // THE FLOOR. Without it all four bans below pass over an empty walk — see the header.
    Assert.NotEmpty(signatures);

    Assert.DoesNotContain(signatures, signature => signature.Contains("IQueryable", StringComparison.Ordinal));
    Assert.DoesNotContain(signatures, signature => signature.Contains("DbContext", StringComparison.Ordinal));
    Assert.DoesNotContain(signatures, signature => signature.Contains("DbSet", StringComparison.Ordinal));
    Assert.DoesNotContain(signatures, signature => signature.Contains("EntityEntry", StringComparison.Ordinal));
  }

  // ⚠⚠ EXAMINED FOR A CITATION AND LEFT UNRESOLVED, WITH THE REASON, SO THE SEARCH IS NOT REPEATED BLIND.
  //
  // Two criteria are adjacent and NEITHER MATCHES THE VERB:
  //
  //   `AC-LOC-0018`  *"Strict DTOs REJECT unknown/TenantId fields..."*  — rejection is what the strict
  //                  READER does with an incoming field. This test says the property does not EXIST.
  //   `AC-LOC-0004`  *"Every path DERIVES TenantId from trusted context..."* — derivation is what a
  //                  handler does. This test says the command cannot carry one.
  //
  // **The structural absence is a PRECONDITION for both and identical to neither.** A DTO with no
  // `TenantId` property tells you nothing about whether the reader rejects an unknown field or silently
  // drops it, and nothing about where a handler then gets the tenant from.
  //
  // ⚠ Three adjacent-citation defects have already cost this loop tonight — a real criterion the test does
  // not satisfy is well-formed, resolvable and wrong, and it inflates the count toward the clause left
  // uncovered. Recorded as EXAMINED-BUT-UNRESOLVED, which is a different number from *uncovered*.
  //
  // ⚠ Worth keeping for whoever resolves it: this test's own anti-vacuity is exemplary — the regex is
  // exercised positively AND negatively, and the property walk carries a count floor with a message.
  [Fact]
  public void Localization_commands_never_accept_tenant_or_actor_identity()
  {
    var commands = new[]
    {
      typeof(CreateTenantLocalizationOverrideCommand),
      typeof(UpdateTenantLocalizationOverrideCommand),
      typeof(UndoTenantLocalizationOverrideCommand),
      typeof(RestoreTenantLocalizationDefaultCommand),
      typeof(PreviewTenantLocalizationOverrideCommand),
      typeof(GetTenantLocalizationHistoryQuery),
      typeof(LocalizationResolutionRequest),
      typeof(LocalizationExplicitBatchRequest),
      typeof(LocalizationGroupBatchRequest)
    };

    // ⚠ THE TYPES ARE NAMED BUT THE PROPERTY WALK AND THE REGEX ARE BOTH UNGUARDED. The ban passes if
    // the commands stop exposing properties, and it passes if the pattern stops matching -- and for a ban
    // those are indistinguishable from the answer it wants. So both are exercised.
    const string IdentityName = "TenantId|Actor|UserId";

    Assert.Matches(IdentityName, "TenantId");
    Assert.Matches(IdentityName, "ActorUserId");
    Assert.DoesNotMatch(IdentityName, "Culture");

    var properties = commands.SelectMany(type => type.GetProperties()).ToArray();

    Assert.True(properties.Length >= commands.Length,
      $"{commands.Length} localization commands yielded only {properties.Length} properties; the walk has " +
      "collapsed and this ban would read nothing.");

    Assert.Empty(properties
      .Where(property => Regex.IsMatch(property.Name, IdentityName, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)));
  }

  // ⚠ CITES `AC-LOC-0018`'s SECOND CLAUSE — *"…and bounded projections DISCLOSE NO FOREIGN STATE."*
  //
  // The criterion is one sentence with two DIRECTIONS: what a request may CARRY IN, asserted above and
  // behaviourally at `LocalizationAuditReadinessApiTests`, and what a projection may HAND BACK, which is
  // this. **Only the inbound half was guarded.**
  //
  // ---- ⚠⚠⚠ AND THE TWO BANS ARE NOT MIRROR IMAGES. COPYING THE ONE ABOVE WOULD HAVE BEEN WRONG.
  //
  // The inbound ban is `TenantId|Actor|UserId`. Applied outbound it FAILS ON `LocalizationHistoryEntry.
  // ActorId` — **and that field is required**: `requirements.md:106` says authorized history *"includes
  // LINEAGE"*, and `LocalizationHistoryEntryResponse.ChangedBy` is its transport form. **An actor on a
  // history entry is the tenant's OWN actor, which is not foreign state; a TenantId is.**
  //
  // So the outbound ban is narrower ON PURPOSE, and the difference is the finding rather than an oversight.
  // *Foreign* is doing real work in that sentence and a careless mirror would have deleted a specified
  // feature to satisfy a criterion that never asked for it.
  //
  // ---- THE POPULATION IS DERIVED, AND ITS PREDICATE DOES NOT NAME THE ASSERTION.
  //
  // Every public type in the localization application namespace, not a hand-written list of the ones I
  // happened to think of — so a projection added tomorrow joins the ban by existing. ⚠ **`B20`'s condition
  // is met: the selection predicate is *public type in this namespace*, which says nothing about tenant
  // identifiers**, so a type that carried one could not leave the population by carrying it.
  //
  // ---- THREE CONTROLS, AND THE SECOND IS THE ONE THAT MATTERS.
  //
  //   MEMBERSHIP, not a count: the three projections whose safety is being claimed are asserted to be IN
  //   the walk. A numeric floor would go stale as types are added; this cannot, and it names its subjects.
  //
  //   ⚠⚠ THE MATCHER IS EXERCISED AGAINST THE THREE LEGITIMATE `Tenant*` FIELDS THAT ACTUALLY EXIST —
  //   `TenantLocalizationVersion`, `TenantOverridable`, `CurrentTenantOverride`. **A ban on `Tenant`
  //   rather than on `TenantId` would match all three and redden on correct code**, and nothing but this
  //   control distinguishes *no tenant identifier* from *no field mentioning tenants*.
  //
  //   THE EXEMPTION ASSERTS ITS OWN GROUNDS: `ActorId` is asserted PRESENT on `LocalizationHistoryEntry`.
  //   **If lineage is ever removed, this test reddens** — so the reason the outbound ban is narrower stays
  //   attached to the thing that justifies it, rather than living only in this comment.
  //
  // ⚠⚠⚠ PLANTED, AND THE PLANT MEASURED THE GAP RATHER THAN JUST THIS TEST. A `public Guid TenantId
  // { get; init; }` was added to `LocalizationMutationResult` in `src/` and reverted, `git diff -- src/`
  // clean afterwards. **EXACTLY ONE TEST REDDENED — this one — and the other 3,290 stayed green.**
  //
  // ⚠⚠⚠ CORRECTED WITHIN THE HOUR, AND THE CORRECTION IS THE SAME DEFECT THIS LOOP DIAGNOSED EARLIER
  // TONIGHT: **AN ABSENCE CLAIM CARRIES AN IMPLICIT LAYER AND I STATED MINE WITHOUT ONE.**
  //
  // I first wrote that *nothing else in the repository notices a tenant identifier on a localization
  // projection*. **The plant was on `LocalizationMutationResult`, an APPLICATION type — so the measurement
  // is exact and the sentence was too wide.** `LocalizationTransportContractTests.Administration_read_
  // contracts_do_not_expose_tenant_or_actor_identity_outside_history` has guarded the API RESPONSE
  // CONTRACTS since before tonight, and the plant could not have reddened it because it does not cover
  // application types.
  //
  // **THE TRUE STATEMENT, WITH ITS LAYER: before this test the APPLICATION projections were unguarded, and
  // exactly one test in 3,291 objects to a tenant identifier on one — this one.** The transport half was
  // never the gap.
  //
  // ⚠ THE TWO ARE A PAIR AND NEITHER SUBSUMES THE OTHER: this walks the application namespace, that names
  // three response contracts, and a field can be added at either layer alone. **They also reached the same
  // narrower-than-inbound exemption independently — `ActorId` here, `ChangedBy` there — which is why the
  // pairing is worth recording rather than deduplicating.**
  //
  // ⚠ The plant remains the only reason the ban is known to discriminate at all: every projection satisfies
  // it today, so a green run proves nothing on its own.
  [Fact]
  [Trait("Criterion", "AC-LOC-0018")]
  public void Localization_projections_never_expose_a_tenant_identifier()
  {
    var projections = typeof(LocalizationMutationResult).Assembly.GetTypes()
      .Where(type => type.IsPublic && type.Namespace == "SSAS.Platform.Application.Localization")
      .ToArray();

    // MEMBERSHIP CONTROL. Without it the bans below pass over a namespace that was renamed or emptied.
    Assert.Contains(typeof(LocalizationMutationResult), projections);
    Assert.Contains(typeof(LocalizationHistoryEntry), projections);
    Assert.Contains(typeof(LocalizationAdministrationResource), projections);

    var properties = projections.SelectMany(type => type.GetProperties()).ToArray();
    Assert.NotEmpty(properties);

    // ⚠ THE MATCHER CONTROL. `TenantLocalizationVersion`, `TenantOverridable` and `CurrentTenantOverride`
    // are real properties on real projections and every one of them is legitimate.
    const string TenantIdentifier = "^TenantId$";

    Assert.Matches(TenantIdentifier, "TenantId");
    Assert.DoesNotMatch(TenantIdentifier, "TenantLocalizationVersion");
    Assert.DoesNotMatch(TenantIdentifier, "TenantOverridable");
    Assert.DoesNotMatch(TenantIdentifier, "CurrentTenantOverride");

    Assert.Empty(properties
      .Where(property => Regex.IsMatch(
        property.Name, TenantIdentifier, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)));

    // THE EXEMPTION'S GROUNDS. Lineage is specified, so its absence must fail rather than pass quietly.
    Assert.Contains(typeof(LocalizationHistoryEntry).GetProperties(), property => property.Name == "ActorId");
  }

  // ⚠ CITES ONE CLAUSE OF `AC-LOC-0036` — *"Manage-only Preview validates fully yet WRITES/CACHES/emits/logs
  // nothing and returns encoded text only."* This is the WRITES-AND-CACHES half, and structurally: the
  // handler's assembly carries no Infrastructure, EF or SqlClient dependency, so it has no capability to
  // write or cache at all.
  //
  // ⚠⚠ IT ASSERTS NOTHING about the other three. *Emits nothing* is domain events, *logs nothing* is
  // diagnostics, and *Manage-only* is authorization — a handler with no persistence dependency can still
  // raise an event, write a log line, or be reachable without the permission. Those need behavioural tests
  // and this is a dependency walk.
  [Fact]
  [Trait("Criterion", "AC-LOC-0036")]
  public void Preview_handler_has_no_infrastructure_or_persistence_dependency()
  {
    // `Contains`, not `StartsWith` — see the note on
    // `Platform_localization_domain_and_application_respect_layer_boundaries`. `Data.SqlClient` is
    // transitive-only and stays emitted-only; the other two are declarable and controlled.
    var declarable = new[] { "Infrastructure", "EntityFrameworkCore" };
    var forbidden = declarable.Concat(["Data.SqlClient"]).ToArray();
    var handler = typeof(PreviewTenantLocalizationOverrideCommandHandler).Assembly;

    // Derived from `declarable` (278), for the reason given on the method above.
    var witnessOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["Infrastructure"] = "SSAS.Host.API",
      ["EntityFrameworkCore"] = "SSAS.BuildingBlocks.Infrastructure"
    };

    Assert.All(declarable, term =>
    {
      Assert.True(witnessOf.TryGetValue(term, out var witness),
        $"'{term}' is banned but no project is named as its declared witness. Add one, or move the term " +
        "to `transitiveOnly` with grounds — an unwitnessed term bans nothing and reads as coverage.");
      Assert.NotEmpty(ForbiddenDeclarations(witness!, [term]));
    });

    // The input leg: this method examines one assembly and the term controls above exercised another.
    Assert.NotEmpty(DeclaredDependencies.Of(handler));

    Assert.Empty(ForbiddenReferences(handler, forbidden));
    Assert.Empty(ForbiddenDeclarations(handler, declarable));
  }

  // ⚠ CITES ONE CLAUSE OF THREE. `AC-LOC-0021` reads *"Events exclude full text; dispatch metadata supplies
  // context; audit projector reads committed versions."* **This test satisfies the FIRST clause only** — it
  // walks the four domain events and bans text, placeholder and transport-shaped property names.
  //
  // IT ASSERTS NOTHING about dispatch metadata supplying the context those events omit, and nothing about
  // the audit projector reading committed versions. Both are behavioural and live elsewhere; naming the
  // criterion without naming the clause would credit this with all three.
  [Fact]
  [Trait("Criterion", "AC-LOC-0021")]
  public void Localization_domain_events_contain_no_text_placeholder_or_transport_data()
  {
    var events = typeof(TenantLocalizationOverride).Assembly.GetTypes()
      .Where(type => typeof(DomainEvent).IsAssignableFrom(type))
      .Where(type => type.Namespace == "SSAS.Platform.Domain.Localization.Events")
      .ToArray();
    var violations = events.SelectMany(type => type.GetProperties()
      .Where(property => Regex.IsMatch(
        property.Name,
        "Value|Text|Placeholder|Http|Claim|Credential|Secret|Password|Token|Request|Trace",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
      .Select(property => $"{type.Name}.{property.Name}"))
      .ToArray();

    Assert.Equal(4, events.Length);
    Assert.Empty(violations);
  }

  // ⚠ CITES ONE CLAUSE OF THREE, AND ONLY ITS STRUCTURAL HALF. `AC-LOC-0011` reads *"Each mutation appends
  // one UNMODIFIABLE, uniquely numbered version and atomically advances current/settings versions."*
  //
  // **This test satisfies *unmodifiable*** — `TenantLocalizationOverrideVersion` exposes no public setter
  // and no public mutating method.
  //
  // ⚠⚠ IT DOES NOT ASSERT THAT A MUTATION APPENDS ONE, that numbering is unique, or that the advance is
  // atomic. Those are behavioural claims about the write path; this is a claim about the TYPE. A version
  // that was never appended would satisfy every assertion here.
  [Fact]
  [Trait("Criterion", "AC-LOC-0011")]
  public void Localization_history_has_no_public_mutation_or_setter_api()
  {
    var type = typeof(TenantLocalizationOverrideVersion);

    // ⚠ BOTH BANS BELOW ARE OVER FILTERED REFLECTION WALKS, AND BOTH PASS OVER AN EMPTY ONE. The type
    // is named, so it cannot go missing silently -- but `GetProperties()` and the `DeclaredOnly` method
    // walk can both come back empty from a record that was restructured, and then neither ban reads a
    // single member.
    Assert.NotEmpty(type.GetProperties());

    Assert.NotEmpty(type.GetMethods(
      BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));

    Assert.Empty(type.GetProperties().Where(property => property.SetMethod?.IsPublic == true));
    Assert.Empty(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
      .Where(method => Regex.IsMatch(method.Name, "^(Set|Update|Delete|Remove|Restore|Undo)", RegexOptions.CultureInvariant)));
  }

  // ⚠⚠ EXAMINED AND LEFT UNRESOLVED ON A MILESTONE MISMATCH, WITH THE SET ENUMERATED THIS TIME.
  //
  // The two criteria that speak to milestone containment are `AC-LOC-0023` (*"M1 generates neutral client
  // JSON only; no Angular runtime/library/screen is introduced"*) and `AC-LOC-0024` (*"M1 contains only
  // approved backend core/migration/tests; HTTP/OpenAPI stay M2"*). **Both are scoped to M1. This test
  // asserts PHASE FOUR's boundaries**, and citing an M1 criterion for a phase-four containment rule would
  // be wrong on the milestone even where the file lists overlap.
  //
  // ⚠ THE DISPOSAL IS ONLY WORTH THIS MUCH BECAUSE THE SET WAS ENUMERATED: `AC-LOC-0001`–`0064`, all read.
  // Earlier disposals in this file were made having read two thirds, which made *no criterion matches* an
  // absence claim over a population I had not counted. Nothing here is scoped to phase four.
  [Fact]
  public void Localization_phase_four_adds_only_approved_server_boundaries_and_no_ui_redis_audit_store_or_mutable_default_catalog()
  {
    var root = FindRepositoryRoot();
    var platformApi = SourceFiles(Path.Combine(root, "src", "Platform", "SSAS.Platform.API")).ToArray();
    var host = SourceFiles(Path.Combine(root, "src", "Host")).ToArray();
    var allowedApiFiles = new HashSet<string>(StringComparer.Ordinal)
    {
      "LocalizationRowVersionCodec.cs",
      "LocalizationTransportContracts.cs",
      "LocalizationApiErrorMapper.cs",
      "LocalizationEndpointRouteBuilderExtensions.cs",
      "LocalizationResponseSecurity.cs"
    };
    Assert.Empty(platformApi.Where(path => File.ReadAllText(path).Contains("Localization", StringComparison.Ordinal))
      .Where(path => !allowedApiFiles.Contains(Path.GetFileName(path))));
    var allowedHostFiles = new HashSet<string>(StringComparer.Ordinal)
    {
      "Program.cs",
      "HostServiceCollectionExtensions.cs",
      "LocalizationOpenApiOperationFilter.cs"
    };
    Assert.Empty(host.Where(path => File.ReadAllText(path).Contains("Localization", StringComparison.Ordinal))
      .Where(path => !allowedHostFiles.Contains(Path.GetFileName(path))));

    var uiRoot = Path.Combine(root, "src", "UI");
    if (Directory.Exists(uiRoot))
    {
      Assert.Empty(Directory.EnumerateFiles(uiRoot, "*localization*", SearchOption.AllDirectories));
    }

    var productionFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
      .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".csproj", StringComparison.Ordinal))
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .ToArray();

    // ⚠ THE ONLY WALK IN THIS FILE THAT CAN COLLAPSE QUIETLY, AND IT GUARDS THE REDIS BAN (T-248).
    //
    // Most checks here are reflection over named types, and the two walks above root at directories that
    // THROW if they vanish. **This one roots at `src`, which always exists, and narrows by extension** —
    // so a changed suffix filter returns an empty array from a healthy directory and "no Redis anywhere in
    // production" passes having read no files.
    Assert.True(productionFiles.Length >= 400,
      $"only {productionFiles.Length} production files were scanned; the extension filter has stopped " +
      "matching and the Redis ban below would pass without reading anything.");

    Assert.Empty(productionFiles.Where(path => Regex.IsMatch(
      File.ReadAllText(path),
      "StackExchange\\.Redis|IDistributedCache|AddStackExchangeRedisCache",
      RegexOptions.CultureInvariant)));

    var migration = Directory.EnumerateFiles(
        Path.Combine(root, "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations"),
        "*AddLocalizationCore.cs")
      .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));
    var migrationSource = File.ReadAllText(migration);
    Assert.DoesNotContain("LocalizationResources", migrationSource, StringComparison.Ordinal);
    Assert.DoesNotContain("LocalizationDefault", migrationSource, StringComparison.Ordinal);
    Assert.DoesNotContain("Audit", migrationSource, StringComparison.Ordinal);
    Assert.Equal(4, Regex.Matches(migrationSource, "migrationBuilder.CreateTable", RegexOptions.CultureInvariant).Count);
  }

  // ⚠ EXAMINED AND LEFT UNRESOLVED. `AC-LOC-0064` is the only criterion about audit readiness and its
  // subject is RUNTIME BEHAVIOUR — a mutation proceeds only when readiness succeeds, else 503. **This test
  // asserts WHERE THE TYPE LIVES**: interface in Application, implementation in Infrastructure, nothing
  // named `AuditReadiness` in the Domain assembly. Layering, not behaviour, and no criterion in the
  // enumerated set `0001`–`0064` states a layering rule for it.
  //
  // The nearest is `AC-LOC-0022`'s *bounded*, but that is scoped to Domain/Application generally and is
  // already carried by two other tests here on their own subjects.
  [Fact]
  public void Audit_readiness_is_application_owned_infrastructure_implemented_and_absent_from_domain()
  {
    Assert.Equal("SSAS.Platform.Application", typeof(ILocalizationManagementAuditReadiness).Assembly.GetName().Name);
    Assert.Equal("SSAS.Platform.Infrastructure", typeof(LocalizationManagementAuditReadiness).Assembly.GetName().Name);
    Assert.DoesNotContain(typeof(TenantLocalizationOverride).Assembly.GetTypes(), type =>
      type.Name.Contains("AuditReadiness", StringComparison.Ordinal));
  }

  // ⚠ CITES THE WIRING OF `AC-LOC-0064` — *"An otherwise authorized Production localization mutation
  // PROCEEDS ONLY WHEN AUDIT READINESS SUCCEEDS; otherwise it returns HTTP 503 … with no SQL state change,
  // Domain event, cache eviction, submitted-text logging, or internal-cause disclosure."*
  //
  // **What this asserts is that ALL FOUR mutation handlers call the guard at all** — the structural
  // precondition without which the criterion is unreachable on some path. That is a real and citable part
  // of it, and it is the part a new fifth handler would silently miss.
  //
  // ⚠⚠ WHAT IT DOES NOT ASSERT, WHICH IS MOST OF THE CRITERION: that the mutation is REFUSED when readiness
  // fails, that the status is 503, that the code is `localization.audit_readiness_unavailable`, or any of
  // the five must-not-happens. **And it is a SOURCE-TEXT match** — `Assert.Contains` on the file — so it
  // proves the call is WRITTEN, not that it is REACHED or that its result is honoured.
  //
  // ⚠ The population cannot silently empty: the four handlers are named and `File.ReadAllText` throws on a
  // missing one, so a renamed handler is a red rather than a vacuum.
  [Fact]
  [Trait("Criterion", "AC-LOC-0064")]
  public void Every_localization_mutation_handler_retains_locked_tenant_eligibility_and_audit_readiness()
  {
    var root = FindRepositoryRoot();
    var handlers = new[]
    {
      "CreateTenantLocalizationOverrideCommandHandler.cs",
      "UpdateTenantLocalizationOverrideCommandHandler.cs",
      "UndoTenantLocalizationOverrideCommandHandler.cs",
      "RestoreTenantLocalizationDefaultCommandHandler.cs"
    };

    foreach (var handler in handlers)
    {
      var source = File.ReadAllText(Path.Combine(
        root, "src", "Platform", "SSAS.Platform.Application", "Localization", handler));
      Assert.Contains("GetEligibilityForUpdateAsync", source, StringComparison.Ordinal);
      Assert.Contains("LocalizationManagementAuditGuard.CheckAsync", source, StringComparison.Ordinal);
    }
  }

  // ⚠⚠ EXAMINED AGAINST `AC-LOC-0064` AND LEFT UNRESOLVED — THE SCOPES DO NOT LINE UP, IN BOTH DIRECTIONS.
  //
  // That criterion bans *submitted-text logging* **on the audit-readiness-failure path**, as one of five
  // must-not-happens attached to the 503. This test bans localized and placeholder values **anywhere in
  // `LocalizationDiagnostics.cs`** — broader in reach, narrower in file, and it never establishes that the
  // audit-failure path logs THROUGH that file.
  //
  // **A superset ban in one file does not satisfy a clause scoped to one path**, and citing it would claim
  // the 503's logging guarantee is covered when nothing has shown the two meet. Recorded as
  // EXAMINED-BUT-UNRESOLVED: the criteria compared were `AC-LOC-0064` and `AC-LOC-0005`, and the search
  // that would settle it is *which logger the audit-failure path actually calls*.
  [Fact]
  public void Localization_diagnostics_never_log_localized_or_placeholder_values()
  {
    var path = Path.Combine(
      FindRepositoryRoot(),
      "src", "Platform", "SSAS.Platform.Infrastructure", "Localization", "LocalizationDiagnostics.cs");
    var source = File.ReadAllText(path);

    Assert.DoesNotContain("LocalizedValue", source, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("PlaceholderValue", source, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotMatch("Log(?:Warning|Error|Information).*\\b(Text|Value|Placeholder)\\b", source);
  }

  // ⚠⚠ EXAMINED AND LEFT UNRESOLVED — SEVEN ASSERTIONS, THREE NEAR CRITERIA, NONE A CLAUSE MATCH.
  //
  //   `AC-LOC-0022`  *Domain/Application remain provider/framework-neutral* — the EF bans here are on the
  //                  **API** assembly, which that criterion does not name. Adjacent scope.
  //   `AC-LOC-0005`  *the four-step chain reports exact source/cultures* — about what the chain REPORTS.
  //                  *No second fallback algorithm in HTTP* is a different claim about where it LIVES.
  //   `AC-LOC-0054`  *no input channel can alter current scope* — the closest, and it fits only the LAST
  //                  assertion (`LocalizationTransportContracts.cs` contains no `TenantId`), not the six
  //                  about resolver reuse.
  //
  // **Citing on one of seven assertions would publish the criterion as covered by a test whose subject is
  // something else.** If this is split later, the transport-contract line is the citable half.
  //
  // Set enumerated `0001`–`0064`; the search that would settle it is whether a phase-five HTTP criterion
  // exists outside `acceptance-criteria.md`.
  [Fact]
  public void Phase_five_localization_http_reuses_the_application_resolver_without_ef_or_a_second_fallback_algorithm()
  {
    var root = FindRepositoryRoot();
    var path = Path.Combine(root, "src", "Platform", "SSAS.Platform.API", "Localization", "LocalizationEndpointRouteBuilderExtensions.cs");
    var source = File.ReadAllText(path);

    Assert.Contains("ILocalizationTextResolver", source, StringComparison.Ordinal);
    Assert.Contains("ResolveTemplateGroupAsync", source, StringComparison.Ordinal);
    Assert.Contains("ResolveExplicitBatchAsync", source, StringComparison.Ordinal);
    Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
    Assert.DoesNotContain("DbSet", source, StringComparison.Ordinal);
    Assert.DoesNotContain("EntityFrameworkCore", source, StringComparison.Ordinal);
    Assert.DoesNotContain("TenantId", File.ReadAllText(Path.Combine(root, "src", "Platform", "SSAS.Platform.API", "Localization", "LocalizationTransportContracts.cs")), StringComparison.Ordinal);
  }

  private static IEnumerable<string> ForbiddenReferences(Assembly assembly, IReadOnlyCollection<string> forbidden) =>
    assembly.GetReferencedAssemblies()
      .Where(reference => forbidden.Any(part => reference.Name?.Contains(part, StringComparison.Ordinal) == true))
      .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}");

  // The declared sibling. ⚠ IT MATCHES BY `Contains`, IDENTICALLY TO THE EMITTED ONE ABOVE, AND THAT IS
  // THE WHOLE REASON THIS PAIR IS SAFE. The terms are bare segments and every assembly name is prefixed,
  // so a `StartsWith` variant here would match nothing — and the two halves would then ban DIFFERENT SETS
  // while appearing to assert one rule, with the dead half's greenness supplied by the live one.
  //
  // Callers pass only the DECLARABLE terms: `Data.SqlClient` is transitive-only and would be vacuous here.
  private static IEnumerable<string> ForbiddenDeclarations(
    Assembly assembly, IReadOnlyCollection<string> forbidden) =>
    ForbiddenDeclarations(assembly.GetName().Name!, forbidden);

  // ================================================================================================
  // ⚠⚠⚠ THE TERM CONTROLS CALL THIS OVERLOAD, AND THAT IS THE ONLY REASON IT EXISTS
  // ================================================================================================
  //
  // ***A CONTROL THAT REIMPLEMENTS THE INSTRUMENT CANNOT WITNESS THE INSTRUMENT CHANGING.***
  //
  // The three term exercises first read `DeclaredDependencies.Of(...).Any(n => n.Contains(term))` inline.
  // That exercises `Contains`; it does NOT exercise this helper. **Measured, not argued: with a forbidden
  // `ProjectReference` planted in `SSAS.Platform.Application.csproj` and this predicate switched to
  // `StartsWith`, both bans went green AND ALL THREE TERM CONTROLS STAYED GREEN.** The control would have
  // been the thing that certified the vacuity. Routed through here, the same swap reddens all three —
  // confirmed by re-running the identical plant.
  //
  // ---- ⚠⚠ AND THE GENERAL RULE IS A SYMMETRY, BECAUSE THE SAME WORD WANTS OPPOSITE THINGS.
  //
  //   A CONTROL MUST SHARE ITS INSTRUMENT WITH THE ASSERTION. It has to sit downstream of the same code,
  //   or it cannot detect that code changing. **Independence here is the defect** — and it is invisible,
  //   because an inline copy and a call through the helper are green in exactly the same situations right
  //   up until the helper changes.
  //
  //   A CHECKLIST MUST NOT SHARE ITS POPULATION WITH THE WORK LIST. If the plan enumerating what to do is
  //   also the plan verifying it was done, an omission is invisible BY CONSTRUCTION. **Dependence there is
  //   the defect.**
  //
  // Confusing the two produces precisely these two failures, and neither is derivable from the other.
  private static IEnumerable<string> ForbiddenDeclarations(
    string projectName, IReadOnlyCollection<string> forbidden) =>
    DeclaredDependencies.Of(projectName)
      .Where(name => forbidden.Any(part => name.Contains(part, StringComparison.Ordinal)))
      .Select(name => $"{projectName} DECLARES {name}");

  private static IEnumerable<string> SourceFiles(string directory) =>
    Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
