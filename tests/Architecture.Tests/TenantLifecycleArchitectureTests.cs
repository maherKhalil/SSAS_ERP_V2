using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Tenants;

namespace SSAS.Architecture.Tests;

// ---- PLANT RECORD (T-249): collapsing the file walk to `*.csx` reddens this file.
//
// Checked rather than assumed. `Assert.NotEmpty(files)` is what catches it, and it catches it because
// the count is taken AFTER the pattern filter rather than before.
//
// ---- PLANT RECORD, `No_tenant_lifecycle_entry_point_accepts_a_caller_supplied_status_or_eligibility`:
//      THREE PLANTS, ONE PER ROUTE, EACH RUN AND REVERTED ALONE.
//
//   ROUTE 1, PASS IT IN   `TenantStatus? forcedStatus = null` added to `Tenant.Activate` — an OPTIONAL
//                         parameter, so no call site breaks and the plant measures the guard rather than
//                         the compiler. GATE RED, exactly ONE test of 3,290 failed. `Assert.Empty`, and
//                         the TRX names `TenantLifecycleArchitectureTests.cs:line 339`.
//   ROUTE 2, ASSIGN IT    `Status { get; private set; }` widened to `{ get; set; }`. GATE RED, exactly one
//                         test. `Assert.True() Failure` — a distinct assertion type, so the name alone
//                         settles it. ⚠ **NOTHING ELSE IN 3,290 TESTS OBJECTS TO A LIFECYCLE AGGREGATE'S
//                         STATUS BECOMING PUBLICLY ASSIGNABLE.** That is the measurement, not a remark.
//   ROUTE 3, CARRY IT     `TenantStatus? DesiredStatus` added to `ActivateTenantCommand`. GATE RED,
//                         exactly one test. `Assert.Empty`, TRX line 373.
//
// ⚠⚠⚠ ROUTES 1 AND 3 SHARE AN ASSERTION TYPE, SO THE FAILURE NAME CANNOT SEPARATE THEM AND THE LINE NUMBER
// IS WHAT DOES. Route 1's plant was re-run for that reason alone: the first pass read only `Assert.Empty`,
// which is equally consistent with route 3 having fired. **The subject-disjointness argument — a method
// parameter cannot appear in a walk of command PROPERTIES — is sound and was still not worth relying on**,
// because it is exactly the shape of reasoning that has been wrong repeatedly here. Two same-named
// assertions in one test need a discriminator that is not the name.
//
// ---- PLANT RECORD, `Tenant_read_projections_expose_exactly_their_lifecycle_contract`: TWO PLANTS, RUN AND
//      REVERTED SEPARATELY, BECAUSE THAT TEST HOLDS TWO INDEPENDENT CONTROLS AND ONE RUN CANNOT SEPARATE
//      THEM. A single plant that reddens the test proves the UNION is live and says nothing about which
//      half did the work — and the half that stops discriminating is then invisible behind the half that
//      still does.
//
//   PLANT A, THE ARITY PIN. `public string? Notes { get; init; }` added to `TenantDto` in `src/`.
//   `Notes` matches NO term in the business-data ban, so only the exact-member-set assertion could fire.
//   **GATE RED, exactly ONE test failed across all seven suites — this one — and the other 3,288 stayed
//   green.** Failure was `Assert.Equal() Failure: Collections differ`, read from the TRX rather than
//   inferred: the PIN, not the ban.
//
//   PLANT B, THE TERM BAN. `public string? EmployeeName { get; init; }` added to `GetTenantQuery`, which is
//   in the guarded namespace and deliberately carries NO arity pin — on `TenantDto` the pin would have
//   fired first and masked the result. **GATE RED, exactly ONE test failed — this one.** Failure was
//   `Assert.Empty() Failure: Collection was not empty`: the BAN, not the pin.
//
//   Two plants, two different assertions, each measured alone. `git diff --quiet -- src/` clean after each.
//
// ⚠ WHY THE PLANTS WERE NECESSARY AND A GREEN RUN IS NOT EVIDENCE: every projection in this namespace
// satisfies both controls today, so this test passes whether or not it can discriminate. **That is the
// definition of the vacuity it exists to prevent, and it applies to the guard as much as to the code.**
public sealed class TenantLifecycleArchitectureTests
{
  [Fact]
  [Trait("NonFunctional", "NFR-TEN-0302")]
  [Trait("Scenario", "TS-TEN-0030")]
  public void Tenant_domain_and_application_are_framework_and_module_independent()
  {
    // ⚠⚠ FIVE BANNED PREFIXES, TWO KINDS, AND THE SPLIT IS STRUCTURAL (272). This method is the clearest
    // case in the sweep that the unit is the BRANCH rather than the site: three of these are declarable and
    // two can never be, in one assertion. No site-level classification could describe it.
    //
    // DECLARABLE: DECLARED is the stronger reading — it catches the capability the moment a `.csproj`
    // merges, which is before the emitted read can see anything at all.
    var declarable = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "SSAS.HR", "SSAS.GL" };

    // ⚠⚠⚠ TRANSITIVE ONLY: `Microsoft.Data.SqlClient` arrives through `EntityFrameworkCore.SqlServer` and
    // appears in no `.csproj` of ours, so **a declared check on it would pass vacuously**. Emitted is the
    // correct instrument for this branch — a decision, not an omission.
    var transitiveOnly = new[] { "Microsoft.Data.SqlClient" };

    var forbidden = declarable.Concat(transitiveOnly).ToArray();
    var assemblies = new[] { typeof(Tenant).Assembly, typeof(CreateTenantCommandHandler).Assembly };

    // One exercise per declarable branch — four predicates sharing nothing, so one control would leave
    // three bans holding over prefixes it never matched.
    //
    // ⚠ DERIVED FROM `declarable`, NOT RESTATED BESIDE IT (278). These controls used to hardcode the four
    // terms, which meant a FIFTH term added to the ban above would have been witnessed by nothing and
    // banned nothing — silently, with all four existing controls still green. Adding one now fails at the
    // witness lookup instead. It still restates `StartsWith` inline, and that is deliberate: see the
    // control section in `DeclaredDependencies` for why widening a ban is loud and only narrowing is silent.
    var witnessOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      ["Microsoft.EntityFrameworkCore"] = "SSAS.BuildingBlocks.Infrastructure",
      ["Microsoft.AspNetCore"] = "SSAS.Host.API",
      ["SSAS.HR"] = "SSAS.Host.API",
      ["SSAS.GL"] = "SSAS.Host.API"
    };

    Assert.All(declarable, term =>
    {
      Assert.True(witnessOf.TryGetValue(term, out var witness),
        $"'{term}' is banned but no project is named as its declared witness. Add one, or move the term " +
        "to `transitiveOnly` with grounds — an unwitnessed term bans nothing and reads as coverage.");
      Assert.Contains(
        DeclaredDependencies.Of(witness!), name => name.StartsWith(term, StringComparison.Ordinal));
    });

    var violations = assemblies.SelectMany(assembly => assembly.GetReferencedAssemblies()
      .Where(reference => forbidden.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
      .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}")).ToArray();

    // The control the transitive branch depends on, since no declared witness for it can exist.
    foreach (var assembly in assemblies)
    {
      Assert.NotEmpty(assembly.GetReferencedAssemblies());
    }

    Assert.Empty(violations);

    var declared = assemblies.SelectMany(assembly => DeclaredDependencies.Of(assembly)
      .Where(name => declarable.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
      .Select(name => $"{assembly.GetName().Name} DECLARES {name}")).ToArray();

    Assert.Empty(declared);
  }

  [Fact]
  [Trait("NonFunctional", "NFR-TEN-0301")]
  [Trait("Scenario", "TS-TEN-0018")]
  public void Every_tenant_handler_exposes_async_cancellation_boundary()
  {
    var handlers = new[]
    {
      typeof(CreateTenantCommandHandler),
      typeof(ActivateTenantCommandHandler),
      typeof(SuspendTenantCommandHandler),
      typeof(ReactivateTenantCommandHandler),
      typeof(ArchiveTenantCommandHandler),
      typeof(GetTenantQueryHandler),
      typeof(ListTenantsQueryHandler),
      typeof(GetTenantAuthenticationEligibilityQueryHandler)
    };

    Assert.All(handlers, handler =>
    {
      var method = Assert.Single(handler.GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(candidate => candidate.Name == "HandleAsync"));
      Assert.True(typeof(Task).IsAssignableFrom(method.ReturnType));
      Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
    });
  }

  // ⚠ CITES `AC-TEN-0011` — *"NO Domain operation, command, repository method, API contract, OR MIGRATION
  // CASCADE physically deletes a Tenant."* **Five named sites, and this test reaches three of them:**
  //
  //   repository method   `ITenantRepository` carries no `Delete`/`Remove` and returns no `IQueryable`
  //   command             the source scan bans `DeleteTenantCommand`/`Handler`
  //   Domain operation    the same scan bans `Tenants.Remove(` in every file importing the Tenants
  //                       namespaces
  //
  // ⚠⚠⚠ AND THE FOURTH SITE IS EXCLUDED BY THIS TEST'S OWN FILTER, WHICH IS INVISIBLE UNLESS YOU READ THE
  // WALK. `:119` drops every path under `Migrations`, and *migration cascade* is one of the five things the
  // criterion names. **The exclusion is correct — a migration file legitimately contains `DROP` and
  // `DELETE` for unrelated objects, so scanning them would false-positive — but it means this test cannot
  // speak for the clause however green it is.**
  //
  // THAT HALF IS COVERED, AND ELSEWHERE: `DeleteBehaviourArchitectureTests.Every_reference_foreign_key_
  // still_restricts` asserts every REFERENCE foreign key in the composed model uses `Restrict`, which is
  // the model-level fact a cascade migration would have to be generated from. **Package-agnostic guard,
  // so nothing in either file names the other** — recorded here because a reader auditing `AC-TEN-0011`
  // against this test alone would find four of five and conclude the fifth is unguarded.
  //
  // ⚠⚠ AND THE CASCADE IS NOT MERELY GUARDED, IT IS NOT CONSTRUCTIBLE — MEASURED ON 2026-09-03 RATHER THAN
  // REASONED. Across EVERY migration in `src/`, `ReferentialAction` appears 96 times: **95 `Restrict` and
  // exactly ONE `Cascade`.** That one is `RelaxOwnershipDeleteBehaviour`, and it moves three
  // `SubscriptionPlan` OWNERSHIP keys — limits, modules, prices. **No cascading foreign key anywhere in
  // the schema references `Tenants`**, so there is no path by which deleting a row cascades into a Tenant.
  //
  // ⚠⚠⚠ CORRECTION, SAME SESSION: *NOT CONSTRUCTIBLE* WAS TOO STRONG, AND ASKING **WHY** IT WAS
  // UNCONSTRUCTIBLE IS WHAT BROKE IT. The 95-to-1 count is a fact about the migrations AS THEY STAND, not
  // an impossibility. There are two routes to a cascading Tenant foreign key and they are not alike:
  //
  //   THROUGH THE MODEL — genuinely closed. `Every_reference_foreign_key_still_restricts` walks
  //   `context.Model` and reddens on any non-`Restrict` reference key, so a model change cannot introduce
  //   one quietly.
  //
  //   ⚠ THROUGH A HAND-WRITTEN MIGRATION — OPEN, AND DEMONSTRATED IN THIS REPOSITORY. That guard reads the
  //   MODEL; a hand-written migration changes the DATABASE. `RelaxOwnershipDeleteBehaviour` is exactly such
  //   a migration and its own comment states the mechanism: *"migrations are diffed snapshot-against-model
  //   rather than database-against-model — so no future scaffold will ever notice."*
  //
  // **THE TWO BLIND SPOTS COMPOSE ONTO THE ONE MECHANISM THAT HAS ACTUALLY BEEN USED HERE.** The model
  // guard cannot see a migration; this test excludes `Migrations/` by path. A hand-written migration adding
  // `ON DELETE CASCADE` to a Tenant reference key would leave both green, and the repository has already
  // used that exact route once — deliberately, with reasons, which is what makes it a normal act rather
  // than an exotic one.
  //
  // ⚠⚠ SECOND CORRECTION, AND IT NARROWS THE MIGRATION ROUTE RATHER THAN CLOSING IT. *"Nothing in the
  // suite would report it"* was ANOTHER UNSEARCHED ABSENCE — I had inspected two guards and generalised to
  // all. Searching `delete_referential_action` / `sys.foreign_keys` across `tests/` returns ELEVEN files,
  // and two bear on this directly:
  //
  //   `PlatformTenantLifecycleSqlServerTests:104` and `:243` select every table referencing
  //   `platform.Tenants` and assert the set is EXACTLY SEVEN, BY NAME. **It pins WHICH tables reference
  //   Tenants and never reads HOW they delete.**
  //
  //   `SubscriptionPlanOwnershipCascadeSqlServerTests:208` runs `SELECT delete_referential_action_desc
  //   FROM sys.foreign_keys` as `DeleteRuleAsync`, and `:119` asserts `"CASCADE"` on the three ownership
  //   keys BY NAME. **The database-level observable is not a form to be invented — it is working code,
  //   invoked by name, three tables away.**
  //
  // SO THE ROUTE SPLITS AND ONLY ONE HALF IS OPEN:
  //
  //   a migration ADDING a cascading key to Tenants      CAUGHT — the set of seven changes
  //   a migration ALTERING an existing key's action      NOT CAUGHT — the set is unchanged
  //
  // **AND ALTERING IS THE ROUTE THIS REPOSITORY HAS TAKEN.** `RelaxOwnershipDeleteBehaviour` changed three
  // existing keys and added none.
  //
  // ---- THE DISPOSAL, IN ITS FINAL FORM.
  //
  // **CLOSED through the model · CLOSED for ADD at the database · OPEN for ALTER on Tenant keys**, and the
  // grounds for the open half are a caller-side practice, not a type-level impossibility. What is missing
  // is COVERAGE, not MECHANISM: `DeleteRuleAsync` would answer this question about a Tenant key today if
  // anyone called it with one. That call needs a database, so it is `Integration.Tests` and outside this
  // gate.
  //
  // ⚠ AND THE SHAPE OF WHO WROTE THAT HELPER IS THE ORDINARY CASE, WORTH NAMING: whoever took the
  // hand-written-migration route INSTRUMENTED THEIR OWN CHANGE AND DID NOT GENERALISE IT. **The control
  // exists because someone needed it once, not because anyone decided the class needed guarding** — which
  // is why looking for a neighbouring control is worth doing before calling a route unwatched.
  //
  // ⚠ THE FIFTH — *API contract* — IS NOT COVERED AND CORRECTLY SO: `AC-TEN-0020` defers the tenant
  // endpoints entirely, and `Tenant_endpoints_remain_deferred…` below is what asserts that. **There is no
  // API contract yet to refuse a delete**, so the clause is satisfied by the surface not existing, and it
  // becomes live the day those endpoints ship.
  [Fact]
  [Trait("Decision", "DEC-TEN-0007")]
  [Trait("Scenario", "TS-TEN-0031")]
  [Trait("Acceptance", "AC-TEN-0011")]
  public void Tenant_repository_and_source_expose_no_generic_query_or_physical_delete_boundary()
  {
    Assert.False(typeof(ITenantRepository).IsGenericType);
    Assert.DoesNotContain(typeof(ITenantRepository).GetMethods(), method =>
      Regex.IsMatch(method.Name, "Delete|Remove", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    Assert.DoesNotContain(typeof(ITenantRepository).GetMethods(), method =>
      method.ReturnType.ToString().Contains("IQueryable", StringComparison.Ordinal));

    var tenantSource = PlatformSourceFiles()
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path =>
      {
        var source = File.ReadAllText(path);
        return source.Contains("SSAS.Platform.Domain.Tenants;", StringComparison.Ordinal) ||
          source.Contains("SSAS.Platform.Application.Tenants;", StringComparison.Ordinal);
      })
      .ToArray();
    Assert.Empty(tenantSource.Where(path => Regex.IsMatch(
      File.ReadAllText(path),
      @"\bDeleteTenant(?:Command|Handler)?\b|\bTenants\.Remove\s*\(|IgnoreQueryFilters\s*\(",
      RegexOptions.CultureInvariant)));
  }

  // ⚠ CITES `AC-TEN-0015`'s SECOND CLAUSE — *"…no event contains CREDENTIALS, TOKENS, COMPLETE CLAIMS,
  // BILLING DETAILS, or HTTP CONTEXT."* The banned-name regex carries the criterion's list item for item —
  // `Credential`, `Token`, `Claim`, `Billing`, `Http` — and bans more besides (`Subscription`, `Company`,
  // `ReasonText`, `Actor`, `Correlation`, `Request`, `Trace`). **Banning a superset satisfies the clause;
  // it is the subset direction that would not.**
  //
  // ⚠⚠ NOT THE FIRST CLAUSE. *"Every successful lifecycle change RAISES the corresponding safe event AFTER
  // PERSISTENCE"* is two behavioural claims — that an event is raised at all, and that it is raised after
  // the write — and **a reflection walk over event TYPES cannot see either.** A package that defined all
  // seven events and raised none would pass this test completely.
  //
  // ⚠ `Assert.Equal(7, eventTypes.Length)` IS THE ANTI-VACUITY CONTROL AND IT IS LOAD-BEARING TWICE OVER.
  // The walk is filtered by base type, namespace, name prefix AND a six-name exclusion list, so there are
  // four ways for it to collapse to nothing — and an empty walk satisfies `Assert.Empty(unsafeProperties)`
  // perfectly. **The count is what makes the ban a claim about seven real types.** Unlike the count in
  // `LocalizationCatalogTests`, this one is a floor and not itself a criterion clause: `AC-TEN-0015` states
  // no number.
  [Fact]
  [Trait("Security", "SEC-TEN-0205")]
  [Trait("Scenario", "TS-TEN-0035")]
  [Trait("Acceptance", "AC-TEN-0015")]
  public void Tenant_events_contain_only_safe_lifecycle_values()
  {
    var eventTypes = typeof(Tenant).Assembly.GetTypes()
      .Where(type => typeof(DomainEvent).IsAssignableFrom(type))
      .Where(type => type.Namespace == "SSAS.Platform.Domain.Events")
      .Where(type => type.Name.StartsWith("Tenant", StringComparison.Ordinal) &&
        type.Name is not "TenantUserActivated" and
        not "TenantUserDeactivated" and
        not "TenantUserInvited" and
        not "TenantUserReactivated" and
        not "TenantUserRoleAssigned" and
        not "TenantUserRoleRemoved")
      .ToArray();
    var unsafeProperties = eventTypes.SelectMany(type => type.GetProperties()
      .Where(property => Regex.IsMatch(
        property.Name,
        "Http|Claim|Credential|Token|Subscription|Billing|Company|ReasonText|Actor|Correlation|Request|Trace",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
      .Select(property => $"{type.Name}.{property.Name}")).ToArray();

    Assert.Equal(7, eventTypes.Length);
    Assert.Empty(unsafeProperties);
  }

  // ⚠⚠⚠ `AC-TEN-0013` HAD NO TEST, AND IT IS A COMPLEMENT CLAIM THE SIGNATURES CLOSE FOR FREE.
  //
  // *"Caller-supplied STATUS OR ELIGIBILITY values cannot create, activate, suspend, reactivate, archive,
  // or AUTHENTICATE a Tenant outside the persisted lifecycle rules."* Six verbs, and the cheapest possible
  // proof: **a value that cannot be PASSED cannot be honoured.** An absent-parameter assertion is a
  // complement claim closed by the signature itself, which needs no arity pin of its own — the parameter
  // list IS the pin. The idiom is already one field over in `TenantLifecycleDomainTests
  // .Creation_factory_generates_immutable_identifier_and_rejects_null_value_objects`, which asserts
  // `Tenant.Create` takes no `tenantId`; this is that move applied to status.
  //
  // ---- THREE ROUTES, THREE CONTROLS, BECAUSE BLOCKING ONE LEAVES THE OTHER TWO OPEN.
  //
  //   PASS IT IN    a `TenantStatus` parameter on a transition method
  //   ASSIGN IT     a public setter on `Status` or `IsAuthenticationEligible`
  //   CARRY IT      a status member on a lifecycle COMMAND, so the transport supplies it
  //
  // Each is independently sufficient to break the criterion and none implies the others, so all three are
  // asserted and all three were planted separately.
  //
  // ⚠⚠⚠ AND THE BAN IS BY TYPE IDENTITY, NOT BY NAME, FOR A REASON THAT WOULD HAVE BITTEN IMMEDIATELY.
  // `Suspend`, `Reactivate` and `Archive` all take a `TenantStatusChangeReason`, which IS caller-supplied
  // and IS legitimate — `Created_reason_is_rejected_for_transitions_and_reactivation_uses_bounded_
  // resolution_reasons` guards its bounds. **A name-based ban on `Status` catches
  // `TenantStatusChangeReason` and would have flagged the reason parameter on three of the five
  // transitions.** The two are different TYPES and identical as substrings, so the mechanism separates them
  // and the name cannot. Third time tonight that a name spanned two populations.
  //
  // ⚠⚠ THE QUERY EXEMPTION ASSERTS ITS OWN GROUNDS. `ListTenantsQuery.Status` is a caller-supplied
  // `TenantStatus?` and is excluded, because it SELECTS rows rather than SETTING state — the criterion is
  // about values that drive a transition, not values that filter a read. That exemption is only sound while
  // the property is a query filter, so its presence is ASSERTED below: **if the list filter is ever removed,
  // this test reddens and the exemption is re-examined rather than left standing over nothing.**
  [Fact]
  [Trait("Acceptance", "AC-TEN-0013")]
  public void No_tenant_lifecycle_entry_point_accepts_a_caller_supplied_status_or_eligibility()
  {
    var statusType = typeof(SSAS.Platform.Domain.Enums.TenantStatus);
    var reasonType = typeof(SSAS.Platform.Domain.Enums.TenantStatusChangeReason);

    static bool IsStatusOrEligibility(Type type, string? name, Type statusType) =>
      type == statusType ||
      Nullable.GetUnderlyingType(type) == statusType ||
      (name is not null && name.Contains("eligib", StringComparison.OrdinalIgnoreCase));

    // ---- ROUTE 1: PASS IT IN. The five lifecycle entry points take no status and no eligibility.
    var lifecycle = typeof(Tenant).GetMethods()
      .Where(method => method.IsPublic)
      .Where(method => method.Name is "Create" or "Activate" or "Suspend" or "Reactivate" or "Archive")
      .ToArray();

    // MEMBERSHIP CONTROL. Without it a renamed transition leaves the ban walking a shorter list in silence.
    Assert.Equal(5, lifecycle.Length);

    Assert.Empty(lifecycle
      .SelectMany(method => method.GetParameters().Select(parameter => (method, parameter)))
      .Where(entry => IsStatusOrEligibility(entry.parameter.ParameterType, entry.parameter.Name, statusType))
      .Select(entry => $"{entry.method.Name}({entry.parameter.Name})"));

    // ⚠ THE MATCHER CONTROL, AND IT IS THE POINT OF USING TYPES. `TenantStatusChangeReason` is caller-
    // supplied on three transitions and legitimate; a name-based ban would flag every one of them.
    Assert.Contains(lifecycle, method => method.GetParameters().Any(parameter => parameter.ParameterType == reasonType));
    Assert.False(IsStatusOrEligibility(reasonType, "reason", statusType));
    Assert.True(IsStatusOrEligibility(statusType, "status", statusType));

    // ---- ROUTE 2: ASSIGN IT. Status is not publicly settable and eligibility is derived, not stored.
    var status = typeof(Tenant).GetProperty(nameof(Tenant.Status))!;
    var eligible = typeof(Tenant).GetProperty(nameof(Tenant.IsAuthenticationEligible))!;

    // `CanWrite` is TRUE for a private setter, so it cannot be used here — `Status` is `{ get; private
    // set; }` and the transitions need that setter. The question is whether a CALLER can reach it.
    Assert.True(status.SetMethod is null or { IsPublic: false });
    Assert.Null(eligible.SetMethod);

    // ---- ROUTE 3: CARRY IT. No lifecycle command exposes a status or eligibility member.
    var commands = typeof(CreateTenantCommand).Assembly.GetTypes()
      .Where(type => type.IsPublic && type.Namespace == "SSAS.Platform.Application.Tenants")
      .Where(type => type.Name.EndsWith("Command", StringComparison.Ordinal))
      .ToArray();

    // MEMBERSHIP CONTROL, naming all five so a renamed command cannot silently leave the population.
    Assert.Equal(5, commands.Length);
    Assert.Contains(typeof(CreateTenantCommand), commands);
    Assert.Contains(typeof(ActivateTenantCommand), commands);
    Assert.Contains(typeof(SuspendTenantCommand), commands);
    Assert.Contains(typeof(ReactivateTenantCommand), commands);
    Assert.Contains(typeof(ArchiveTenantCommand), commands);

    Assert.Empty(commands
      .SelectMany(type => type.GetProperties())
      .Where(property => IsStatusOrEligibility(property.PropertyType, property.Name, statusType))
      .Select(property => $"{property.DeclaringType?.Name}.{property.Name}"));

    // THE EXEMPTION'S GROUNDS. The one caller-supplied `TenantStatus?` in this namespace is a READ FILTER,
    // and it is asserted present so the exemption cannot outlive the thing it exempts.
    var listFilter = typeof(ListTenantsQuery).GetProperty(nameof(ListTenantsQuery.Status))!;
    Assert.Equal(statusType, Nullable.GetUnderlyingType(listFilter.PropertyType));
  }

  // ⚠⚠⚠ THE READ PROJECTIONS HAD NO SHAPE GUARD AT ALL, AND `AC-TEN-0004`'S CLAIM IS ENTIRELY ABOUT SHAPE.
  //
  // *"Get and bounded list queries ‖ return safe lifecycle projections and NO TENANT BUSINESS DATA."*
  // `TenantLifecycleApplicationTests.Get_and_list_return_bounded_safe_projections` establishes everything
  // BEFORE the bar — page 0 and size 101 are both refused — and nothing after it, because `Map` in that
  // same file BUILDS the dto its fake returns, so the test asserts its own arrangement. **A test that
  // verifies the criterion's SUBJECT is not weak evidence for its PREDICATE; it is no evidence for it**, and
  // that test's name contains every one of the criterion's words, which is exactly why it read as covered.
  //
  // The absence was searched two ways before building this: `TenantDto` appeared in `tests/` only in that
  // one file and only as construction, and no test in this project walked `SSAS.Platform.Application
  // .Tenants` by namespace — by NAME and by MECHANISM, the two routes a guard could have reached it.
  //
  // ---- WHY A TERM BAN ALONE WOULD HAVE REPRODUCED THE DEFECT IT IS MEANT TO CATCH.
  //
  // `Tenant_events_contain_only_safe_lifecycle_values` above is the idiom: a term regex AND an arity pin.
  // The pin is the load-bearing half. **A ban list is a claim about the names you thought of; the criterion
  // says NO business data, which is a claim about the COMPLEMENT** — and a complement claim is closed by
  // enumeration only when something else pins the SIZE of the set being enumerated. Ban `Employee` and a
  // `PrimaryContactSalary` walks straight through.
  //
  // So the two assertions do different jobs and both are needed: the exact member sets close the criterion
  // for the projections that EXIST, and the term ban covers a projection ADDED to this namespace tomorrow,
  // which no member set can anticipate.
  //
  // ⚠ THE TWO PINS ARE DELIBERATELY DIFFERENT IN STRENGTH. `TenantDto` pins NAMES: `AC-TEN-0004` asks what
  // is EXPOSED, and a `TenantCode` that changes representation is not a business-data question. The
  // eligibility result pins NAME AND TYPE, because `AC-TEN-0016` says *returns EXACTLY* — a contract shape
  // rather than a field list.
  //
  // ⚠⚠ AND THAT SECOND PIN CLOSES A RESIDUAL RECORDED ONE COMMIT AGO. `Eligibility_is_derived_exactly_and_
  // has_no_name` asserts five members PRESENT; nothing asserted they were the ONLY five, so a sixth property
  // passed every line there unless its name contained `Name`. It also carried ONE of that criterion's six
  // bans. **With the set pinned exactly, the other five bans stop being assertions and become CONSEQUENCES**
  // — no `IQueryable`, aggregate, generic repository, subscription decision or authorization grant can be a
  // sixth member of a set asserted to have exactly five. One pin, five bans discharged.
  //
  // ⚠⚠⚠ PLANTED TWICE, SEPARATELY, BECAUSE A SHARED FLOOR OVER A UNION HIDES ONE MEMBER COLLAPSING.
  // See the plant record above the class for what each reddened.
  [Fact]
  [Trait("Acceptance", "AC-TEN-0004")]
  [Trait("Acceptance", "AC-TEN-0016")]
  public void Tenant_read_projections_expose_exactly_their_lifecycle_contract()
  {
    var projections = typeof(TenantDto).Assembly.GetTypes()
      .Where(type => type.IsPublic && type.Namespace == "SSAS.Platform.Application.Tenants")
      .ToArray();

    // MEMBERSHIP CONTROL. Without it the ban below passes over a namespace that was renamed or emptied.
    Assert.Contains(typeof(TenantDto), projections);
    Assert.Contains(typeof(TenantAuthenticationEligibilityResult), projections);

    // ---- THE ARITY PINS. These are what make *no business data* and *exactly* mean anything.
    Assert.Equal(
      [
        "CreatedBy",
        "CreatedUtc",
        "ModifiedBy",
        "ModifiedUtc",
        "RowVersion",
        "Status",
        "StatusChangeReasonCode",
        "StatusChangedBy",
        "StatusChangedUtc",
        "TenantCode",
        "TenantId",
        "TenantName"
      ],
      typeof(TenantDto).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));

    Assert.Equal(
      [
        "Exists:Boolean",
        "IsAuthenticationEligible:Boolean",
        "TenantAuthenticationIneligibilityReason:TenantAuthenticationIneligibilityReason",
        "TenantId:Guid",
        "TenantStatus:TenantStatus?"
      ],
      typeof(TenantAuthenticationEligibilityResult).GetProperties().Select(Describe).Order(StringComparer.Ordinal));

    // ---- THE TERM BAN, WHICH COVERS THE PROJECTION THAT DOES NOT EXIST YET.
    //
    // ⚠⚠⚠ READ THIS BEFORE ADDING A PIN. **THE BAN'S POPULATION IS THE WHOLE NAMESPACE; ITS LOAD-BEARING
    // POPULATION IS THE UNPINNED TYPES ONLY.** On any type with a member pin above, the pin fires first and
    // the ban can never be the first failure — it is subsumed there, and no plant on a pinned type can
    // observe it. That is why plant B had to be placed on `GetTenantQuery`.
    //
    // The consequence runs the wrong way round from intuition: **pinning the remaining types in this
    // namespace would make this ban completely unobservable while making the suite look stronger.** If you
    // pin more types, either accept that this ban is then decorative and say so, or move it to a population
    // that still has unpinned members. Do not leave it looking like a namespace-wide control when its
    // observable population is empty.
    var properties = projections.SelectMany(type => type.GetProperties()).ToArray();
    Assert.NotEmpty(properties);

    const string TenantBusinessData =
      "Employee|Department|Position|Branch|Company|Payroll|Attendance|Journal|Ledger|Account|Invoice|" +
      "Salary|Subscription|Billing|Credential|Password|Token|Claim|Secret";

    // ⚠ THE MATCHER CONTROL. Every name on the right is a real property in this namespace today, and every
    // one of them is legitimate lifecycle metadata — the ban has to let all of them through.
    Assert.Matches(TenantBusinessData, "EmployeeCount");
    Assert.Matches(TenantBusinessData, "BillingContact");
    Assert.DoesNotMatch(TenantBusinessData, "TenantCode");
    Assert.DoesNotMatch(TenantBusinessData, "StatusChangeReasonCode");
    Assert.DoesNotMatch(TenantBusinessData, "IsAuthenticationEligible");
    Assert.DoesNotMatch(TenantBusinessData, "StatusChangedBy");
    Assert.DoesNotMatch(TenantBusinessData, "RowVersion");

    Assert.Empty(properties
      .Where(property => Regex.IsMatch(
        property.Name, TenantBusinessData, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
      .Select(property => $"{property.DeclaringType?.Name}.{property.Name}"));

    static string Describe(PropertyInfo property)
    {
      var underlying = Nullable.GetUnderlyingType(property.PropertyType);
      return underlying is null
        ? $"{property.Name}:{property.PropertyType.Name}"
        : $"{property.Name}:{underlying.Name}?";
    }
  }

  // ==================================================================================================
  // THE FOUR-SPELLING MILESTONE GUARD IS RETIRED (`DEC-L-030`). WHAT REPLACED IT, AND WHAT DID NOT.
  // ==================================================================================================
  //
  // ---- WHAT WAS HERE, AND WHY IT WENT.
  //
  // `Milestone_contains_no_deferred_tenant_endpoint_or_post_session_implementation` scanned Platform source
  // for four declaration spellings -- TenantController, Subscription, Billing, CompanyProvision -- on the
  // authority of `AC-TEN-0020`, FP-003's first-milestone scope statement, which lists ELEVEN deferred
  // concerns.
  //
  // **It checked four spellings of a rule that had already expired three times over.** `Company` shipped in
  // FP-005; `AuthenticationSession` and `RefreshToken` in FP-002. The guard went on passing only because it
  // looked for `CompanyProvision` rather than `Company`, and never named the other two. `TenantController`
  // could not have fired at all -- this codebase maps minimal-API endpoints and declares no controllers.
  //
  // Subscription was simply the first term whose spelling it caught, when `DEC-L-004` and `DEC-L-006` ruled
  // the commercial plane in scope and ratified FP-014 was built (T-035).
  //
  // **Retired rather than trimmed.** Dropping `Subscription` alone would have left a guard passing for the
  // wrong reason -- still appearing to protect a boundary three shipped features had already crossed, with
  // its clearest counter-example quietly removed. `AC-TEN-0020` now records which package superseded which
  // concern.
  //
  // ---- WHAT SURVIVED, AND WHY IT IS ITS OWN TEST.
  //
  // The retired test carried a SECOND assertion unrelated to the four spellings: that `SSAS.Platform.API`
  // does not reach into `SSAS.Platform.Application.Tenants`. **That one is still live and still true** --
  // no tenant endpoint is mapped anywhere in this product, verified before retiring. Retiring the whole
  // test would have dropped it silently, so it stands here on its own.
  [Fact]
  [Trait("Acceptance", "AC-TEN-0020")]
  [Trait("Scenario", "TS-TEN-0034")]
  public void Tenant_endpoints_remain_deferred_and_the_platform_api_does_not_reach_tenant_application()
  {
    var files = PlatformSourceFiles().ToArray();

    // The scan must find something, or everything below asserts nothing at all.
    Assert.NotEmpty(files);

    Assert.Empty(files.Where(path =>
      path.Contains($"{Path.DirectorySeparatorChar}SSAS.Platform.API{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
      File.ReadAllText(path).Contains("SSAS.Platform.Application.Tenants", StringComparison.Ordinal)));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0014")]
  [Trait("Scenario", "TS-TEN-0028")]
  public void Tenant_migration_creates_only_tenants_and_does_not_retrofit_legacy_foreign_keys()
  {
    var migration = Directory.EnumerateFiles(
        Path.Combine(FindRepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations"),
        "*AddTenantLifecycle.cs")
      .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));
    var source = File.ReadAllText(migration);

    Assert.Single(Regex.Matches(source, "migrationBuilder.CreateTable", RegexOptions.CultureInvariant).Cast<Match>());
    Assert.DoesNotContain("AddForeignKey", source, StringComparison.Ordinal);
    Assert.DoesNotContain("InsertData", source, StringComparison.Ordinal);
    Assert.DoesNotContain("TenantUsers", source, StringComparison.Ordinal);
    Assert.DoesNotContain("Roles", source, StringComparison.Ordinal);
    Assert.Contains("TR_Tenants_PreventDelete", source, StringComparison.Ordinal);
  }

  // ---- ⚠⚠⚠ BUILD OUTPUT, EXCLUDED IN T-192 — AND UNTIL T-192 IT WAS NOT (measured).
  //
  // `src/Platform` contains every Platform project, and each project's `obj/` and `bin/` are BENEATH it,
  // so `AllDirectories` walked straight into them. ***MEASURED 2026-09-07: 786 files, of which 756 are
  // real sources — 30 GENERATED FILES in the population.***
  //
  // ---- ⚠⚠⚠ THE TRIGGER, NAMED, BECAUSE IT IS A SIDE EFFECT FOR WHOEVER PULLS IT.
  //
  // The consumer above bans `SSAS.Platform.Application.Tenants` inside `SSAS.Platform.API` files, and
  // **generated files under that project satisfy the path predicate** — `obj/Debug/net8.0/` sits under
  // `…{sep}SSAS.Platform.API{sep}…`. Today `SSAS.Platform.API.GlobalUsings.g.cs` holds only the seven
  // `System` namespaces, so the ban was latent rather than firing.
  //
  // ***ADD `<Using Include="SSAS.Platform.Application.Tenants" />` TO THE API `.csproj` — the kind of edit
  // someone makes while TIDYING USINGS — AND MSBUILD WRITES THAT EXACT STRING INTO `GlobalUsings.g.cs`,
  // REDDENING A TENANT-BOUNDARY GUARD FOR A REASON THAT HAS NOTHING TO DO WITH THE RULE.*** *Nobody making
  // that edit would connect it to this test, which is the only condition under which naming a trigger pays.*
  //
  // ⚠⚠ DEMONSTRATED RATHER THAN ARGUED (T-192). A generated-looking file carrying that token, planted under
  // `SSAS.Platform.API/obj/Debug/net8.0/`, reddened the guard at :568. **The message was
  // `Assert.Empty() Failure: Collection was not empty` with the collection TRUNCATED mid-path** — so the
  // false red did not even name its offender. *That is the cost this filter buys off.*
  //
  // ⚠ The floor at :566 is `Assert.NotEmpty(files)` and CANNOT see any of this: 786 and 756 both pass it.
  //
  // The clause is COPIED from `DepartmentReadScopeArchitectureTests` rather than retyped.
  private static IEnumerable<string> PlatformSourceFiles() => Directory
    .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src", "Platform"), "*.cs", SearchOption.AllDirectories)
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
