using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Positions;
using SSAS.TestSupport.CutoverModel;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY POSITION-FAMILY READ IS SCOPED, AND THERE IS AN INDEX FOR IT (274, `AC-POS-0051`).
// ==================================================================================================
//
// `AC-POS-0051` has two halves and they are different KINDS of claim, which is why one file carries both
// and neither is honest alone:
//
//   * *an index whose leading keys are tenant then company exists for every scoped read path* — a claim
//     about the MODEL, asserted below against the composed EF model.
//   * *every scoped read composes an explicit tenant and company predicate rather than relying on a global
//     filter* — a claim about the SOURCE, asserted below by reading it.
//
// An index without a predicate is a scoped read waiting to be written unscoped; a predicate without an
// index is a correct answer produced by a scan. The criterion wants both and neither implies the other.
//
// ---- ⚠ WHY THIS FILE EXISTS AT ALL: THREE OF FOUR WAS NOT A DECISION.
//
// `DepartmentReadScopeArchitectureTests`, `EmployeeReadScopeArchitectureTests` and
// `GlReadScopeArchitectureTests` all guard exactly this rule for their own families. **The position family
// had none.** That is the same shape as the Platform-reference guard covering three HR assemblies and not
// the fourth: whoever wrote the others understood the rule, so the gap is an omission rather than a ruling.
//
// ---- ⚠⚠ AND THE CRITERION WAS REWORDED TO MAKE THIS ASSERTABLE, WHICH IS THE INTERESTING PART.
//
// It read *every scoped read is SERVED BY an index … no scoped read is served by a scan*. **That is a
// QUERY-PLAN claim**, and a plan assertion is brittle, environment-dependent and breaks on statistics —
// this repository asserts plans nowhere and has already removed one allocation-budget assertion in favour
// of a structural guard. Index-existence plus predicate-shape is what the criterion actually wanted, and
// unlike the plan version it is true or false at compile time.
//
// ---- WHAT THIS IS NOT.
//
// AN ARCHITECTURE GUARD ASSERTS THE SHAPE OF A QUERY. IT NEVER ASSERTS THAT THE SHAPE WAS APPLIED AT
// RUNTIME. The predicate half reads source text: it proves the predicate is WRITTEN, not that the resulting
// SQL filtered any row. A total, cheap, entirely passing scope guard of exactly this kind sat above two
// read services that were broken, because no test ever CONSTRUCTED them. The behavioural cover is the SQL
// suite's business and this file is not evidence about it.
//
// ---- COMMENTS ARE STRIPPED BEFORE ANYTHING IS ASSERTED.
//
// ⚠ `PositionReadService` opens with a comment containing `TenantId = @tenant AND CompanyId IN
// (@companies)`. A guard reading raw text could be satisfied by that PROSE while the query below it
// composed nothing — the same mirror the department and GL guards both strip against.
public sealed class PositionReadScopeArchitectureTests
{
  // The three entities whose reads carry a `PositionReadScope`, `JobGradeReadScope` or
  // `SalaryGradeReadScope`. `EmployeePositionAssignment` is deliberately absent: it is append-only history
  // read through the employee side, and its scope story belongs to `EmployeeReadScopeArchitectureTests`.
  public static TheoryData<Type> ScopedEntities() => new() { typeof(Position), typeof(JobGrade), typeof(SalaryGrade) };

  // Both read-service files. `GradeReadServices.cs` holds TWO services, which is why this is a file list
  // rather than a one-service constant like the department guard's.
  private static readonly string[] ReadServices = ["PositionReadService.cs", "GradeReadServices.cs"];

  private static readonly string[] EntitySets =
    ["Set<Position>()", "Set<JobGrade>()", "Set<SalaryGrade>()"];

  // ---- HALF ONE: THE INDEX, FROM THE COMPOSED MODEL.
  //
  // Read from the model rather than from `sys.indexes` for the reason `CutoverManifestArchitectureTests`
  // gives: `GATE_SCOPE=TASK` never runs Integration, so an invariant asserted only there is not checked
  // during ordinary development. The composed model is also the surface where a CONVENTION-supplied index
  // would appear, which a migration-file read would miss.
  //
  // ⚠⚠ THE ASSERTION IS EXISTENTIAL, AND I WROTE THE UNIVERSAL FIRST AND WAS WRONG. Reading the nine
  // `HasIndex` calls in the three configurations, every declared index leads tenant then company, so
  // *every index complies* looked strictly stronger and free. **The composed model holds MORE indexes than
  // the configurations declare:** EF supplies one per foreign key by convention, and all three entities
  // carry a single-column `CompanyId` index nothing in `src/` asks for. The universal failed on convention
  // output that is entirely correct.
  //
  // ⚠⚠⚠ AND THE CONVENTION SET IS NOT EVEN STABLE — IT DEPENDS ON THE DECLARED INDEXES. EF omits the
  // convention index when a declared one already leads with that foreign key's columns. The plant for this
  // test dropped `TenantId` from Position's three declared indexes; the single-column `CompanyId` index
  // then DISAPPEARED, because the declared ones had started leading with `CompanyId` themselves. **So the
  // index population is a function of the configuration rather than a list beside it**, which is the
  // strongest argument for reading the model here and the reason no hand-written expected set belongs in
  // this file.
  //
  // ⚠ THE ERROR IS THE ONE THIS SWEEP KEEPS FINDING: I enumerated the population by NAME — the `HasIndex`
  // calls I could grep — and asserted a universal over the MECHANISM's output, which is a superset. The
  // model is the instrument precisely because it sees what the source does not say.
  //
  // So this asserts what the criterion asserts: an index whose leading keys are tenant then company EXISTS
  // for each scoped entity. Primary keys are not in `GetIndexes()` and convention FK indexes are, which is
  // why the existential is the honest form rather than a weakened one.
  [Theory]
  [MemberData(nameof(ScopedEntities))]
  [Trait("Decision", "ADR-025")]
  [Trait("Criterion", "AC-POS-0051")]
  public void Every_position_family_index_leads_with_tenant_then_company(Type entityClrType)
  {
    var entity = CutoverTenantModel.Source.Model.FindEntityType(entityClrType);

    // ANTI-VACUITY, BOTH LEGS. A renamed or uncontributed entity makes `FindEntityType` null, and an entity
    // with no indexes makes every assertion below hold over an empty set — the second is the one that would
    // pass silently.
    Assert.True(entity is not null, $"{entityClrType.Name} is not in the composed model at all");

    var indexes = entity!.GetIndexes().ToArray();

    Assert.True(
      indexes.Length > 0,
      $"{entityClrType.Name} carries no indexes in the composed model, so the rule below asserts nothing");

    // Bound to the interface members rather than to two bare strings. `258` fixed exactly this class of
    // lookup elsewhere and its population never reached `Architecture.Tests`; a misspelt literal here would
    // make this find nothing and report a missing index that is present.
    var leading = new[] { nameof(ITenantOwnedEntity.TenantId), nameof(ICompanyOwnedEntity.CompanyId) };

    var shapes = indexes
      .Select(index => string.Join(",", index.Properties.Select(property => property.Name)))
      .OrderBy(shape => shape, StringComparer.Ordinal)
      .ToArray();

    // The failure NAMES every index the entity has. A bare "no scoped index" would send the next reader to
    // the configuration to work out what is there instead, which is the lookup the message can just do.
    Assert.True(
      indexes.Any(index => index.Properties.Select(property => property.Name).Take(2).SequenceEqual(leading)),
      $"{entityClrType.Name} has no index leading {string.Join(",", leading)}. Its indexes are: " +
      $"{string.Join(" | ", shapes)}. Every scoped read filters on tenant and company first, so without " +
      "one of these the read is correct and unservable.");
  }

  // ---- HALF TWO: THE PREDICATE, FROM THE SOURCE.
  //
  // ⚠ EVERY OCCURRENCE IS CHECKED, NOT ONE ENTRY POINT. The department guard asserts `Set<Department>()`
  // appears exactly once and then checks the single `Scoped` helper. **That shape does not hold here and
  // assuming it would have hidden a real second reader:** `PositionReadService` reaches `Set<JobGrade>()`
  // directly for its grade block, in addition to `GradeReadServices`' own `Scoped`. That block composes its
  // own explicit tenant and company predicate and is correct — so the rule is *every reach is scoped*,
  // which is what the criterion says and is strictly stronger than *there is only one reach*.
  [Theory]
  [MemberData(nameof(ReadServiceSources))]
  [Trait("Decision", "ADR-025")]
  [Trait("Criterion", "AC-POS-0051")]
  public void Every_position_family_read_composes_an_explicit_tenant_and_company_predicate(
    string fileName, string source)
  {
    var reaches = EntitySets.Sum(entitySet => CountOccurrences(source, entitySet));

    Assert.True(
      reaches > 0,
      $"{fileName} reaches no position-family entity set — the derivation has stopped matching and this " +
      "guard is asserting nothing rather than passing");

    foreach (var entitySet in EntitySets)
    {
      for (var index = source.IndexOf(entitySet, StringComparison.Ordinal);
        index >= 0;
        index = source.IndexOf(entitySet, index + entitySet.Length, StringComparison.Ordinal))
      {
        // The window is generous rather than tight: the predicate follows the entity set within a few
        // lines in every current reach, and a window that had to be tuned per call site would be a filter
        // shaped to the code it is checking. A reach whose predicate sits further away than this fails,
        // and moving it further away is exactly the change worth being told about.
        var window = source[index..Math.Min(source.Length, index + 600)];

        Assert.True(
          window.Contains("TenantId == scope.TenantId", StringComparison.Ordinal),
          $"{fileName}: a {entitySet} reach composes no explicit tenant predicate");

        Assert.True(
          window.Contains("scope.Companies.CompanyIds.Contains", StringComparison.Ordinal),
          $"{fileName}: a {entitySet} reach composes no explicit company predicate");
      }
    }
  }

  // ---- AND NOTHING TURNS THE FILTERS IT DOES HAVE BACK OFF.
  //
  // The population here is DELIBERATELY WIDER than the two read services: a repository calling
  // `IgnoreQueryFilters` on a position-family set is the same defect arriving through the write side, and
  // scoping this ban to the read services would exempt the files most likely to do it for a good reason.
  //
  // ==================================================================================================
  // ⚠⚠⚠ THE TWO TESTS IN THIS FILE ARE OPEN TO THE CARELESS READ AND CLOSED TO THE DELIBERATE ONE.
  // ==================================================================================================
  //
  // Measured 2026-09-06, both directions, whole gate each time. An HR production file that is NEITHER
  // `PositionReadService.cs` NOR `GradeReadServices.cs`, reaching `Set<Position>()` with no company
  // predicate:
  //
  //     the ordinary read                    -> [GATE GREEN]  -- nothing caught it
  //     the same file + `IgnoreQueryFilters` -> [GATE RED]    -- caught, by THIS test, by name
  //
  // ***THE UNION OF THE TWO TESTS COVERS THE DELIBERATE BYPASS EVERYWHERE IN HR, AND THE MISSING
  // PREDICATE ONLY IN TWO NAMED FILES.*** The predicate test above is filtered to `ReadServices`; this
  // one enumerates all of `src/Modules/HR` but matches only the `IgnoreQueryFilters` token. So the gap is
  // a real read, in HR, that simply forgets the predicate — and forgetting is what an author DOES, while
  // `IgnoreQueryFilters` has to be typed on purpose.
  //
  // ⚠ **THE COVERAGE IS ANTI-CORRELATED WITH THE LIKELIHOOD**, and the two comments explaining the two
  // populations are each locally true, which is why reading the file top-to-bottom is reassuring. The
  // mechanism-derived test is the one with the NARROW trigger; the name-filtered test is the one with the
  // BROAD trigger. Each is the opposite of what its own derivation suggests.
  //
  // ---- AND THE SAME MEASUREMENT AT A SECOND ADDRESS, WHICH IS WHY WIDENING `ReadServices` IS NOT THE FIX.
  //
  // `PositionFamilyPaths()` enumerates `src/Modules/HR`, so BOTH tests are blind to the whole of
  // `SSAS.Host.API` — which references `SSAS.HR.Infrastructure` and can therefore reach these sets. A real
  // `Set<Position>()` read placed there was GREEN with the bypass AND green without it. `Department` and
  // `Employee` behave identically at that address, each plant-verified separately.
  //
  // No remedy is asserted here because the remedy is a scope decision, not a test edit: widening the
  // predicate population would redden files that legitimately compose no predicate, and widening the
  // directory would put a Host assembly inside a module guard. Recorded so the next reader starts from
  // the measurement rather than from the reassuring impression.
  [Fact]
  [Trait("Decision", "ADR-025")]
  [Trait("Criterion", "AC-POS-0051")]
  public void No_position_family_read_bypasses_the_filters_it_does_have()
  {
    var paths = PositionFamilyPaths();

    Assert.True(
      paths.Count >= 1,
      "no source reaching a position-family entity set was found at all — the derivation has stopped " +
      "matching, and this guard is asserting nothing rather than passing");

    // Collected rather than asserted one at a time: a failure should name every offender, not the first.
    var bypassing = paths
      .Where(path => path.Value.Contains("IgnoreQueryFilters", StringComparison.Ordinal))
      .Select(path => Path.GetFileName(path.Key))
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      bypassing.Length == 0,
      $"these reach a position-family entity set and call IgnoreQueryFilters: {string.Join(", ", bypassing)}. " +
      "One call turns a scoped read into a tenant-wide one, which is the failure the explicit predicates " +
      "exist to make impossible to write by accident.");
  }

  public static TheoryData<string, string> ReadServiceSources()
  {
    var data = new TheoryData<string, string>();

    foreach (var path in PositionFamilyPaths()
      .Where(path => ReadServices.Any(name => path.Key.EndsWith(name, StringComparison.Ordinal)))
      .OrderBy(path => path.Key, StringComparer.Ordinal))
    {
      data.Add(Path.GetFileName(path.Key), path.Value);
    }

    return data;
  }

  // A position-family read path is any HR production source that reaches one of the three entity sets.
  // That is the MECHANISM — not a name, not a folder, and not whether it already carries a predicate.
  private static Dictionary<string, string> PositionFamilyPaths()
  {
    var paths = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var path in Directory.EnumerateFiles(
      Path.Combine(RepositoryRoot(), "src", "Modules", "HR"), "*.cs", SearchOption.AllDirectories))
    {
      if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
          path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      {
        continue;
      }

      var source = StripComments(File.ReadAllText(path));

      if (EntitySets.Any(entitySet => source.Contains(entitySet, StringComparison.Ordinal)))
      {
        paths[path] = source;
      }
    }

    return paths;
  }

  private static string StripComments(string source) =>
    string.Join(
      Environment.NewLine,
      source
        .Split('\n')
        .Select(line =>
        {
          var comment = line.IndexOf("//", StringComparison.Ordinal);
          return comment >= 0 ? line[..comment] : line;
        }));

  private static int CountOccurrences(string source, string value)
  {
    var count = 0;
    for (var index = source.IndexOf(value, StringComparison.Ordinal);
      index >= 0;
      index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal))
    {
      count++;
    }

    return count;
  }

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
         directory is not null;
         directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new InvalidOperationException("Repository root not found.");
  }
}
