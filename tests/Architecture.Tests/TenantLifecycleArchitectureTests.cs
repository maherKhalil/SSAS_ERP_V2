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
  // **So the disposal is *not constructible*, not *unguarded*** — a distinction worth the two commands it
  // cost, because the remedies differ: an unguarded live path wants a test, and an unconstructible one
  // wants exactly this sentence and nothing else.
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

  private static IEnumerable<string> PlatformSourceFiles() => Directory
    .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src", "Platform"), "*.cs", SearchOption.AllDirectories);

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
