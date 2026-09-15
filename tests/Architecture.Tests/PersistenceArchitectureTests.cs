using System.Reflection;
using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// PERSISTENCE STAYS BEHIND THE INFRASTRUCTURE BOUNDARY (T-246).
// ==================================================================================================
//
// ---- ⚠ THIS FILE WAS PROVED TO PASS WHILE MEASURING NOTHING, AND THAT IS WHY IT LOOKS LIKE THIS NOW.
//
// Every test here was `Assert.Empty` over a file walk with **no floor anywhere**. Renaming the one path
// segment the walk filters on — `src` to `sources`, a single plausible layout change — made the walk return
// nothing and **all nine tests passed.** Three real architectural rules were being defended by an
// instrument that could not notice its own absence.
//
// **The failure needed no bug.** A file walk that finds nothing returns an empty set, an empty set contains
// no violations, and no violations is exactly what success looks like. **A green guard produces no prompt
// to ask whether it is measuring anything**, which is why this survived unexamined.
//
// ---- TWO OF THESE WERE NEVER TEXT QUESTIONS, AND THOSE ARE NOW ASKED OF THE COMPILED CODE.
//
// *"Domain and Application remain Entity Framework free"* is an **assembly-reference** question, and
// *"no `IQueryable` on Application boundaries"* is a question about **public signatures**. Reflection
// answers both exactly where text answered them approximately — and, decisively, **reflection cannot fail
// the way the file walk did**: an assembly that cannot be loaded throws, where a directory that matches
// nothing returns empty and reads as success.
//
// That is the general preference this file now embodies: **remove a failure mode rather than detect it.**
// Where the question really is about source text — a naming pattern, a call shape — the scan stays and
// carries a floor instead.
//
// ---- ⚠ PLANT RECORD, WRITTEN HERE RATHER THAN LEFT IN A COMMIT MESSAGE.
//
// An audit of this repository's text-scanning guards found 3 of 5 recorded plants were visible ONLY in git
// history. **A property that can only be established by archaeology stops being established**: the next
// reader sees a green assertion and no reason to trust it, which is how this file reached five unexamined
// rules in the first place.
//
// Each of these was applied, the suite run, and the named assertion observed to fail:
//
//   1. `src` → `sources` in the walk filter — **the original false green, which passed 9 of 9 before this
//      rewrite.** Now reddens all three remaining scans on the file count.
//   2. `Platform` → `PlatformX` — enumeration healthy, path filter dead. Reddens on the second half of
//      `AssertWalkIsIntact`, which is the half a bare count cannot see.
//   3. Assembly glob narrowed to `*.Domain.dll` — reddens the project/assembly cross-check by name.
//   4. Reference prefix pointed at `System.Runtime` — proves the EF check reads real references rather
//      than always finding nothing.
//   5. `IQueryable` widened to `IEnumerable` — proves the signature walk reads real signatures.
//
// **4 and 5 exist because a reflection test that finds nothing is as unfalsifiable as a file walk that
// finds nothing.** Converting away from text removed one failure mode; it did not remove the need to show
// the replacement can fail.
public sealed class PersistenceArchitectureTests
{
  // ⚠ THE ANTI-VACUITY CONTROL IS A CROSS-CHECK, NOT A FLOOR, AND IT IS STRICTLY STRONGER.
  //
  // A floor catches the walk collapsing. **It cannot catch one project quietly dropping out** — eleven
  // assemblies still clear a floor of eight while the twelfth goes unexamined. So the assembly set is
  // compared against a set derived INDEPENDENTLY, by counting `SSAS.*.Domain` and `SSAS.*.Application`
  // directories under `src/`. Two different routes to the same number disagreeing is the signal; either
  // route alone can be silently short.
  [Fact]
  public void Every_domain_and_application_project_is_actually_examined()
  {
    var assemblies = DomainAndApplicationAssemblies();
    var projects = DomainAndApplicationProjectNames();

    Assert.NotEmpty(projects);

    var missing = projects
      .Except(assemblies.Select(assembly => assembly.GetName().Name!), StringComparer.Ordinal)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(missing.Length == 0,
      "a Domain or Application project exists under src/ but its assembly is not loaded here, so every " +
      "rule below silently skips it:\n  " + string.Join("\n  ", missing) +
      "\n\nAdd a project reference from Architecture.Tests, or this file is checking a subset while " +
      "reporting on the whole.");
  }

  // ---- CONVERTED: an assembly-reference question, asked of assembly references.
  //
  // Text-scanning for `Microsoft.EntityFrameworkCore` found a `using`, which is a proxy for the dependency
  // rather than the dependency. A project can reference EF Core and never write the namespace — an
  // extension method reached through a fully-qualified call, or a transitive reference — and the scan
  // would have said nothing. **The reference is the thing the rule is about.**
  //
  // ⚠⚠ AND THAT CONVERSION WAS RIGHT AND STILL SHORT, WHICH IS THE INTERESTING PART (272). The reasoning
  // above is correct; the instrument it reached for does not do it. `GetReferencedAssemblies()` reads
  // EMITTED METADATA, and **the compiler omits a reference no type is taken from** — so a project could
  // declare `Microsoft.EntityFrameworkCore` in its `.csproj`, build, and be reported EF-free here until the
  // day somebody first used it. **THIS MOVED FROM ONE PROXY TO A BETTER PROXY BELIEVING IT HAD REACHED THE
  // THING**, and the improvement is what stopped the search.
  //
  // MEASURED, NOT ARGUED: item `269` planted a forbidden `ProjectReference` and the equivalent assertion
  // stayed green (`3b9728c`).
  //
  // ---- BOTH READINGS, BECAUSE NEITHER SUBSUMES THE OTHER.
  //
  // DECLARED catches the capability the moment the `.csproj` edit merges — which is when the friction is
  // gone and the next developer meets nothing in the way. EMITTED catches consumption, including a type
  // reached TRANSITIVELY that no `.csproj` of ours names. A rule about persistence leaking out of
  // Infrastructure wants both, and they fail on different days.
  [Fact]
  public void Domain_and_application_projects_remain_entity_framework_free()
  {
    // ⚠ THE PREDICATE IS PROVEN TO MATCH ITS TARGET BEFORE ANY EMPTINESS IS READ AS COMPLIANCE.
    //
    // `Assert.Empty` over a filtered set passes when nothing violates the rule AND when the filter cannot
    // recognise a violation — a `.csproj` shape the parse misreads, a `PackageReference` it never sees.
    // `SSAS.BuildingBlocks.Infrastructure` is where EF legitimately lives, so finding it there proves the
    // parse reads a real project and that this term matches something. Without it the ban below holds over
    // nothing.
    //
    // ⚠ IT DOES NOT PROVE *THIS EXACT PREDICATE* CAN FIRE, WHICH IS WHAT THIS COMMENT USED TO CLAIM (278).
    // The lambda below is a COPY of the one in the ban, not the same expression, so it cannot witness that
    // one changing. Left as a copy deliberately: `Contains ⊇ StartsWith`, so widening the ban only makes it
    // fire more — a loud false red. See the control section in `DeclaredDependencies`.
    Assert.Contains(
      DeclaredDependencies.Of("SSAS.BuildingBlocks.Infrastructure"),
      name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));

    var declared = DomainAndApplicationAssemblies()
      .SelectMany(assembly => DeclaredDependencies.Of(assembly)
        .Where(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
        .Select(name => $"{assembly.GetName().Name} DECLARES {name}"))
      .OrderBy(text => text, StringComparer.Ordinal)
      .ToArray();

    var emitted = DomainAndApplicationAssemblies()
      .SelectMany(assembly => assembly.GetReferencedAssemblies()
        .Where(reference => reference.Name is not null
          && reference.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
        .Select(reference => $"{assembly.GetName().Name} USES {reference.Name}"))
      .OrderBy(text => text, StringComparer.Ordinal)
      .ToArray();

    var violations = declared.Concat(emitted).ToArray();

    Assert.True(violations.Length == 0,
      "a Domain or Application project can see or uses Entity Framework, so persistence has leaked out of " +
      "Infrastructure:\n  " + string.Join("\n  ", violations));
  }

  // ---- CONVERTED: a question about public signatures, asked of public signatures.
  //
  // The text version matched the WORD `IQueryable` anywhere in an Application file — including inside a
  // comment explaining why `IQueryable` must not be exposed, which is a false positive, and missing a type
  // that exposes it through an alias or a generic parameter, which is a false negative. Reflection reads
  // what the compiler produced.
  [Fact]
  public void Application_boundaries_do_not_expose_iqueryable()
  {
    var violations = new List<string>();

    foreach (var assembly in DomainAndApplicationAssemblies()
      .Where(assembly => assembly.GetName().Name!.EndsWith(".Application", StringComparison.Ordinal)))
    {
      foreach (var type in assembly.GetExportedTypes())
      {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance
          | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
          if (IsQueryable(method.ReturnType)
            || method.GetParameters().Any(parameter => IsQueryable(parameter.ParameterType)))
          {
            violations.Add($"{type.FullName}.{method.Name}");
          }
        }

        violations.AddRange(type
          .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.DeclaredOnly)
          .Where(property => IsQueryable(property.PropertyType))
          .Select(property => $"{type.FullName}.{property.Name}"));
      }
    }

    Assert.True(violations.Count == 0,
      "an Application boundary exposes IQueryable, so a caller can compose a database query across the " +
      "boundary and the persistence technology is no longer swappable:\n  " +
      string.Join("\n  ", violations.OrderBy(text => text, StringComparer.Ordinal)));
  }

  // ---- THE AUDIT MARKER IS OPT-IN, AND UNTIL NOW NOTHING ASSERTED WHO OPTED IN.
  //
  // `PersistenceDbContext.ApplyPersistenceRules` stamps `ChangeTracker.Entries<IAuditableEntity>()` and,
  // one screenful later, assigns tenants for `Entries<ITenantOwnedEntity>()`. **Two opt-in markers, one
  // method.**
  //
  // ⚠⚠⚠ CORRECTION TO `c8edc4e`, WHICH INTRODUCED THIS TEST AND WHOSE COMMIT BODY CANNOT BE AMENDED ON A
  // SHARED BRANCH. That commit says *"49 carriers, zero structural assertions"*. **THAT IS FALSE.** The
  // census behind it matched two idioms — `typeof(M).IsAssignableFrom` and `IsAssignableTo<M>` — and missed
  // a third, `Assert.Contains(typeof(M), interfaces)`, which occurs **six** times for this marker:
  // `CompanyArchitectureTests:21`, `CompanyOwnershipArchitectureTests:72`, `DepartmentArchitectureTests:44`,
  // `EmployeeArchitectureTests:33`, `ImportExportRunDomainTests:75`, `CompanyDomainTests:375`.
  //
  // ***THE TRUE STATEMENT IS NARROWER AND STRONGER THAN THE ZERO WAS: the structural assertions that exist
  // name `Company`, `UserCompanyAccess`, `Department`, `Employee` and `ImportExportRun` — and NOT ONE of
  // them is among the 15 types that can lose the marker silently.*** Coverage sits where attention was, not
  // where the exposure is. **The exposed set is structurally unwitnessed, which is why the plant below went
  // green and why this test has a job.**
  //
  // ⚠ THE ONE TEST OF THE MECHANISM PROVES THE MECHANISM AND NOT THE MEMBERSHIP.
  // `PersistenceFoundationTests.Save_changes_assigns_utc_audit_fields_and_the_trusted_tenant` saves a
  // `TestAggregate` and asserts all four fields are stamped. It is correct and it is a PROBE — a test-only
  // type that opts in on purpose. **A probe is the right design for testing a guard and it can never witness
  // that a production type is inside the enumeration**, which is why the guard here is well covered and the
  // membership was not covered at all.
  //
  // ---- ⚠⚠ WHY THIS IS KEYED ON THE PROPERTIES AND NOT ON A LIST OF THE 49.
  //
  // A named list is only necessary when removal leaves nothing to key on. **Removal leaves a trace here:
  // the four properties are the type's own, so they survive the marker's deletion.** Keying on them catches
  // removal AND the likelier failure in a growing tree — the new entity that declares audit columns and
  // forgets the marker — which a named list cannot see at all.
  //
  // ---- ⚠ THE POPULATION IS THE 15 THAT NEED IT, AND THAT IS NOT AN OVERSIGHT.
  //
  // Measured over interface maps: of 49 carriers, **34 implement the members EXPLICITLY** (`DateTimeOffset
  // IAuditableEntity.CreatedUtc` forwarding to a `private set` property) and 15 have plain public setters.
  // Dropping the marker from one of the 34 is `CS0540` and **the compiler refuses the build** — measured by
  // a plant that FAILED TO COMPILE, not predicted. So the walk covers exactly the set whose marker can be
  // removed silently, and the compiler covers the rest.
  //
  // ---- ⚠⚠ WHY THE KEY IS A *PUBLIC SETTER* AND NOT MERELY THE FOUR NAMES.
  //
  // The first version keyed on the names alone and **failed on the current tree with six offenders, none of
  // them a defect** — which is the answer to "is this a guard or a bug report", and it was a bug report.
  // Three were DTOs (`CompanyDto`, `TenantDto`, `PlatformSupportPrincipalDto`), which are never tracked and
  // must never be stamped. Three were platform aggregates — `Tenant`, `SubscriptionPlan`,
  // `ModuleDefinition` — which **self-stamp in the domain**, assigning `CreatedUtc`/`CreatedBy` from an
  // explicit `occurredUtc`/`actor` parameter rather than from ambient infrastructure.
  //
  // **`{ get; set; }` and `{ get; private set; }` are the discriminator, and they are two different design
  // statements.** A public setter says *something outside this type assigns this*, and the only thing
  // outside that does is `ApplyPersistenceRules` — so a public setter without the marker means nobody
  // assigns it. A private setter says *this type assigns its own*, which is a deliberate alternative and
  // not this test's business.
  //
  // ⚠ THE BOUNDARY THAT BUYS: a new entity that declares `private set` audit columns and neither
  // self-stamps nor carries the marker is INVISIBLE here. That failure is indistinguishable from `Tenant`
  // by shape alone, and a rule that cannot separate them would fail on `Tenant` forever.
  //
  // ---- ⚠⚠⚠ WHAT THIS DOES NOT ENFORCE, BECAUSE A MARKER WALK NEXT TO AUDIT COLUMNS WILL BE READ AS THE
  // AUDIT-TRAIL GUARD AND IT IS NOT ONE.
  //
  // `BR-PLT-0004` requires *"Every business transaction shall create an immutable audit record"* carrying
  // nine fields: User, Date, Time, Company, Tenant, Action, Entity, Old Values, New Values. **These four
  // columns supply at most three of the nine and none of the remaining six.** And `ModifiedUtc`/`ModifiedBy`
  // are overwritten in place on every change, which **destroys** the previous values — the inverse of an
  // immutable record, not a partial one.
  //
  // **So this test is deliberately UNCITED.** It asserts membership of the stamping mechanism. It asserts
  // nothing whatever about `BR-PLT-0004`, and citing it here would certify as met a rule this mechanism
  // cannot express.
  [Fact]
  public void Every_type_declaring_the_audit_properties_opts_in_to_audit_stamping()
  {
    string[] auditProperties =
      [nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.CreatedUtc),
       nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.CreatedBy),
       nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.ModifiedUtc),
       nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.ModifiedBy)];

    var declaring = new List<Type>();

    foreach (var assembly in DomainAndApplicationAssemblies())
    {
      foreach (var type in assembly.GetExportedTypes())
      {
        // ⚠ A DOMAIN ENTITY, ESTABLISHED BY THE BASE CHAIN RATHER THAN BY ASSEMBLY NAME. `Entity<TId>` is
        // generic with no non-generic base and no marker interface, so the chain is walked. This is what
        // excludes the DTOs: they carry the same four names and are never tracked, and an assembly-name
        // proxy would have to be re-argued every time a type moves.
        if (!IsDomainEntity(type))
        {
          continue;
        }

        var publiclySettable = type
          .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
          .Where(property => property.CanRead && property.SetMethod is { IsPublic: true })
          .Select(property => property.Name)
          .ToHashSet(StringComparer.Ordinal);

        if (auditProperties.All(publiclySettable.Contains))
        {
          declaring.Add(type);
        }
      }
    }

    // ⚠⚠⚠ THIS FLOOR IS AN ANTI-VACUITY CONTROL AND **NOT** A MEMBERSHIP GUARD, AND THE DIFFERENCE DECIDES
    // ITS VALUE. IT IS DELIBERATELY BELOW THE POPULATION AND MUST NOT BE RAISED TO TRACK IT.
    //
    // `TenantOwnershipGuardCoverageTests` selects on its marker, so removal drops a type OUT of its
    // population and its floor is the only defence — there, the floor must equal the count. **This walk is
    // TRACE-KEYED: it selects on the four audit PROPERTIES, which survive the marker's removal. A type that
    // loses the marker STAYS IN this population and fails the assertion BY NAME.** Measured: stripping
    // `IAuditableEntity` from `LeaveType` reddened this test naming `LeaveType`, while leaving the
    // marker-keyed walk green. ***A WALK THAT CAN NAME ITS OFFENDER IS NOT RELYING ON ITS FLOOR.***
    //
    // So this number defends one thing only: that the selection chain is alive. It is four links long —
    // assembly loaded, type exported, property public and declared here, setter public — and the offender
    // list is empty if ANY link stops matching. A floor set ABOVE the population is worse than none: it
    // fails on a correct tree, which is how a floor gets deleted.
    //
    // ⚠ AND THE SLACK COSTS NOTHING HERE, WHICH IS NOT TRUE OF A MARKER-KEYED FLOOR. The five between 10 and
    // today's 15 are not licensed removals: a type that stops declaring audit properties **is not a
    // violation** — it is a type that is no longer auditable, and this test correctly has no opinion on it.
    Assert.True(declaring.Count >= 10,
      $"only {declaring.Count} types were found declaring all four audit properties with public setters; " +
      "the selection chain has stopped matching and the check below would judge nothing.");

    var offenders = declaring
      .Where(type => !typeof(SSAS.BuildingBlocks.Domain.IAuditableEntity).IsAssignableFrom(type))
      .Select(type => type.FullName!)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(offenders.Length == 0,
      "these types declare CreatedUtc, CreatedBy, ModifiedUtc and ModifiedBy but do not implement " +
      "IAuditableEntity, so PersistenceDbContext.ApplyPersistenceRules never stamps them and the columns " +
      "stay at their default values forever:\n  " + string.Join("\n  ", offenders));
  }

  // ---- THE SAME GAP IN THE MARKER THAT GATES AN AUTHORIZATION CHECK RATHER THAN A METADATA STAMP.
  //
  // `ICompanyOwnedEntity` drives `TenantDbContext.ApplyCompanyRulesAsync`: entries of that type make the
  // save call `AuthorizeCurrentCompanyAsync` and receive the trusted `CompanyId`. Its own declaration is
  // explicit that the failure is silent — *"An entity that should have been company-scoped and was not is
  // readable by every company in the tenant, and nothing about it looks wrong."*
  //
  // ⚠⚠⚠ MEASURED PER CARRIER, NOT ARGUED: 24 carriers, each stripped of the marker one at a time and put
  // through the full seven-suite gate. **13 of the 24 lost the marker with the gate STAYING GREEN.**
  // `Department` was caught by one test and `PayElement` by nothing at all — same marker, same removal,
  // opposite results. The exposed 13 were `AttendancePeriod`, `AttendanceRecord`, `EmployeeCompensation`,
  // `JournalDraft`, `JournalEntry`, `LeaveBalance`, `LeaveRequest`, `LeaveType`, `OneOffPayment`,
  // `PayElement`, `PayrollPeriod`, `PayrollRun` and `WorkingCalendar`.
  //
  // ---- ⚠ WHAT THE 13 WERE AND WERE NOT. **They were EXPOSED, never BROKEN.** All 24 carry the marker
  // today and this walk passes on the current tree. It does not fix thirteen defects; it turns thirteen
  // silently-removable markers into zero, in one assertion.
  //
  // ---- THE COVERAGE THAT EXISTED WAS DELIBERATE AND SIMPLY NEVER REACHED THESE MODULES.
  //
  // Every witnessed carrier outside HR was `FiscalYear`, and its test is not incidental —
  // `Finance.Tests/Calendar/CalendarDomainTests.cs:26`
  // `A_fiscal_year_is_company_owned_which_is_what_makes_closing_a_company_scoped_write`, whose comment
  // reads:
  //
  //   > The interface is the mechanism, not the column: ICompanyOwnedEntity is what makes
  //   > TenantDbContext.ApplyCompanyRulesAsync run AuthorizeCurrentCompanyAsync before a close reaches SQL.
  //
  // **That sentence is quoted here rather than referenced because it is the only part of the reasoning that
  // survives being read by a stranger.** A considered assertion and a generic interface enumeration are
  // written identically — `Assert.Contains(typeof(M), interfaces)` — and ***INTENT IS NOT RECOVERABLE FROM
  // FORM, ONLY FROM THE AUTHOR'S OWN SENTENCE AT THE SITE.*** This walk generalises that assertion so the
  // next module does not depend on somebody remembering to write it again.
  //
  // ---- ⚠⚠ WHY THE KEY IS A PUBLICLY SETTABLE `CompanyId`, AND WHY THE INTERFACE ITSELF LICENSES IT.
  //
  // The marker leaves a trace: the property outlives it. And `ICompanyOwnedEntity` states what a public
  // setter MEANS — *"THE SETTER EXISTS SO THE SERVER CAN STAMP IT, and for no other reason."* So a domain
  // entity with a publicly settable `CompanyId` and no marker is a type declaring that the server stamps a
  // value the server has never been told about.
  //
  // ⚠ Same boundary as the audit walk, and it is the honest half: a `private set` `CompanyId` says the type
  // assigns its own, and such a type is invisible here. That is deliberate — `Tenant` and `SubscriptionPlan`
  // self-stamp their audit columns for the same reason, and a rule that could not tell them apart would fail
  // on correct code forever.
  //
  // ---- ⚠ UNCITED, AND THE SEARCH THAT ESTABLISHED IT WAS OVER ALL FOURTEEN PACKAGES.
  //
  // No criterion governs company-scope membership tree-wide. What exists is two narrower kinds, and neither
  // is what this asserts:
  //
  //   * PER-TYPE membership — `AC-DEP-0051` (`Department`) and `AC-POS-0057` (`EmployeePositionAssignment`).
  //     Each names one type and also asserts a NEGATIVE about `IBranchOwnedEntity` that this says nothing
  //     about, and each already has a test that witnesses it directly and more strongly than this would.
  //   * BEHAVIOURAL authorization — `AC-EMP-0026` and `FP-006`'s scope criteria, which are about a CALLER
  //     being refused when scope is revoked. That is the authorizer's behaviour, not which types reach it.
  //
  // `AC-GL-0007` mentions `AuthorizeCurrentCompanyAsync`, but in a commentary note about which scope owns an
  // account — not in a clause. **Citing any of them here would attach a tree-wide claim to a criterion that
  // does not make one**, which is the `BR-PLT-0004` mistake in a fresh costume.
  [Fact]
  public void Every_domain_entity_with_a_server_stamped_company_opts_in_to_company_scoping()
  {
    var declaring = new List<Type>();

    foreach (var assembly in DomainAndApplicationAssemblies())
    {
      foreach (var type in assembly.GetExportedTypes())
      {
        if (!IsDomainEntity(type))
        {
          continue;
        }

        var stamped = type
          .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
          .Any(property =>
            property.Name == nameof(SSAS.BuildingBlocks.Domain.ICompanyOwnedEntity.CompanyId)
            && property.PropertyType == typeof(Guid)
            && property.SetMethod is { IsPublic: true });

        if (stamped)
        {
          declaring.Add(type);
        }
      }
    }

    // ⚠ AN ANTI-VACUITY CONTROL, NOT A MEMBERSHIP GUARD — the same distinction as the audit walk above and
    // the opposite of `TenantOwnershipGuardCoverageTests`. This walk is TRACE-KEYED: a type that loses the
    // marker keeps its `CompanyId` property, stays in this population, and fails the assertion BY NAME. The
    // floor only proves the selection chain is alive, so it sits below the measured population deliberately
    // and must NOT be raised to track it.
    Assert.True(declaring.Count >= 15,
      $"only {declaring.Count} domain entities were found declaring a publicly settable Guid CompanyId; " +
      "the selection chain has stopped matching and the check below would judge nothing.");

    var offenders = declaring
      .Where(type => !typeof(SSAS.BuildingBlocks.Domain.ICompanyOwnedEntity).IsAssignableFrom(type))
      .Select(type => type.FullName!)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(offenders.Length == 0,
      "these domain entities declare a publicly settable CompanyId but do not implement " +
      "ICompanyOwnedEntity, so TenantDbContext.ApplyCompanyRulesAsync never sees them: their writes skip " +
      "AuthorizeCurrentCompanyAsync entirely and CompanyId is never stamped from the trusted company " +
      "context. The row is then readable by every company in the tenant and nothing about it looks " +
      "wrong:\n  " + string.Join("\n  ", offenders));
  }

  // ---- NOT CONVERTED: genuinely a question about source text, so it keeps a floor instead.
  //
  // A generic repository is a SHAPE in the source — `IRepository<T>` — and a type that was never written
  // does not exist to be reflected over. Reflection could ask "is any interface generic and named
  // Repository", which is a narrower question than the one being asked.
  // ⚠ SECOND SCENARIO TRAIT ADDED: this rule is `TS-IAM-0047` as well as `TS-AUTH-0071`. `FP-001`'s
  // `test-scenarios.md` defines `TS-IAM-0047` as *"no generic repository exists"* — the same rule, named
  // independently by a second package, and this test was the only thing asserting it for either.
  //
  // ⚠⚠ ONE TEST SERVING TWO PACKAGES AND CITING ONE IS UNDER-CITATION, AND IT IS NOT A CRITERION-AXIS
  // PHENOMENON: it happens on any axis a sweep is keyed to, because **the evidence sits where the behaviour
  // lives and the citation sits where someone happened to be working.** Measured before adding: repeated
  // traits are preserved and BOTH values remain filterable — checked in both directions, since a second
  // trait REPLACING the first would look identical when you only filter on the new one.
  [Fact]
  [Trait("Scenario", "TS-AUTH-0071")]
  [Trait("Scenario", "TS-IAM-0047")]
  public void Production_source_does_not_define_a_generic_repository()
  {
    var files = ProductionSourceFiles();
    AssertWalkIsIntact(files);

    var matches = files
      .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\b(?:I)?Repository\s*<", RegexOptions.CultureInvariant))
      .ToArray();

    Assert.Empty(matches);
  }

  // ⚠ CITES `AC-IAM-0017` — *"No API or domain operation physically deletes a user."* The criterion is a
  // CAPABILITY claim, and a capability that does not exist is what makes *cannot* true, so a source-shape
  // ban is the right instrument rather than a behavioural one — there is no delete to call.
  //
  // ⚠⚠ THE SCOPE IS NARROWER THAN THE CRITERION AND THE GAP IS NAMED: this walks `src/Platform/` only. The
  // criterion says *no API or domain operation*, and a delete introduced in a module assembly that reached
  // a Platform user would not be seen here. **Cited for the Platform half, which is where the user
  // aggregate lives.**
  //
  // ⚠⚠⚠ AND THIS FILE IS WHERE `FP-001`'s ROW 27 RESOLVES — the row whose acceptance cell reads
  // *"architecture constraints"* and names no criterion at all. Its three scenarios are real and all three
  // are tested: `TS-IAM-0046` (Domain/Application EF-free) by `Domain_and_application_declare_no_ef_core`
  // above, `TS-IAM-0047` (no generic repository) by the test above this one, and `TS-IAM-0048` (Platform
  // does not depend on HR or GL) by `ModulePermissionContributionArchitectureTests`. **The row is honest and
  // simply is not an acceptance row; `NFR-IAM-0302`/`0303` are its real subjects.**
  [Fact]
  [Trait("Criterion", "AC-IAM-0017")]
  public void Platform_identity_access_has_no_physical_delete_operation()
  {
    var files = ProductionSourceFiles();
    AssertWalkIsIntact(files);

    var violations = files
      .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Platform{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => Regex.IsMatch(
        File.ReadAllText(path),
        @"\bDelete(?:Identity|TenantUser|Role)(?:Async|Command|Handler)?\b|(?:Identities|TenantUsers|Roles)\.Remove\s*\(",
        RegexOptions.CultureInvariant))
      .ToArray();

    Assert.Empty(violations);
  }

  [Fact]
  public void Platform_source_does_not_log_secrets_tokens_or_raw_claims()
  {
    var files = ProductionSourceFiles();
    AssertWalkIsIntact(files);

    var violations = files
      .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Platform{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => Regex.IsMatch(
        File.ReadAllText(path),
        @"Log(?:Trace|Debug|Information|Warning|Error|Critical)\s*\([^;]*(?:password|secret|token|claims?)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
      .ToArray();

    Assert.Empty(violations);
  }

  // ⚠ THE FLOOR THAT WAS MISSING, AND THE SECOND HALF IS THE PART THAT WAS ACTUALLY BROKEN.
  //
  // The count catches the enumeration failing. The Platform check catches the FILTER failing — the rename
  // that produced the false green left the enumeration healthy and made every `Where` match nothing, which
  // a count alone would not have seen because the count was never taken.
  private static void AssertWalkIsIntact(IReadOnlyCollection<string> files)
  {
    Assert.True(files.Count >= 400,
      $"only {files.Count} production source files were found; the walk has degraded and 'no violations' " +
      "below would mean nothing rather than being reassuring.");

    Assert.True(
      files.Any(path => path.Contains($"{Path.DirectorySeparatorChar}Platform{Path.DirectorySeparatorChar}", StringComparison.Ordinal)),
      "the walk found files but none under Platform, so the path filters the Platform rules depend on are " +
      "matching nothing. This is the exact shape that made this file pass while measuring nothing.");
  }

  // `Entity<TId>` is the root of every persisted domain type and is generic, so identity is the base chain
  // rather than a single `IsAssignableFrom`.
  private static bool IsDomainEntity(Type type)
  {
    for (var current = type.BaseType; current is not null; current = current.BaseType)
    {
      if (current.IsGenericType
        && current.GetGenericTypeDefinition() == typeof(SSAS.BuildingBlocks.Domain.Entity<>))
      {
        return true;
      }
    }

    return false;
  }

  // ---- ⚠⚠⚠ BUILD OUTPUT, EXCLUDED IN T-192 — AND UNTIL T-192 IT WAS NOT (measured).
  //
  // This walks the REPOSITORY ROOT with `AllDirectories` and filters to `src` AFTERWARDS, so every
  // `src/**/obj/**/*.cs` matched the predicate and entered the population. ***MEASURED 2026-09-07: 1,333
  // files reached the predicate, 1,133 of which are real sources — 200 GENERATED FILES.***
  //
  // ⚠⚠ THE DIRECTION MATTERS AND IS NOT "A MISSED VIOLATION". All three consumers are `Assert.Empty`
  // BANS, so build output in the population can only produce a FALSE RED — a failure naming a path
  // nobody wrote. **Demonstrated, not argued (T-192):** a generated-looking file planted under
  // `SSAS.Platform.API/obj/Debug/net8.0/` declaring `IRepository<T>` reddened
  // `Production_does_not_define_a_generic_repository`, and *the message was
  // `Assert.Empty() Failure: Collection was not empty` with the path TRUNCATED before it became
  // legible.* **A maintainer would have had no way to tell the offender was generated code.**
  //
  // ⚠ Note the pre-existing `src` test already used this interpolated form — for `src`, not for
  // `bin`/`obj`, which is why a search for the house exclusion clause did not find this file.
  //
  // ⚠ REMAINS TRUE AND IS NOT FIXED HERE: the walk still traverses the whole repository — `.git`,
  // `TestResults` and every test project — to keep the `src` filter as the single place scope is
  // decided. That is a cost, not a correctness problem, and narrowing the root is a separate change.
  //
  // The clause is COPIED from `DepartmentReadScopeArchitectureTests` rather than retyped.
  private static IReadOnlyCollection<string> ProductionSourceFiles() => [.. Directory
    .EnumerateFiles(FindRepositoryRoot(), "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
      !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
    .Where(path => path.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];

  private static bool IsQueryable(Type type) =>
    type.Name.StartsWith("IQueryable", StringComparison.Ordinal)
    || (type.IsGenericType && type.GetGenericArguments().Any(IsQueryable));

  private static Assembly[] DomainAndApplicationAssemblies() =>
    [.. Directory
      .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.dll")
      // `RepositoryPaths.ProjectName` rather than `Path.GetFileNameWithoutExtension`, which
      // `RepositoryPathPortabilityTests` bans outright in this project. The ban is blanket by design:
      // the helper is correct for a local filesystem path and WRONG for an MSBuild `Include`, and the
      // two are indistinguishable at a glance -- which is exactly how the Linux blindness arrived. This
      // use happened to be the safe kind, and a rule that only fires on the unsafe kind needs a reader
      // to classify it correctly every time.
      .Select(RepositoryPaths.ProjectName)
      .Where(name => name is not null
        && (name.EndsWith(".Domain", StringComparison.Ordinal)
          || name.EndsWith(".Application", StringComparison.Ordinal)))
      .Select(name => Assembly.Load(name!))];

  private static string[] DomainAndApplicationProjectNames() =>
    [.. Directory
      .EnumerateDirectories(Path.Combine(FindRepositoryRoot(), "src"), "SSAS.*", SearchOption.AllDirectories)
      .Select(Path.GetFileName)
      .Where(name => name is not null
        && (name.EndsWith(".Domain", StringComparison.Ordinal)
          || name.EndsWith(".Application", StringComparison.Ordinal)))
      .Select(name => name!)
      .Distinct(StringComparer.Ordinal)];

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
