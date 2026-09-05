using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// FP-005's UNCITED CRITERIA, CENSUSED 2026-09-05. FOUR OF THE SEVEN ARE DISPOSED HERE. NONE IS CITED.
// ==================================================================================================
//
// ***WHY THIS IS WRITTEN DOWN RATHER THAN REPORTED.*** The census that produced it was delivered as prose
// between two sessions, and a tree-wide sweep then partitioned the 85 uncited criteria into **9 tripwired ·
// 36 disposed in a comment · 40 recorded nowhere** — ***and twenty-five of that last forty were cells
// examined that same evening.*** **They counted as unexamined because the examination lived in a
// conversation.** *A finding delivered in a message is on the same path as a script in a scratchpad: alive
// while the session is, gone after.* **The 36 that survived are the ones somebody stopped and wrote down.**
//
// ⚠ **NOTHING BELOW IS A CITATION AND NONE OF IT SHOULD BE COUNTED AS COVERAGE.** These are dispositions —
// what was checked, what was found, and why no trait follows.
//
// ---- ⚠⚠⚠ `AC-CMP-0018` IS **FALSE AS WRITTEN**, AND IT IS THE ONLY FALSE CELL IN 23 CENSUSED.
//
// *"Company uses the existing **Platform** context, schema, connection, migration history, and Unit of Work."*
//
// **`TenantDbContext.cs:84` — `public DbSet<Company> Companies => Set<Company>();`** The configuration lives
// at `Persistence/TenantErp/Configurations/CompanyConfiguration.cs`. Migration
// `20260814110659_MoveCompanyToTenantDatabase.cs:8`, verbatim: ***"Relinquishes platform ownership of Company
// (ADR-017). Company now belongs to the tenant ERP"***. **Company is not in the Platform context.**
//
// ⚠⚠ **THE QUALIFIER THAT RESCUES IT IS IN YAML FRONTMATTER, LINE 7, ~75 LINES ABOVE THE DECLARATION AND
// ABOVE THE `# Acceptance Criteria` HEADING: `milestone: Milestone 1`.** Under that scope the sentence is
// historically true. ***THIS IS THE READING-DIRECTION FAILURE AT DOCUMENT SCALE*** — every reader in this
// project, and the trait matcher, reads FORWARD from a declaration.
//
// ⚠ **AND THE BLAST RADIUS IS ONE, WHICH IS WORTH STATING BECAUSE THE OPPOSITE WAS ASSUMED.** Only two
// `acceptance-criteria.md` files carry a `milestone:` key (FP-005, FP-006). **Every other scoped criterion in
// both puts the scope IN ITS OWN SENTENCE** — `AC-CMP-0016` and `AC-CMP-0019` open *"Milestone 1 introduces
// no…"*, FP-006's three deferrals open *"FP-006 introduces no…"*, and `AC-EMP-0047` was corrected to that
// form on 2026-08-31 for exactly this reason. **`AC-CMP-0018` is the single lapse in a convention the tree
// otherwise keeps.** *Owner item: it needs its scope in its sentence, not a new file layout.*
//
// ---- `AC-CMP-0016` AND `AC-CMP-0019` — DECAYED MILESTONE-SCOPE CLAIMS, AND THE TEST BELOW ALREADY SAYS SO.
//
// `0016`: *"Milestone 1 introduces no `ICompanyOwnedEntity` interface, no company query filter, no company
// write guard, and no current-company / scope-resolution persistence."* ***ALL FOUR NOW EXIST*** —
// `BuildingBlocks.Domain/ICompanyOwnedEntity.cs` implemented across roughly thirty domain types,
// `TenantDbContext.ApplyCompanyRulesAsync`, `CompanyWriteAuthorizer`, `CurrentCompany`.
//
// ⚠ **`ICompanyOwnedEntity_is_a_separate_opt_in_contract_that_company_does_not_implement` BELOW ALREADY TELLS
// THIS STORY IN FULL** — *"This assertion previously required that `ICompanyOwnedEntity` did NOT exist. That
// was correct for FP-005 Milestone 1 and only for it."* ***IT JUST NEVER NAMED THE CRITERION, WHICH IS WHY
// THE CELL READ AS UNEXAMINED.*** *That is the whole write-back in one instance: the reasoning existed, the
// id did not, and only the id is machine-readable.*
//
// `0019` is the same shape one clause wider — `UserCompanyAccess` is the user↔company assignment it says M1
// introduces none of, and the GL fiscal calendar ships.
//
// ---- ⚠⚠⚠ CORRECTED 2026-09-05: "DECAYED" IS THE WRONG WORD, AND FP-005's README SAYS SO.
//
// **A first draft called both DECAYED, which implies rot. ***`README.md:88` DEFERS EXACTLY THESE THINGS BY
// NAME:*** *"Fiscal calendar, additional currencies, language, and numbering sequences are acknowledged and
// **deferred to later milestones**."* And `README.md:39` opens *"## Scope (Milestone 1)"*.
//
// ***SO THESE TWO STATED AN M1 BOUNDARY THAT LATER MILESTONES WERE ALWAYS PLANNED TO CROSS, AND THEY
// CROSSED IT. THAT IS COMPLETION, NOT DECAY*** — the criteria are uncitable now for the same reason a passed
// phase gate is uncitable, and nothing went wrong. **Both name their own milestone in their own sentences,
// so a forward reader meets the qualifier; `AC-CMP-0018` is the outlier precisely because it does not.**
//
// ⚠ **This correction is one of five tonight that all ran the same way: *what the audit attributed to defect,
// the tree had recorded as plan* — and in every case the record was in a document the criterion does not
// name.** *`README.md` here; `business-rules.md` and `data-model.md` for `AC-EMP-0001`; `api-contracts.md`
// for three FP-014 criteria; `localization-resolution-model.md` for `AC-LOC-0016`.*
//
// ---- `AC-CMP-0009` — WITNESSED IN PART, HERE, AND THE PART THAT IS MISSING IS NAMED.
//
// *"No Domain operation, command, repository method, API contract, or migration cascade physically deletes a
// Company."* **FIVE CHANNELS.** `Company_uses_a_guid_aggregate_key_and_exposes_no_physical_delete` covers the
// DOMAIN one; `Company_repository_is_aggregate_specific_without_delete_or_queryable` covers the REPOSITORY
// one; `CompanyApiArchitectureTests.Company_route_builder_exposes_no_delete_reactivate_restore_or_suspend_
// route` covers the API CONTRACT one. **A runtime guard also exists — `TenantDbContext.PreventCompanyDeletion`
// throws on a tracked `Deleted` Company.**
//
// ***THE MIGRATION-CASCADE CHANNEL IS ASSERTED BY NOTHING, SO THE CRITERION IS NOT CITED.*** *Four of five
// would read as five* — the failure this project refuses everywhere else, and refusing it here costs a
// citation and keeps the sentence honest.
//
// ---- THE OTHER THREE OF FP-005's SEVEN, DISPOSED ELSEWHERE AND POINTED AT FROM HERE SO THE SET IS CLOSED.
//
// `AC-CMP-0003` (*a normalized code may repeat across tenants*) — **structurally guaranteed by
// `CompanyConfiguration.cs:86`, `HasIndex(new { TenantId, NormalizedCompanyCode }).IsUnique()`: per-tenant
// uniqueness is the index's SHAPE.** Witnessed exactly by `TenantCompanyOrganizationSqlServerTests`
// (`Company_migration_enforces_schema_uniqueness_and_cross_tenant_isolation`, which inserts the same code
// into a second tenant). ⚠⚠ ***IT IS THE ONLY CELL IN 23 WITH A WHOLE-SENTENCE WITNESS AVAILABLE — AND THE
// WITNESS IS IN THE INTEGRATION SUITE, WHICH IS GREEN AT A DATE RATHER THAN GATED.*** **So the one cell where
// "uncited" understates coverage is also one where "covered" would overstate it.** *Uncited · untagged ·
// green-at-a-date · unrecorded: four words that all render to a reader as "not covered".*
//
// ---- ⚠⚠⚠ `AC-CMP-0011` AND `AC-CMP-0012` — CORRECTED 2026-09-05. BOTH HAVE WITNESSES. I SAID THEY DID NOT.
//
// **The first census recorded these as "not asserted / untagged". *THAT WAS A CLAIM ABOUT THE TEST CORPUS
// MADE WITH AN INSTRUMENT THAT READS ONE TRAIT KEY*, and both criteria have real witnesses that key cannot
// see.** *Found by partitioning the uncited pool into dispositions resting on facts about the PRODUCT
// (independent of any tagging convention) and dispositions resting on facts about the CORPUS (made with the
// blind instrument). These two were in the second bucket and both were wrong.*
//
// ***`AC-CMP-0011` — TWO OF THREE CLAUSES WITNESSED AGAINST REAL SQL SERVER, TAGGED `TS-CMP-0045`,
// INVISIBLE TO EVERY CRITERION COUNT.*** In `TenantCompanyOrganizationSqlServerTests`:
//
//   *"cannot be changed afterward"*    `Company_tenant_id_cannot_change_after_creation` — mutates `TenantId`
//                                      on a tracked entity, asserts the save THROWS, then **re-reads in a
//                                      fresh context** and asserts the stored value is unchanged. *The
//                                      re-read is what makes it a witness rather than a demonstration.*
//   *"mismatched tenant … rejected"*   `Company_insert_with_mismatched_tenant_is_rejected_by_assign_tenant`
//                                      — adds an aggregate carrying tenant B under tenant A's context,
//                                      asserts the throw, then asserts zero rows **`IgnoreQueryFilters()`**.
//
// ⚠⚠ **THAT `IgnoreQueryFilters()` IS WORTH LIFTING OUT AS A NAMED PATTERN: A ZERO-ROW ASSERTION UNDER AN
// ACTIVE TENANT QUERY FILTER IS A VACUITY THAT CERTIFIES ITSELF** — the filter would hide the row whether or
// not the write was refused. *That author knew it.*
//
// ***AND IT IS STILL NOT CITED, DELIBERATELY.*** The third clause — *"`TenantId` is assigned from the trusted
// current tenant at creation"* — is asserted by nothing: I listed all eleven tests in that file and none
// asserts that an EMPTY `TenantId` is stamped from context. **The two witnessed clauses say the mechanism
// cannot be SUBVERTED; the missing one says it WORKS.** *Citing on the wrong half is the one-of-five
// partial-citation error running in the flattering direction.* ⚠ **The gap is small and buildable: create a
// company with a default `TenantId` under a trusted context and assert it persists owned by that tenant.**
//
// ***`AC-CMP-0012` — WITNESSED BY AN UNTAGGED TEST, WHICH IS A LARGER BLIND SPOT THAN THE TAGGED ONE.***
// `CompanyRouteInventoryTests.Every_route_requires_the_permission_the_inventory_names()` carries **no trait
// at all** and asserts clause 1 directly; the separability clause is visible in the same inventory — seven
// company routes across THREE distinct permissions (`ViewCompanies` ×2, `ManageCompanies` ×2,
// `CompanyLifecycle` ×3). **The cross-tenant clause is not asserted there, so again no trait.**
//
// ⚠⚠⚠ **MEASURED, BECAUSE THE SCALE IS THE POINT: of 3,438 test methods in this tree, 691 carry a criterion
// trait, 1,110 carry a trait indexed to some OTHER id space (`DEC-`, `ADR-`, `TS-`, `OD-`), and 1,637 carry
// NO TRAIT AT ALL.** ***A CRITERION'S WITNESS CAN BE IN ANY OF THE THREE, AND THE CENSUS READS ONE.***
//
// `AC-CMP-0013` is disposed in `CompanyApiArchitectureTests`, where its body channel is witnessed.
public sealed class CompanyArchitectureTests
{
  [Fact]
  [Trait("Decision", "DEC-CMP-0001")]
  [Trait("Decision", "DEC-CMP-0004")]
  [Trait("Scenario", "TS-CMP-0085")]
  public void Company_is_tenant_owned_and_auditable_but_not_company_owned()
  {
    var interfaces = typeof(Company).GetInterfaces();

    Assert.Contains(typeof(ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(IAuditableEntity), interfaces);
    // ⚠ TYPED, NOT NAMED (252). This was `contract.Name == "ICompanyOwnedEntity"`, which passes when the
    // predicate matches NOTHING — so a typo in the name asserted nothing and still reported PASSED. That
    // was measured on this pattern, not argued. As a `typeof` a wrong name is CS0246 at build time.
    //
    // ⚠⚠ DO NOT APPLY THIS TO THE `type.Name == "ICompanyOwnedEntity"` BELOW — IT IS DELIBERATE. That one
    // resolves the interface by REFLECTION OVER `ITenantOwnedEntity`'S ASSEMBLY in order to assert WHICH
    // ASSEMBLY DECLARES IT; a `typeof` there binds at compile time and would assert nothing about location.
    // Its `Assert.NotNull` is the companion proving that lookup can match.
    Assert.DoesNotContain(typeof(ICompanyOwnedEntity), interfaces);
  }

  // ---- SUPERSEDED PREMISE, RETAINED PROTECTION (FP-006C1).
  //
  // This assertion previously required that `ICompanyOwnedEntity` did NOT exist. That was correct for FP-005
  // Milestone 1 and only for it: `DEC-CMP-0005` and `ADR-014` decision 6 deferred the interface "until the
  // first real company-owned business record", and FP-006C1 is that moment — `ADR-025` decision 1 introduces
  // it as shared infrastructure ahead of Employee.
  //
  // So the deferral half is retired by approved decision rather than by convenience, and what remains is the
  // half that never expired: the interface is a SEPARATE, OPT-IN contract in the shared Domain layer, and
  // `Company` is the company ROOT and must never implement it. That is the assertion with a live failure
  // mode — a Company scoped by company would be self-referential nonsense that nothing else would catch.
  [Fact]
  [Trait("Decision", "DEC-CMP-0005")]
  [Trait("Decision", "DEC-EMP-0002")]
  [Trait("Scenario", "TS-CMP-0086")]
  public void ICompanyOwnedEntity_is_a_separate_opt_in_contract_that_company_does_not_implement()
  {
    var companyOwned = typeof(ITenantOwnedEntity).Assembly.GetTypes()
      .SingleOrDefault(type => type.Name == "ICompanyOwnedEntity");

    // It lives beside ITenantOwnedEntity in the shared Domain layer, not in Platform: otherwise every future
    // company-owned module would depend on Platform's Domain to declare its own ownership.
    Assert.NotNull(companyOwned);
    Assert.True(companyOwned!.IsInterface);

    // OPT-IN, NOT INHERITED. Adding CompanyId to ITenantOwnedEntity would force a company dimension onto
    // every tenant-wide record that has none (`ADR-014` decision 4).
    Assert.DoesNotContain(companyOwned, typeof(ITenantOwnedEntity).GetInterfaces());
    Assert.DoesNotContain(typeof(ITenantOwnedEntity), companyOwned.GetInterfaces());

    // And the company root is still not company-owned.
    Assert.DoesNotContain(typeof(Company).GetInterfaces(), contract => contract == companyOwned);
  }

  [Fact]
  [Trait("Decision", "DEC-CMP-0003")]
  public void Company_uses_a_guid_aggregate_key_and_exposes_no_physical_delete()
  {
    Assert.Equal(typeof(AggregateRoot<Guid>), typeof(Company).BaseType);
    Assert.DoesNotContain(
      typeof(Company).GetMethods().Select(method => method.Name),
      name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  [Trait("Security", "SEC-CMP-0205")]
  [Trait("Scenario", "TS-CMP-0083")]
  public void Company_events_contain_only_safe_values()
  {
    var eventTypes = typeof(Company).Assembly.GetTypes()
      .Where(type => typeof(DomainEvent).IsAssignableFrom(type))
      .Where(type => type.Namespace == "SSAS.Platform.Domain.Events")
      .Where(type => type.Name.StartsWith("Company", StringComparison.Ordinal))
      .ToArray();
    var unsafeProperties = eventTypes.SelectMany(type => type.GetProperties()
      .Where(property => Regex.IsMatch(
        property.Name,
        "Name|Code|Currency|Http|Claim|Credential|Secret|Password|Token|Actor|Correlation|Request|Trace|ReasonText",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
      .Select(property => $"{type.Name}.{property.Name}")).ToArray();

    Assert.Equal(5, eventTypes.Length);
    Assert.Empty(unsafeProperties);
  }

  [Fact]
  [Trait("NonFunctional", "NFR-CMP-0305")]
  public void Company_repository_is_aggregate_specific_without_delete_or_queryable()
  {
    Assert.False(typeof(ICompanyRepository).IsGenericType);
    var methods = typeof(ICompanyRepository).GetMethods();
    Assert.DoesNotContain(methods, method =>
      Regex.IsMatch(method.Name, "Delete|Remove", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    Assert.DoesNotContain(methods, method => method.ReturnType.ToString().Contains("IQueryable", StringComparison.Ordinal));
  }

  [Fact]
  [Trait("NonFunctional", "NFR-CMP-0301")]
  public void Company_command_and_query_handlers_are_async_and_accept_cancellation()
  {
    var handlers = new[]
    {
      typeof(CreateCompanyCommandHandler), typeof(UpdateCompanyProfileCommandHandler),
      typeof(ActivateCompanyCommandHandler), typeof(DeactivateCompanyCommandHandler),
      typeof(ArchiveCompanyCommandHandler), typeof(GetCompanyByIdQueryHandler),
      typeof(ListCompaniesQueryHandler)
    };

    Assert.All(handlers, handler =>
    {
      var method = Assert.Single(handler.GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(candidate => candidate.Name == "HandleAsync"));
      Assert.True(typeof(Task).IsAssignableFrom(method.ReturnType));
      Assert.Contains(method.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
    });
  }
}
