using System.Reflection;
using SSAS.Attendance.Contracts.Summaries;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Leave;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ================================================================================================
// FP-013's ARCHITECTURE GUARDS.
// ================================================================================================
//
// Three of these exist because a RULING required them rather than because a rule was general:
// the entity-by-entity branch classification (`DEC-ATT-0014`), the branch-BLIND summary contract
// (`OD-ATT-0011`), and module isolation in both directions (`ADR-012`, `DEC-ATT-0002`).
// ---- `AC-ATT-0034` IS UNCITED AND THE NON-CITATION IS CORRECT (recorded 2026-09-05).
//
// *"No Attendance table declares a foreign key to a Platform DB table."* ***THE CRITERION IS TRUE AND IT IS
// TRUE BY THE ENGINE RATHER THAN BY ANYONE'S DESIGN: a cross-DATABASE foreign key is not expressible in SQL
// Server, and Platform and the tenant ERP are separate databases.*** A test would assert a consequence of a
// constraint nobody could violate.
//
// ⚠ **`AC-GL-0020` IS THE SAME SENTENCE ONE MODULE OVER AND CARRIES THE SAME DISPOSAL**, recorded in
// `GlSchemaSqlServerTests` on the same day — *and `AC-PAY-0031` is the same sentence a THIRD time and WAS
// cited, on a superset argument that turned out to be false and has since been withdrawn.* **Three
// identically-shaped criteria, three different outcomes, and only reading each against the product told them
// apart.**
public sealed class AttendanceArchitectureTests
{
  // Derived by reflection over the domain assembly, not typed. FP-012 stated its entity count wrong FOUR
  // times — including once inside the section warning about miscounts — which is why every inventory in this
  // file is derived.
  private static Type[] AttendanceEntities() =>
    typeof(AttendanceRecord).Assembly
      .GetTypes()
      .Where(type => type.IsClass && !type.IsAbstract)
      .Where(type => typeof(ITenantOwnedEntity).IsAssignableFrom(type))
      .OrderBy(type => type.Name, StringComparer.Ordinal)
      .ToArray();

  // ================================================================================================
  // DEC-ATT-0014. EVERY ENTITY IS CLASSIFIED — INCLUDING THE NEGATIVES.
  // ================================================================================================
  //
  // `IBranchOwnedEntity` states the stakes itself: *"IMPLEMENTING THIS IS A DELIBERATE CLASSIFICATION, NOT A
  // DEFAULT … the failure mode is silent: an entity that should have been branch-scoped and was not is
  // readable by every branch in the tenant, and nothing about it looks wrong."*
  //
  // HR asserts its classification entity by entity. **Payroll asserts nothing** — its entities are
  // tenant-global by OMISSION, and the only classification guard in the suite
  // (`BranchSessionArchitectureTests.No_tenant_global_or_routing_entity_is_branch_owned`) walks a HARDCODED
  // LIST OF SIX PLATFORM TYPES. Nothing forces a new module to classify itself, which is exactly how Payroll
  // ended up unasserted.
  //
  // So this is a commitment FP-013 makes rather than a rule it inherits. **The expected map is written out
  // and then checked against reflection**, so adding an entity without deciding its classification fails
  // here rather than shipping unclassified.
  // ---- ⚠ CITES `AC-ATT-0036`, WHICH THIS ASSERTS EXACTLY AND WHICH NOTHING CITED.
  //
  // *"Every Attendance entity carries an explicit branch classification assertion — positive or negative,
  // never absent."* **The expected map above is the "explicit", and checking it against reflection is the
  // "never absent": an entity added without a decision fails here rather than shipping unclassified.**
  //
  // ⚠ HOW IT WAS FOUND, BECAUSE THE ROUTE IS WORTH MORE THAN THE CITATION: **this test reddened during an
  // unrelated `ITenantOwnedEntity` plant hours earlier.** I read only whether the expected test fired and
  // moved on. ***A PLANT NAMES EVERY TEST THAT WATCHES THE PROPERTY IT BREAKS, AND WE HAD BEEN READING ONE
  // ROW OF THAT LIST.*** The other rows are free, already paid for, and answer *what watches what* — which
  // is the question a citation pass exists to answer, in the one language that cannot be misread.
  [Fact]
  [Trait("Decision", "DEC-ATT-0014")]
  [Trait("Criterion", "AC-ATT-0036")]
  public void Every_attendance_entity_carries_an_explicit_branch_classification()
  {
    var expected = new Dictionary<string, bool>(StringComparer.Ordinal)
    {
      // ---- THE ONE POSITIVE. Attendance is observed LOCALLY: a branch supervisor records who was present
      // at their branch, and the UserBranchAccess-to-ITenantBranchAccessResolver stack exists so that
      // boundary is enforced rather than trusted.
      [nameof(AttendanceRecord)] = true,

      // ---- THE NEGATIVES, EACH WITH ITS REASON.
      // A calendar is company POLICY, following Department's asserted classification.
      [nameof(WorkingCalendar)] = false,
      [nameof(CalendarHoliday)] = false,
      // A period is a company-level accounting boundary; branch lives on the records inside it.
      [nameof(AttendancePeriod)] = false,
      // A catalog is company policy.
      [nameof(LeaveType)] = false,
      // Entitlement is company policy; the employee's branch does not meter their leave.
      [nameof(LeaveBalance)] = false,
      // Approval runs through the DEPARTMENT chain, not the branch tree — a branch predicate here would
      // filter on a dimension the workflow does not use.
      [nameof(LeaveRequest)] = false
    };

    var actual = AttendanceEntities();

    // The inventory is DERIVED and compared against the map, so a new entity fails this test by being
    // absent from `expected` rather than by being silently unclassified.
    Assert.Equal(
      expected.Keys.OrderBy(name => name, StringComparer.Ordinal),
      actual.Select(type => type.Name));

    foreach (var type in actual)
    {
      var isBranchOwned = typeof(IBranchOwnedEntity).IsAssignableFrom(type);
      Assert.Equal(expected[type.Name], isBranchOwned);
    }
  }

  // Every tenant-owned Attendance type must carry `ITenantOwnedEntity`, or `TenantCutoverCopyPlan` — which
  // derives its manifest by reflecting over that interface — omits it SILENTLY. No error, no warning, and
  // no failing test until a tenant migrates and its data does not arrive.
  [Fact]
  [Trait("Decision", "DEC-ATT-0007")]
  public void The_attendance_entity_inventory_is_seven_and_is_derived_rather_than_listed()
  {
    Assert.Equal(7, AttendanceEntities().Length);
  }

  // ================================================================================================
  // OD-ATT-0011. THE SUMMARY CONTRACT APPLIES NO BRANCH PREDICATE, AND THAT IS GUARD-ASSERTED.
  // ================================================================================================
  //
  // The ruling took BOTH halves of an asymmetry the analysis package could not resolve: records are
  // branch-scoped so a supervisor sees only their branch, and the Payroll summary is branch-BLIND so a
  // payroll run is company-complete.
  //
  // `DEC-PAY-0017` refused a branch filter on the employee roster because **a filter means a payroll-feeding
  // query can silently omit employees** — and an omitted employee's hours produce a payroll that balances
  // perfectly and underpays somebody.
  //
  // **The hole is ruled INTENDED.** The obligation attached to that ruling was: stated at the site, and
  // guard-asserted. This is the guard. It reads the compiled source of the query method and asserts no
  // branch predicate appears — the comment explains the decision, this survives someone who has not read it.
  // ---- ⚠⚠⚠ THE PAYROLL-FACING SUMMARY REFUSES BY THROWING, AND NOTHING ASSERTED THAT.
  //
  // ***DELIBERATELY UNCITED.*** `AC-ATT-0028` describes this mechanism — *"throws
  // `UnauthorizedAccessException` — it does not return an empty list"* — but names a READ SCOPE as its
  // subject, and the read scope does no such thing: `AttendanceScopeResolver` returns `Result.Failure`
  // with `CompanyScopeDenied`. **One criterion, two surfaces.** Whether it is stale, misaddressed, or
  // malformed and in need of splitting is author intent, and it is with the owner. *A test that enforces
  // a misreading is worse than no test, because the gate then defends the misreading.*
  //
  // **The GAP is real under every reading, which is why this exists anyway.** `AttendanceSummaryService`
  // throws twice, is constructed by exactly ONE test file in the tree — an Integration chain — and neither
  // throw is asserted anywhere. A label can be added later; an unwritten test cannot.
  //
  // ⚠ AND THE CONSEQUENCE IS IN THE SOURCE'S OWN WORDS, which is why it is worth a guard rather than a
  // note: *"An empty summary would claim 'this employee worked no hours', a statement about the DATA:
  // payroll would calculate cleanly, pay nothing for the period, and nobody would learn that an
  // authorization check had failed."* **A silent wrong payroll, from a refusal that returned data-shaped
  // emptiness instead of refusing.**
  //
  // ⚠⚠ THE COUNTS ARE EXACT RATHER THAN FLOORS, and that is the assertion doing the work. A floor of one
  // survives somebody converting the second throw into a `Result` — which is exactly the drift this guards,
  // because a `Result` on this path is what the read scope legitimately returns one module over.
  //
  // ⚠⚠⚠ AND THE THIRD PUBLIC METHOD IS EXEMPT, SO ITS GROUNDS ARE ASSERTED RATHER THAN ASSUMED.
  // `GetWorkingDaysAsync` does not authorize. That is defensible — it answers a calendar question and
  // returns no employee data — but *"it looked fine"* is not a guard. **The exemption is pinned by its
  // reason: that method touches no entity set at all.** If it ever queries one, this fails and the
  // exemption has to be re-argued rather than inherited.
  [Fact]
  public void The_payroll_facing_summary_refuses_an_unauthorized_company_by_throwing()
  {
    var source = AttendanceCode("SSAS.Attendance.Infrastructure", "Summaries", "AttendanceSummaryService.cs");

    // MATCHER CONTROL. A source guard's failure mode is reading the wrong file and finding nothing, which
    // is indistinguishable from compliance for every assertion below.
    Assert.Contains("AttendanceSummaryService", source, StringComparison.Ordinal);

    // TWO throws: the unresolved actor, and the company the caller has no grant for.
    Assert.Equal(2, CountOccurrences(source, "throw new UnauthorizedAccessException"));

    // FAIL CLOSED. A FAILED permission lookup refuses; it does not fall through to "all companies".
    Assert.Contains("permitted.IsFailure ||", source, StringComparison.Ordinal);

    // One declaration and two call sites — the two methods that return data. An exact count is what
    // notices a call site being dropped from one of them.
    Assert.Equal(3, CountOccurrences(source, "AuthorizeCompanyAsync"));

    // ---- AND THE EXEMPTION'S GROUNDS.
    var workingDays = source[source.IndexOf(
      "public async Task<int> GetWorkingDaysAsync", StringComparison.Ordinal)..];
    workingDays = workingDays[..workingDays.IndexOf("public async Task<AttendancePeriodInspection>", StringComparison.Ordinal)];

    Assert.DoesNotContain("Set<", workingDays, StringComparison.Ordinal);
    Assert.DoesNotContain("AuthorizeCompanyAsync", workingDays, StringComparison.Ordinal);
  }

  // ---- ⚠⚠⚠ THE APPEND-ONLY REFUSAL CONSULTS NOTHING, AND "NOTHING" IS THE CRITERION'S ACTUAL WORD.
  //
  // `AC-ATT-0015` — *"Attempting to modify or delete any `IAppendOnlyEntity` attendance row throws,
  // **regardless of period status** — the refusal is unconditional and no code path may assume otherwise."*
  //
  // ⚠ THE EXISTING TESTS SAMPLE THE BEHAVIOUR; THIS ASSERTS THE PROPERTY, AND THE CRITERION ASKS FOR THE
  // PROPERTY. `AttendanceSchemaSqlServerTests` proves a record cannot be modified and cannot be deleted —
  // **both against a single seeded period, so neither varies the status the criterion names.** A scenario
  // pair can only ever sample: *two statuses would still be two samples, and "unconditional" is a claim
  // about every state that exists and every state added later.* **Only the absence of a condition in the
  // code says it, so that is what is asserted here.** Those tests keep their own value and are Integration;
  // this is gated, and neither subsumes the other.
  //
  // ⚠⚠ THE MECHANISM IS SHARED AND THE CRITERION IS ATTENDANCE'S, which is why this guard lives here while
  // watching a Platform file. `AttendanceRecord` is the ONLY `IAppendOnlyEntity` in the module — a closed
  // population of one, checked rather than assumed — so the criterion's *"any attendance row"* is exactly
  // this one type, and the code that refuses it is `TenantDbContext.PreventAppendOnlyMutation`.
  //
  // ⚠⚠⚠ ***AND THERE ARE TWO METHODS OF THAT NAME. `PlatformDbContext` HAS ONE TOO, AND ONLY IT HAS A
  // GATED BEHAVIOURAL TEST.*** `PlatformAppendOnlyGuardTests` drives the refusal for real — the exact
  // message, the row surviving — and its own header says it *"drive[s] `PlatformDbContext` directly."*
  // **It watches the OTHER class.** Same name, same body shape, different owner, and the tenant one is the
  // one `AttendanceRecord`, `PayrollRunLine` and `JournalLine` all rest on.
  //
  // *Measured, not inferred:* removing `EntityState.Deleted` from the TENANT method leaves delete
  // unrefused and **1,131 `Platform.Tests` pass, including every test in that file.** So the behavioural
  // gate for this class's rule is `Integration` — green at a date — and this source guard is the only
  // gated thing watching it. `TenantAppendOnlyGuardTests` was written to close that; until it existed,
  // this assertion was alone.
  //
  // ⚠ A CORRECTION BELONGS HERE BECAUSE THE COMMIT THAT ADDED THIS TEST STATED ITS FINDING ON A VOID
  // PLANT AND A COMMIT MESSAGE CANNOT BE AMENDED. That plant ADDED a condition — `periodClosed` — which
  // was true exactly when there was something to refuse, so it changed no behaviour, and its green proved
  // only that a source guard reads source. **The conclusion survived re-derivation on a SUBTRACTIVE plant;
  // the original reasoning did not.** *Prefer subtractive plants: a removal cannot be tautological.*
  //
  // ⚠⚠⚠ THE BAN LIST IS THE WEAK HALF AND IT IS PLACED SECOND DELIBERATELY. Naming `Period`, `Status`,
  // `Closed` catches the conditions somebody would plausibly add and cannot catch one nobody thought of.
  // **The load-bearing assertion is the first: the walk is over `Entries<IAppendOnlyEntity>()` with no
  // narrowing, and both `Modified` and `Deleted` are refused.** A guard narrowed to one state or one type
  // fails there regardless of what the new condition is called.
  [Fact]
  [Trait("Criterion", "AC-ATT-0015")]
  public void The_append_only_refusal_consults_nothing_but_the_entity_state()
  {
    var source = SolutionCode(
      "Platform", "SSAS.Platform.Infrastructure", "Persistence", "TenantErp", "TenantDbContext.cs");

    // MATCHER CONTROL: a source guard's failure mode is reading the wrong file and finding nothing.
    Assert.Contains("PreventAppendOnlyMutation", source, StringComparison.Ordinal);

    // Declared once, called once. An exact count is what notices the call being dropped from the save path
    // — which would leave every assertion below true of a method nobody runs.
    Assert.Equal(2, CountOccurrences(source, "PreventAppendOnlyMutation"));

    var start = source.IndexOf("private void PreventAppendOnlyMutation()", StringComparison.Ordinal);
    Assert.True(start >= 0, "PreventAppendOnlyMutation is no longer declared under that name.");

    var body = source[start..];
    body = body[..(body.IndexOf("\n  }", StringComparison.Ordinal) + 4)];

    // ---- THE POPULATION IS EVERY APPEND-ONLY ENTITY, NARROWED BY NOTHING.
    Assert.Contains("ChangeTracker.Entries<IAppendOnlyEntity>()", body, StringComparison.Ordinal);

    // ---- AND BOTH MUTATIONS ARE REFUSED. A guard that dropped `Deleted` would still pass a modify test.
    Assert.Contains("EntityState.Modified", body, StringComparison.Ordinal);
    Assert.Contains("EntityState.Deleted", body, StringComparison.Ordinal);

    // ---- IT CONSULTS NO STATE OF ITS OWN. Each of these would make the refusal conditional, and the
    // criterion's whole point is that no code path may assume it is.
    foreach (var condition in new[] { "Period", "Status", "Closed", "Permission", "Role", "Tenant" })
    {
      Assert.DoesNotContain(condition, body, StringComparison.Ordinal);
    }
  }

  // ---- ⚠⚠⚠ THE ATTENDANCE MIGRATION DROPS ONLY ITS OWN TABLES (AC-ATT-0037).
  //
  // *"The migration is produced by `tools/SSAS.Tenant.MigrationTool` and contains **no `DROP` statement**
  // for any other module's table."*
  //
  // A tenant migration's `Down` drops what its `Up` created. **The hazard is a `Down` that reaches past its
  // own feature** — EF writes what the model diff tells it to, and a model composed wrongly at design time
  // produces a migration that tears down another module's tables on a rollback. *That is not a hypothetical
  // shape: this context is COMPOSED from every module, which is exactly why the tool exists.*
  //
  // ---- ⚠⚠ TWO VACUITY ROUTES, AND FIXING THE FIRST DOES NOT FIX THE SECOND.
  //
  // **(1) THE ARTEFACT MIGHT NOT BE READ.** A scan that opens nothing satisfies *"contains no DROP"*
  // perfectly. A file-exists floor is not enough — it proves a file was opened, not that it was the right
  // one — so the migration is located BY ITS CONTENT and a known-positive is asserted from inside it.
  //
  // **(2) THE FORBIDDEN SET MIGHT BE EMPTY.** *"Every dropped table is an attendance table"* is true of a
  // migration that drops nothing at all, and a `Down` that dropped nothing would be a different defect
  // wearing this test's green. **A floor alone would answer that and still miss a `Down` that drops SOME of
  // what it created.**
  //
  // ***SO THE TWO SETS ARE BOUND TO EACH OTHER RATHER THAN EACH FLOORED SEPARATELY: WHAT `Up` CREATES AND
  // WHAT `Down` DROPS MUST BE THE SAME SET.*** A drop that reaches another module fails it, a drop that is
  // missing fails it, and neither can pass by the set being empty — because the created set is non-empty
  // and the two must agree. *Cross-population bind, where a floor was the obvious answer.*
  [Fact]
  [Trait("Criterion", "AC-ATT-0037")]
  public void The_attendance_migration_drops_only_the_tables_it_created()
  {
    var migrations = Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure",
      "Persistence", "TenantErp", "Migrations");

    // Located by CONTENT, not by file name: a rename would otherwise silently empty this population, and
    // the `.Designer` and snapshot files must not be mistaken for the migration itself.
    var file = Directory.EnumerateFiles(migrations, "*.cs")
      .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
      .Single(path => File.ReadAllText(path).Contains(
        "name: \"AttendanceRecords\"", StringComparison.Ordinal));

    var source = File.ReadAllText(file);

    // KNOWN-POSITIVE FROM INSIDE THE ARTEFACT. Proves the right file was read and that the matcher below
    // parses what this file actually contains, rather than passing over an unreadable one.
    Assert.Contains("migrationBuilder.CreateTable(", source, StringComparison.Ordinal);

    var created = TableNamesAfter(source, "migrationBuilder.CreateTable(");
    var dropped = TableNamesAfter(source, "migrationBuilder.DropTable(");

    // The population is real, and it is the CREATED set that establishes it — so the bind below cannot be
    // satisfied by two empty sets agreeing with each other.
    Assert.NotEmpty(created);

    // ---- THE CLAUSE. Nothing dropped that was not created here, and nothing created left undropped.
    Assert.Equal(created, dropped);

    // ---- AND EVERY ONE OF THEM IS THIS MODULE'S. The bind alone would pass a migration that created and
    // dropped somebody else's table symmetrically, which is the exact act the criterion forbids.
    Assert.All(dropped, name =>
      Assert.StartsWith("Attendance", name, StringComparison.Ordinal));
  }

  // ---- ⚠⚠⚠ THE TOOL IS THE PRODUCER, AND THE CHECKABLE FORM IS ITS CONTRIBUTOR LIST.
  //
  // ***MY FIRST ATTEMPT AT THIS CLAUSE ASSERTED THAT THE TOOL HOLDS THE ONLY DESIGN-TIME FACTORY. IT DOES
  // NOT, AND THE TEST IS WHAT TOLD ME.*** `TenantDbContextDesignTimeFactory` also exists, in
  // `SSAS.Platform.Infrastructure`, and it is CORRECT: `ADR-018` publishes
  // `dotnet ef ... --context TenantDbContext` as operational procedure and that factory serves it.
  // *Two factories, both legitimate, and which one `dotnet ef` uses is a property of the command somebody
  // types — not of the repository.*
  //
  // ⚠⚠ SO CLAUSE 1 AND CLAUSE 2 ARE ONE MECHANISM, WHICH THE TOOL'S OWN HEADER MEASURES. Scaffolding
  // through Platform's factory does not produce an empty migration — **it produces an `Up` of 32
  // `DropTable` covering the whole of HR, Finance/GL, Payroll and Attendance**, because those tables exist
  // in the database and in no model. *"Produced by the tool" IS "no DROP for another module's table"; the
  // criterion names a cause and an effect and they are the same fact.*
  //
  // ⚠⚠⚠ AND THE PART THAT CAN ROT IS THE CONTRIBUTOR LIST. The factory's own comment states the stakes:
  // *"A module that is not named here does not appear in a migration."* **A module shipping a contributor
  // and forgetting to register it gets its tables DROPPED by the next scaffold** — the destructive outcome,
  // reached by omission rather than by using the wrong tool.
  //
  // ***SO THE TWO POPULATIONS ARE BOUND: EVERY `ITenantModelContributor` IN THE SOLUTION MUST BE NAMED IN
  // THE FACTORY.*** Reflection finds the implementations, the factory's source names the registered ones,
  // and neither list can drift without this failing. *A floor on either would pass a module that shipped a
  // contributor nobody registered, which is the whole hazard.*
  // Every module that contributes tenant entities. Named rather than reflected, following the factory's own
  // ruling that module discovery is explicit — a module missing from BOTH this list and the factory would
  // otherwise agree with itself and pass.
  private static readonly string[] ModuleInfrastructureAssemblies =
  [
    "SSAS.HR.Infrastructure", "SSAS.GL.Infrastructure",
    "SSAS.Payroll.Infrastructure", "SSAS.Attendance.Infrastructure"
  ];

  [Fact]
  [Trait("Criterion", "AC-ATT-0037")]
  public void Every_tenant_model_contributor_is_registered_with_the_migration_tool()
  {
    var implemented = ModuleInfrastructureAssemblies
      .SelectMany(name => Assembly.Load(name).GetTypes())
      .Where(type => type.IsClass && !type.IsAbstract)
      .Where(type => type.GetInterfaces().Any(contract => contract.Name == "ITenantModelContributor"))
      .Select(type => type.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // The walk found contributors. Without this the comparison below is two empty lists agreeing.
    Assert.NotEmpty(implemented);

    // ---- ⚠⚠⚠ COMMENTS STRIPPED, AND A PLANT IS WHY.
    //
    // A first version read the file raw. **Commenting a contributor out — the realistic way somebody
    // disables one — left `new AttendanceTenantModelContributor()` in the text and this test passed the
    // plant.** *The registration was gone from the array and present to the matcher.*
    //
    // This file already carried a comment-stripping reader, three hundred lines up, written for exactly
    // this hazard. **I did not use it.**
    var factory = StripComments(File.ReadAllText(Path.Combine(
      RepositoryRoot(), "tools", "SSAS.Tenant.MigrationTool", "ComposedTenantDbContextFactory.cs")));

    // KNOWN-POSITIVE FROM INSIDE THE ARTEFACT, so a moved or renamed file fails here rather than passing
    // with an empty registration set.
    Assert.Contains("ITenantModelContributor[] Contributors", factory, StringComparison.Ordinal);

    var registered = implemented
      .Where(name => factory.Contains($"new {name}()", StringComparison.Ordinal))
      .ToArray();

    Assert.Equal(implemented, registered);
  }

  // Reads the `name:` argument that follows each occurrence of a migration-builder call.
  private static string[] TableNamesAfter(string source, string call)
  {
    var names = new List<string>();

    for (var i = source.IndexOf(call, StringComparison.Ordinal); i >= 0;
      i = source.IndexOf(call, i + call.Length, StringComparison.Ordinal))
    {
      var marker = source.IndexOf("name: \"", i, StringComparison.Ordinal);
      if (marker < 0)
      {
        continue;
      }

      var start = marker + "name: \"".Length;
      names.Add(source[start..source.IndexOf('"', start)]);
    }

    return [.. names.Distinct().OrderBy(name => name, StringComparer.Ordinal)];
  }

  private static string StripComments(string source) =>
    string.Join(
      Environment.NewLine,
      source.Split('\n').Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
      }));

  private static string SolutionCode(params string[] segments)
  {
    var path = Path.Combine(new[] { RepositoryRoot(), "src" }.Concat(segments).ToArray());

    Assert.True(File.Exists(path), $"Source not found: {path}");

    return string.Join(
      Environment.NewLine,
      File.ReadAllText(path).Split('\n').Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
      }));
  }

  private static int CountOccurrences(string source, string term)
  {
    var count = 0;
    for (var i = source.IndexOf(term, StringComparison.Ordinal); i >= 0;
      i = source.IndexOf(term, i + term.Length, StringComparison.Ordinal))
    {
      count++;
    }

    return count;
  }

  // Comments are stripped before matching. This tree's comments QUOTE the very phrases asserted above —
  // the source's own note explains why the refusal throws — and a raw matcher would credit the prose.
  private static string AttendanceCode(params string[] segments)
  {
    var path = Path.Combine(
      new[] { RepositoryRoot(), "src", "Modules", "Attendance" }.Concat(segments).ToArray());

    Assert.True(File.Exists(path), $"Source not found: {path}");

    return string.Join(
      Environment.NewLine,
      File.ReadAllText(path).Split('\n').Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
      }));
  }

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }

  // ---- ⚠⚠⚠ A RECORD STAYS READABLE AFTER THE EMPLOYEE LEAVES, BECAUSE NO READER CAN LEARN THEY LEFT.
  //
  // `AC-ATT-0009` — *"A record already settled by termination remains readable after termination —
  // `BR-HR-0004` bars new obligations, not the settlement of existing ones."* The write path enforces the
  // first half of that reading and `AttendanceRecordEmploymentWindowTests` drives it. **This is the second
  // half, and it is an ABSENCE: nothing on the read path may filter a record out because its employee was
  // terminated.**
  //
  // ⚠ WHY A DEPENDENCY ASSERTION RATHER THAN A BEHAVIOURAL ONE. Recording, terminating and re-reading needs
  // a database, which puts it in Integration — currently unrunnable here, and an unrun assertion is a claim
  // rather than a check. **The structural statement is available at the gate and is stronger in one
  // specific way: it forbids the filter from being WRITABLE, not merely absent today.**
  //
  // ⚠⚠ THE ENFORCEMENT SET WAS ENUMERATED RATHER THAN ASSUMED. The read path is three types, and none can
  // reach an employment date:
  //
  //   `AttendanceReadService`               a context accessor and a scope resolver
  //   `AttendanceScopeResolver`             company and branch ACCESS, plus current-actor services
  //   `AttendanceSelfServiceScopeResolver`  a user-employee link and a PLACEMENT directory
  //
  // ⚠ AND ONE OF THE THREE IS ALREADY HELD BY THE COMPILER, WHICH IS SAID HERE SO THIS GUARD IS NOT READ
  // AS THREE EQUALLY LOAD-BEARING CHECKS. **`SSAS.Attendance.Infrastructure` does not reference
  // `SSAS.HR.Contracts` at all**, so `AttendanceReadService` COULD NOT take the roster even if somebody
  // tried — that leg would fail to compile before it failed here. *The two Application-layer resolvers are
  // the ones this test actually protects*, and the plant that reddens it was applied to one of them.
  // Keeping the read service in the population costs nothing and states the boundary; it just is not where
  // the risk lives.
  //
  // ⚠⚠⚠ AND THE SECOND ASSERTION IS THE ONE THAT SURVIVES A RENAME. `EmploymentRecord`, reached through
  // `IEmployeeRoster`, is the ONLY contract type carrying employment dates — `EmployeePlacement` is two
  // `Guid`s and nothing else. **So banning the roster is only sound while placement stays dateless**, and
  // adding a `TerminationDateUtc` to it would hand the self-service read exactly the fact this criterion
  // says it must not have, without touching a constructor. *That is asserted on the contract type itself,
  // where the change would happen.*
  [Fact]
  [Trait("Criterion", "AC-ATT-0009")]
  public void No_attendance_read_path_can_learn_that_an_employee_was_terminated()
  {
    var readService = typeof(SSAS.Attendance.Application.Reads.AttendanceScopeResolver).Assembly
      .GetType("SSAS.Attendance.Application.Reads.AttendanceSelfServiceScopeResolver");
    Assert.NotNull(readService);

    Type[] readPath =
    [
      Assembly.Load("SSAS.Attendance.Infrastructure")
        .GetType("SSAS.Attendance.Infrastructure.Persistence.AttendanceReadService")!,
      typeof(SSAS.Attendance.Application.Reads.AttendanceScopeResolver),
      readService!
    ];

    // The population is named, so it cannot silently shrink — but assert it anyway, because a `null` from
    // either lookup above would otherwise reach the loop as a hole rather than as a failure.
    Assert.All(readPath, type => Assert.NotNull(type));

    // ---- NONE OF THEM CAN ASK FOR AN EMPLOYMENT WINDOW.
    foreach (var type in readPath)
    {
      var constructor = Assert.Single(type.GetConstructors());

      Assert.DoesNotContain(
        constructor.GetParameters(),
        parameter => parameter.ParameterType == typeof(SSAS.HR.Contracts.Employment.IEmployeeRoster));
    }

    // ---- AND THE ONE HR CONTRACT THE READ PATH DOES USE CARRIES NO DATE.
    //
    // Asserted as "every property is a `Guid`" rather than "no property is a date": a ban names the shapes
    // it thought of, and this criterion is broken by ANY employment fact arriving here, not only by a
    // `DateTimeOffset`.
    Assert.All(
      typeof(SSAS.HR.Contracts.Employment.EmployeePlacement).GetProperties(),
      property => Assert.Equal(typeof(Guid), property.PropertyType));
  }

  //
  // ⚠ CITES `AC-ATT-0031` CLAUSE 2 — *"and the Payroll summary contract applies no branch predicate at all
  // — company-complete by design, guard-asserted."* **The criterion names the instrument, and the
  // instrument was sitting here carrying only its ruling.** Clause 1 — *"a caller sees only their
  // authorized, active branches on record reads, resolved live"* — is
  // `AttendanceScopeResolverTests.The_branch_authority_is_consulted_on_every_resolution`.
  //
  // ⚠⚠ THE TWO CLAUSES ARE OPPOSITE OBLIGATIONS AND THAT IS WHY THE CRITERION IS ONE ROW. Record reads
  // must NARROW to the caller's branches; the summary must NOT narrow at all. *A single reader tidying
  // toward consistency would break exactly one of them*, which is the split the ruling exists to record.
  [Fact]
  [Trait("Decision", "OD-ATT-0011")]
  [Trait("Criterion", "AC-ATT-0031")]
  public void The_payroll_summary_contract_applies_no_branch_predicate()
  {
    var service = typeof(IAttendanceSummary).Assembly
      .GetType("SSAS.Attendance.Contracts.Summaries.IAttendanceSummary");
    Assert.NotNull(service);

    // The CONTRACT itself must not even be able to express a branch: no method takes one, and no result
    // record carries one. A contract that accepted a branch identifier would let a caller narrow the
    // company-complete answer, which is the failure the ruling exists to prevent.
    foreach (var method in typeof(IAttendanceSummary).GetMethods())
    {
      Assert.DoesNotContain(method.GetParameters(), parameter =>
        parameter.Name!.Contains("branch", StringComparison.OrdinalIgnoreCase));
    }

    foreach (var type in new[] { typeof(AttendanceSummaryResult), typeof(AttendancePeriodInspection) })
    {
      Assert.DoesNotContain(type.GetProperties(), property =>
        property.Name.Contains("Branch", StringComparison.OrdinalIgnoreCase));
    }
  }

  // The other half of the same ruling, asserted positively: the contract carries TOTALS and no per-event or
  // time-of-day data (`DEC-ATT-0002`). A contract exposing punch-level movement would let every future
  // Payroll feature read minute-by-minute employee location with no call-site change for anyone to review.
  // ⚠ CITES `AC-ATT-0023` — *"The summary contract returns totals for one employee and one period, and
  // exposes **no punch-level, per-event or time-of-day data**."* **The second clause is what this asserts,
  // by name, over the record's own properties.** The first clause is carried by the contract's SIGNATURE —
  // `GetSummaryAsync` takes one employee and one date inside one period — which the type system enforces
  // and no test needs to restate.
  //
  // ⚠⚠ THE ASSERTION IS A NAME BAN, SO ITS REACH IS THE VOCABULARY IT LISTS. A per-event field named
  // something none of `Punch`, `ClockIn`, `ClockOut`, `TimeOfDay` or `Event` matches would pass — `Movement`
  // or `Swipe`, say. **That is the honest bound of a name-shaped guard and it is why the ban is broad
  // rather than exact.** *The structural alternative — assert the exact property set — exists next door in
  // `EmploymentTypeAssumptionTests`, which pins `AttendanceSummaryResult`'s components exactly; between the
  // two, an added field is caught there and a suspiciously-named one is caught here.*
  [Fact]
  // ⚠⚠ ALSO CITES `AC-ATT-0027` — *"The contract does not disclose leave **type** if `OD-ATT-0013`(3) rules
  // it sensitive — the contract may not be laxer than the module's own HTTP surface."* **The `LeaveType`
  // ban below is that clause**, and the comment beside it already gave the criterion's own reason before
  // any criterion was attached to it.
  //
  // ⚠⚠⚠ AND THE DISTINCTION FROM `AC-ATT-0045`, WHICH IS NOT CITED HERE AND MUST NOT BE READ AS COVERED:
  // this asserts the CONTRACT carries no leave type AT ALL. `0045` is about the module's own HTTP reads —
  // *leave type is not returned on any read a caller holds only `Attendance.Leave.View` for* — which is
  // PER-ROW REDACTION in `AttendanceReadService`, decided at runtime by a permission. **A structural absence
  // and a conditional redaction are different mechanisms; this test reaches only the first.**
  [Trait("Decision", "DEC-ATT-0002")]
  [Trait("Criterion", "AC-ATT-0023")]
  [Trait("Criterion", "AC-ATT-0027")]
  public void The_summary_contract_exposes_totals_and_no_per_event_data()
  {
    var names = typeof(AttendanceSummaryResult).GetProperties().Select(property => property.Name).ToArray();

    Assert.DoesNotContain(names, name =>
      name.Contains("Punch", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("ClockIn", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("ClockOut", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("TimeOfDay", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Event", StringComparison.OrdinalIgnoreCase));

    // And no leave TYPE, because `Attendance.Leave.ViewSensitive` gates it over HTTP. A cross-module
    // contract has no business being laxer than the owning module's own HTTP surface.
    Assert.DoesNotContain(names, name => name.Contains("LeaveType", StringComparison.OrdinalIgnoreCase));

    // No money of any kind (`DEC-ATT-0004`): Attendance records how much, Payroll decides what it is worth.
    Assert.DoesNotContain(names, name =>
      name.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Rate", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Currency", StringComparison.OrdinalIgnoreCase));
  }

  // ================================================================================================
  // MODULE ISOLATION, IN BOTH DIRECTIONS (ADR-012).
  // ================================================================================================
  // ---- ⚠⚠⚠ CITES `AC-ATT-0026`, AND THIS IS THE CRITERION'S PROPER HOME — I CITED IT ELSEWHERE FIRST.
  //
  // *"Payroll consumes the contract **without an assembly reference** to any Attendance implementation
  // project — asserted by an architecture test, not by inspection."*
  //
  // **I cited that on `PayrollArchitectureTests.Payroll_assemblies_reach_other_modules_only_through_contracts`
  // before finding this, and recorded a bound there — *reads EMITTED metadata, so an unused
  // `ProjectReference` is pruned before the test sees it.* THAT BOUND IS FALSE FOR THE CRITERION, because
  // THIS test closes it: it asserts the DECLARED dependencies too, and its plant record shows an unused
  // `SSAS.Attendance.Domain` reference passing the emitted half and reddening the declared one.**
  //
  // ⚠ THE TWO ARE COMPLEMENTARY AND NEITHER SUBSUMES THE OTHER, WHICH IS WHY BOTH KEEP THE CITATION:
  // **this one is EXHAUSTIVE (`Assert.Equal(["SSAS.Attendance.Contracts"])`, not a list of assemblies
  // somebody thought of) and covers DECLARED + EMITTED, but only `SSAS.Payroll.Application`.** The other
  // covers **all four Payroll assemblies** and is emitted-only. *A reference from `SSAS.Payroll.API` would
  // be caught only there; an unused one from Application only here.*
  [Fact]
  [Trait("Decision", "DEC-ATT-0002")]
  [Trait("Criterion", "AC-ATT-0026")]
  public void Payroll_references_the_attendance_contracts_and_no_attendance_implementation()
  {
    var payrollApplication = typeof(SSAS.Payroll.Application.Runs.CalculatePayrollRunCommandHandler).Assembly;

    var attendanceReferences = payrollApplication
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name)
      .Where(name => name is not null && name.StartsWith("SSAS.Attendance", StringComparison.Ordinal))
      .ToArray();

    // Exactly one, and it is the contracts assembly. Not "does not contain Domain" — an exhaustive check,
    // because a future reference to `.Infrastructure` would pass the negative form of this assertion if
    // somebody only listed the assemblies they thought of.
    Assert.Equal(["SSAS.Attendance.Contracts"], attendanceReferences);

    // ================================================================================================
    // ⚠⚠ TWO CLAUSES, ONE INSTRUMENT, AND ONLY THE POSITIVE ONE WAS SAFE (272)
    // ================================================================================================
    //
    // The exact set above carries a POSITIVE clause — *references the contracts* — and a NEGATIVE one —
    // *and no implementation*. **The exact form protects only the first.**
    //
    // `GetReferencedAssemblies()` reads EMITTED metadata, and the compiler omits a reference no type is
    // taken from. ⚠ **PRUNING REMOVES THE FORBIDDEN REFERENCE, NOT THE SANCTIONED ONE.** Add an unused
    // `SSAS.Attendance.Domain` ProjectReference and the emitted set is still exactly
    // `["SSAS.Attendance.Contracts"]` — this passes, unchanged, over a project that can now reach the
    // implementation. The reference is merged, the friction is gone, and the guard says nothing until the
    // first use.
    //
    // I first filed this test as FAIL-SAFE on the reasoning that pruning would empty the set and redden it.
    // That was wrong in the direction that matters, and the declared reading is what closes it.
    //
    // ---- ⚠ BOTH ASSERTIONS STAY. THEY FAIL ON DIFFERENT DAYS.
    //
    // DECLARED catches the capability the moment the `.csproj` merges. EMITTED catches consumption,
    // INCLUDING through a transitive path no `.csproj` of ours names. **Neither subsumes the other**, and
    // two assertions that both mention "references" are exactly what a later tidy-up deletes one of.
    //
    // ---- ⚠⚠ AND THE ORDER OF THE TWO IS LOAD-BEARING FOR THE PLANT, WHICH IS INVISIBLE FROM HERE.
    //
    // The plant that justified this conversion added an UNUSED `SSAS.Attendance.Domain` ProjectReference to
    // `SSAS.Payroll.Application.csproj`. xUnit stops at the first failure, and because the EMITTED
    // assertion runs ABOVE this one it was observed to PASS while the declared assertion below reddened —
    // both halves demonstrated in a single run.
    //
    // **Swap the two and the coverage is identical but that demonstration is gone**: the declared failure
    // would mask the emitted one, and nobody could show the emitted check is blind to an unused reference
    // without a second run. A tidy-up that reorders these loses nothing at runtime and destroys the
    // evidence, which is why the order is written down rather than left to look arbitrary.
    var declared = DeclaredDependencies.Of(payrollApplication)
      .Where(name => name.StartsWith("SSAS.Attendance", StringComparison.Ordinal))
      .ToArray();

    // The predicate matches an implementation reference where one legitimately exists, so the exact set
    // below is not satisfied by a parse that recognises no Attendance project at all.
    Assert.Contains(
      DeclaredDependencies.Of("SSAS.Attendance.Infrastructure"),
      name => name.StartsWith("SSAS.Attendance.Domain", StringComparison.Ordinal));

    Assert.Equal(["SSAS.Attendance.Contracts"], declared);
  }

  [Fact]
  [Trait("Decision", "DEC-ATT-0003")]
  public void Attendance_references_the_hr_contracts_and_no_hr_implementation()
  {
    var attendanceApplication = typeof(SSAS.Attendance.Application.Approval.LeaveApprovalRouter).Assembly;

    var hrReferences = attendanceApplication
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name)
      .Where(name => name is not null && name.StartsWith("SSAS.HR", StringComparison.Ordinal))
      .ToArray();

    Assert.Equal(["SSAS.HR.Contracts"], hrReferences);
  }

  // `DEC-ATT-0003`: Attendance reads HR facts through a contract and NEVER writes HR. Asserted on the
  // contracts themselves, because a mutating method added later would be the one change nobody would read
  // as a boundary violation.
  [Fact]
  [Trait("Decision", "DEC-ATT-0003")]
  public void The_hr_contracts_attendance_consumes_are_read_only()
  {
    foreach (var contract in new[]
      {
        typeof(SSAS.HR.Contracts.Employment.IEmployeeRoster),
        typeof(SSAS.HR.Contracts.Employment.IEmployeeApproverDirectory)
      })
    {
      foreach (var method in contract.GetMethods())
      {
        Assert.StartsWith("Get", method.Name, StringComparison.Ordinal);
      }
    }
  }

  // ---- THE SELF-APPROVAL BAR LIVES IN ATTENDANCE, NOT IN HR (OD-ATT-0007).
  //
  // `IEmployeeApproverDirectory` returns the chain and applies HR's facts. If HR filtered the requester out,
  // `BR-ATT-0007` would live in two modules and could drift — with the module that owns the rule not being
  // the module enforcing it. So the contract must NOT take the requester as something to exclude.
  [Fact]
  [Trait("Decision", "OD-ATT-0007")]
  public void The_approver_directory_returns_the_chain_and_does_not_apply_attendance_policy()
  {
    var method = typeof(SSAS.HR.Contracts.Employment.IEmployeeApproverDirectory)
      .GetMethod(nameof(SSAS.HR.Contracts.Employment.IEmployeeApproverDirectory.GetApproverChainAsync))!;

    // Company, employee, cancellation token. No "exclude" parameter, no policy flag.
    Assert.DoesNotContain(method.GetParameters(), parameter =>
      parameter.Name!.Contains("exclude", StringComparison.OrdinalIgnoreCase) ||
      parameter.Name!.Contains("requester", StringComparison.OrdinalIgnoreCase));
  }

  // ================================================================================================
  // THE READ SCOPE IS UNFORGEABLE (DEC-ATT-0008, AC-ATT-0030).
  // ================================================================================================
  //
  // Private constructor, internal factory. Holding one is proof that Attendance's permission check and
  // Attendance's company AND branch resolution all ran against live state — a scope a caller could construct
  // would make that proof a shrug.
  //
  // ⚠ CITES `AC-ATT-0030` — *"The read scope cannot be constructed outside its factory — private
  // constructor, internal factory, asserted by an architecture test."* **All three clauses, including the
  // last, which names the instrument rather than the property.** The ban below passes over an empty set,
  // which is the compliant state here rather than a vacuity: the walk is rooted in a NAMED TYPE, so it
  // cannot silently return nothing — delete the type and this stops compiling. The factory assertions are
  // positive and fail if it is removed or widened.
  [Fact]
  [Trait("Decision", "DEC-ATT-0008")]
  [Trait("Criterion", "AC-ATT-0030")]
  public void The_attendance_read_scope_cannot_be_constructed_outside_its_factory()
  {
    var scope = typeof(SSAS.Attendance.Application.Reads.AttendanceReadScope);

    Assert.All(
      scope.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
      constructor => Assert.Fail($"Public constructor found: {constructor}"));

    var factory = scope.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(factory);
    Assert.True(factory!.IsAssembly);
  }

  // The first three-dimensional read scope in the product. The branch set is a distinct member from the
  // company set, so a query cannot accidentally filter on the wrong one.
  [Fact]
  [Trait("Decision", "OD-ATT-0011")]
  public void The_attendance_read_scope_carries_tenant_company_and_branch()
  {
    var scope = typeof(SSAS.Attendance.Application.Reads.AttendanceReadScope);
    var names = scope.GetProperties().Select(property => property.Name).ToArray();

    Assert.Contains("TenantId", names);
    Assert.Contains("CompanyIds", names);
    Assert.Contains("BranchIds", names);
  }

  // ================================================================================================
  // THE SELF-SERVICE PERMISSIONS ARE EXACTLY TWO (AC-ATT-0032, OD-ATT-0013).
  // ================================================================================================
  //
  // ---- WHAT THIS GUARD USED TO ASSERT, AND WHY IT DID NOT SIMPLY GET DELETED (T-089).
  //
  // It asserted that NO permission name contained `Own`, `Self` or `Mine`. That absence was correct for as
  // long as self-service was deferred — first because the identity-to-employee mapping did not exist, then,
  // after T-082 built it, because FP-015's endpoint did not.
  //
  // **FP-015 has now landed, so the absence is false. The question the guard answers is not.** It was never
  // really *"is there a self permission"*; it was *"has a self-service surface appeared without a person
  // deciding its shape"*. **An absence check answers that only until the first one is added, and then it is
  // deleted and answers nothing ever again.** An exact inventory keeps answering it: the THIRD one still
  // needs a person, exactly as the first two did.
  //
  // `DEC-L-072`, applied on the day the door opened — the same move T-088 made for the placement directory's
  // injection set, and for the same reason.
  //
  // ---- AND WHY THE INVENTORY IS TWO RATHER THAN ONE.
  //
  // The administrative plane permissions records and leave separately (`Attendance.Records.View` versus
  // `Attendance.Leave.View`) because a timesheet and a leave history disclose different things. **A single
  // `Attendance.ViewOwn` would be a WIDENING wearing the costume of a simplification:** granting one's own
  // attendance would silently grant one's own leave.
  //
  // **Pinning both names is what makes a later collapse into one a visible decision** rather than a
  // simplification nobody reviewed. `TS-SS-0013` asserts the runtime half — that neither substitutes for
  // the other at an endpoint — and this asserts the catalog half.
  //
  // **The durable handle is `AC-ATT-0032`, not this method name.** `AC-` identifiers survive refactors and
  // are what the traceability matrix and specification prose cite; a test name changes whenever the thing
  // it describes does, which is exactly what happened here, twice.
  [Fact]
  [Trait("Criterion", "AC-ATT-0032")]
  [Trait("Decision", "OD-ATT-0013")]
  public void The_self_service_permissions_are_exactly_the_two_that_were_decided()
  {
    var constants = typeof(SSAS.Attendance.Application.Permissions.AttendancePermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral)
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();

    string[] expectedSelfService = ["Attendance.Leave.ViewOwn", "Attendance.Records.ViewOwn"];

    var actualSelfService = constants
      .Where(name =>
        name.Contains("Own", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Self", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Mine", StringComparison.OrdinalIgnoreCase))
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(expectedSelfService, actualSelfService);

    // And the catalog contributor defines exactly the constants that exist — no more, no fewer. A
    // permission an endpoint requires but the catalog omits refuses every caller, which is FP-006P's
    // incident; the reverse is a grant nothing checks.
    var contributor = new SSAS.Attendance.Application.Permissions.AttendancePermissionCatalogContributor();
    var defined = contributor.Permissions.Select(permission => permission.Name).OrderBy(n => n, StringComparer.Ordinal);

    Assert.Equal(constants.OrderBy(n => n, StringComparer.Ordinal), defined);
  }

  // The permission grammar. Every name is exactly `<Plane>.<Resource>.<Action>` with the plane fixed —
  // a malformed one would still compile and would simply never match a grant.
  [Fact]
  public void Every_attendance_permission_follows_the_three_part_grammar()
  {
    var contributor = new SSAS.Attendance.Application.Permissions.AttendancePermissionCatalogContributor();

    foreach (var permission in contributor.Permissions)
    {
      var parts = permission.Name.Split('.');
      Assert.Equal(3, parts.Length);
      Assert.Equal("Attendance", parts[0]);
      Assert.All(parts, part => Assert.False(string.IsNullOrWhiteSpace(part)));
    }
  }
}
