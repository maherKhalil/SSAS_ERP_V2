using System.Reflection;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// WHAT A PROJECT CAN SEE, AS OPPOSED TO WHAT IT HAPPENS TO USE (272).
// ==================================================================================================
//
// ---- THE DEFECT THIS EXISTS TO REMOVE, MEASURED RATHER THAN ARGUED.
//
// `Assembly.GetReferencedAssemblies()` reads EMITTED METADATA, and **the C# compiler omits a reference no
// type is taken from**. So a project can declare a forbidden dependency in its `.csproj`, build, and report
// that dependency as ABSENT.
//
// Measured in item `269` (`3b9728c`): adding a forbidden `ProjectReference` to `SSAS.HR.API.csproj` left an
// assertion over `GetReferencedAssemblies()` GREEN, and the three pre-existing HR boundary guards stayed
// green under the same plant. Item `T-153` measured the same thing independently, item `169` reads the
// output directory for the same reason, and `EmploymentTypeAssumptionTests` records it at its own site.
// `Architecture-Principles.md` states it as a principle.
//
// ---- WHY THE DIFFERENCE MATTERS, AND IT IS NOT PEDANTRY.
//
// A declared-but-unused reference is LATENT CAPABILITY. The `.csproj` edit is merged, the friction is gone,
// and the next developer reaching for a forbidden type meets nothing in the way. **`can see` catches the
// capability; `does use` catches the consequence** — and by then the boundary has already been crossed.
//
// ⚠ SO NEITHER READING IS SIMPLY BETTER. A criterion about actual coupling — *reaches X only through the
// published contract* — is correctly served by `GetReferencedAssemblies`, and using this instead would
// report a project that declares a reference it never touches. **Pick the instrument the criterion's
// wording asks for:** *may not reference* / *cannot see* / *regardless of use* wants this one.
//
// ---- WHAT IT READS, AND THE BOUND ON THAT.
//
// `ProjectReference` and `PackageReference` `Include` attributes, matched ON THE ELEMENT rather than on the
// name. ⚠ A bare substring search for a forbidden name reports a false positive on PROSE: a comment in
// `SSAS.HR.Domain.csproj` cites `SSAS.Platform.Domain` as a naming precedent, and an early version of the
// `269` guard called that a violation on a clean tree.
//
// ⚠⚠ IT READS ONE LINE AT A TIME. MSBuild writes these elements on a single line and every project in this
// repository does; an `Include` split across lines would be invisible here. That is a real bound and it is
// stated rather than guarded, because no such element exists to test against.
//
// ==================================================================================================
// ⚠⚠⚠ READ THIS BEFORE CHOOSING BETWEEN THIS HELPER AND THE RAW CALL — THERE IS NO BAN, DELIBERATELY
// ==================================================================================================
//
// **`can see` and `does use` are NOT a strength ordering. Which one is stronger is a function of HOW THE
// DEPENDENCY CAN ARRIVE**, and item `272` swept every boundary guard in this suite on that basis:
//
//   A DIRECT `ProjectReference`, or a package this repository really declares (`Microsoft.EntityFrameworkCore`,
//   `Microsoft.AspNetCore`) — **declared ⊃ emitted.** You can declare without using, so the emitted read is
//   blind to the merged-but-unused reference: the `.csproj` edit is in, the friction is gone, and the next
//   developer reaching for a forbidden type meets nothing in the way. **Use this helper, and keep the
//   emitted assertion beside it — they fail on different days, and the emitted one catches a transitive
//   use no `.csproj` of ours names.**
//
//   A TRANSITIVE OR FRAMEWORK DEPENDENCY — `Microsoft.Data.SqlClient` (arrives through
//   `EntityFrameworkCore.SqlServer`), `System.Security.Cryptography`, `Microsoft.Extensions.Caching`, or a
//   package banned precisely BECAUSE nothing references it, like the seven message brokers in
//   `TenantRoutingArchitectureTests` — **emitted ⊃ declared, AND DECLARED IS EMPTY BY CONSTRUCTION.** The
//   whole repository declares ELEVEN `PackageReference` lines across three projects. **A declared check on
//   any of those subjects passes forever, for a reason no reader would guess, while reading as extra
//   rigour. THE RAW CALL IS CORRECT THERE. Do not "finish the job" by adding the declared half.**
//
// ---- ⚠ AND THERE IS NO GUARD BANNING THE RAW CALL. THAT IS A RULING, NOT AN OVERSIGHT.
//
// `272` intended to end with one, on the `RepositoryPathPortabilityTests` precedent: ban the weak call,
// supply the sanctioned helper. **It is not writable.** The ban's real population is *call sites that
// SHOULD have been converted* — and separating those from the ones that correctly use the raw call
// requires knowing what each criterion needs, which is a SEMANTIC judgement no mechanical rule can make.
// A ban would have to exempt a third of its own population on day one, which is how a guard is born
// vacuous. A *pair it with a declared read* rule is a structural proxy for that semantic property and can
// be satisfied by a `.csproj` read that checks something else entirely.
//
// **So the record lives here rather than in a guard, because this file is what you are reading at the
// moment you make the choice.** If the thing you are banning can never appear in a `.csproj`, use the raw
// call and say why at the site — `TenantRoutingArchitectureTests` is the worked example, and it separates
// the two levels: the MECHANISM is controllable, the individual banned LITERALS are not, exactly as `273`
// found for an identifier with no symbol anywhere in the tree.
// ==================================================================================================
// ⚠⚠⚠ HOW TO CONTROL A BAN BUILT ON THIS, AND THE ONE ASYMMETRY THAT MAKES IT SAFE (278)
// ==================================================================================================
//
// Every ban here is an `Assert.Empty` / `Assert.DoesNotContain` and therefore needs a control proving the
// predicate CAN fire. `278` audited all 40 controls this sweep wrote. **7 call through the instrument the
// assertion uses; 33 re-express the predicate inline.**
//
// ---- ⚠ *33 BLIND* IS THE WRONG SENTENCE, AND THE DISTINCTION IS THE WHOLE POINT.
//
// All 33 call `DeclaredDependencies.Of`. **They are sighted on the PARSE and blind on the PREDICATE** —
// different organs. If this class stopped recognising an element type (as it really had, until `ba94176`),
// every one of the 33 reddens. That is the failure they were written against and they catch it.
//
// ---- ⚠⚠ WHY THE INLINE PREDICATE IS LEFT ALONE: WIDENING A BAN CANNOT PRODUCE A FALSE GREEN.
//
// `Contains ⊇ StartsWith` for every string, so `StartsWith` -> `Contains` makes a ban fire MORE. That is a
// false RED — loud, immediate, self-announcing. **Only NARROWING can hide a violation, and narrowing can
// only empty the set where a term is NOT a real prefix.** Measured across all 33: the one file where that
// held was `LocalizationArchitectureTests`, whose terms are bare segments — which is why that file matches
// by `Contains`, says so in a ⚠⚠⚠ header, and routes its controls through a helper. It is the exception.
//
// **So do not "finish the job" by routing the other 33 through helpers.** It closes a direction that is
// already loud, and a fix whose visible size exceeds the risk it closes is how a sweep starts lying about
// its own coverage.
//
// ---- ⚠⚠⚠ WHAT *IS* SILENT, AND WHAT THE CONTROLS MUST THEREFORE DERIVE FROM.
//
// A control that hardcodes its own term literals CANNOT NOTICE A TERM ADDED TO THE BAN. Add a fourth prefix
// to `declarable` and forget a witness, and that branch holds over a term nothing can match — silently,
// forever, and with every existing control still green.
//
// ⚠ **That is not a malicious swap. It is someone extending a ban and not thinking about controls, which
// is the ordinary case — and therefore the one that will actually happen.**
//
// So where a site has a term LIST, the controls are DERIVED FROM IT:
//
//   Assert.All(declarable, term =>
//   {
//     Assert.True(witnessOf.TryGetValue(term, out var witness), "...");
//     Assert.Contains(DeclaredDependencies.Of(witness!), name => name.StartsWith(term, Ordinal));
//   });
//
// Adding a term now fails at the witness lookup rather than passing into silence. **This closes TERM drift
// and deliberately not PREDICATE drift** — see the asymmetry above, which is the reason and which lives
// nowhere else in the code.
//
// ---- WHERE THE DERIVATION IS REQUIRED, AND WHERE IT IS NOT.
//
//   A ban that grows a term LIST MUST derive its controls from that list. The list can sit far from the
//   controls that iterate it, so a term can be added without the controls ever being looked at.
//   A ban that is a SINGLE INLINE LITERAL need not. The banned term and its control are adjacent literals
//   in the same method, so **extending the ban requires editing the method the control sits in** — the
//   drift is not constructible there. ~17 controls are of this kind and are deliberately left alone.
//
// ⚠⚠⚠ AND THE PARAGRAPH ABOVE IS DIAGNOSIS, NOT PREVENTION. **NOTHING ENFORCES IT.** No guard checks that
// a term list has derived controls. A future method that introduces a `declarable` array with hardcoded
// controls beside it reintroduces the entire defect, silently, and every existing test stays green.
//
// **Naming a failure mode confers no immunity, and a comment that gets CREDITED as a guard is worse than
// no comment at all** — it buys the reassurance of coverage while supplying none. If this shape recurs,
// the answer is an instrument over the term lists, not a longer version of this paragraph.
internal static class DeclaredDependencies
{
  // ================================================================================================
  // ⚠⚠⚠ THE ELEMENT TYPES THAT CARRY A DEPENDENCY, ENUMERATED RATHER THAN ACCUMULATED (275)
  // ================================================================================================
  //
  // This list read `ProjectReference` and `PackageReference` only, and that was WRONG AND SHIPPED.
  // `FrameworkReference Include="Microsoft.AspNetCore.App"` appears SEVEN TIMES in `src/` and is how an
  // ASP.NET dependency is actually taken here — so guards banning `Microsoft.AspNetCore` had a declared
  // half that could not see the thing it was banning.
  //
  // ⚠ PROVEN BEFORE IT WAS FIXED: with `<FrameworkReference Include="Microsoft.AspNetCore.App" />` added to
  // `SSAS.Platform.Domain.csproj`, `TenantLifecycleArchitectureTests` stayed GREEN — **and so did its
  // emitted half, because an unused reference emits nothing.** Both readings blind at once, which is the
  // FP-011 shape: the capability merges, the friction disappears, and nothing notices until first use.
  //
  // ---- WHAT WAS CONSIDERED AND WHY THE REST ARE ABSENT. THE LIST IS A DECISION, NOT AN ACCUMULATION.
  //
  // Measured across every `.csproj` in the repository, the item types that appear at all are:
  // `ProjectReference`, `PackageReference`, `FrameworkReference`, `InternalsVisibleTo` and `Using`.
  //
  //   * `InternalsVisibleTo` is an OUTBOUND grant — it names who may see THIS project, the reverse
  //     direction, and is not a dependency of it.
  //   * `Using` is a namespace import. The assembly it names must already arrive through one of the three
  //     below, so it adds no edge.
  //
  // ⚠⚠ AND A DIFFERENT KIND OF ABSENCE, KEPT SEPARATE BECAUSE THE GROUNDS DIFFER: `Reference`,
  // `COMReference` and `NativeReference` WOULD each carry a dependency and appear NOWHERE in this
  // repository today — checked, not assumed. **They are excluded by absence, not by kind, so the first one
  // added must be added here too.** `Analyzer` and `PackageDownload` are build-time only and carry no
  // reference either way.
  //
  // The distinction matters: an element excluded by KIND stays excluded forever; one excluded by ABSENCE
  // is a standing obligation, and collapsing the two is how this list became wrong the first time.
  private static readonly string[] DependencyElements =
    ["ProjectReference", "PackageReference", "FrameworkReference"];

  // The declared dependency names of the project that builds this assembly.
  //
  // Project references are reduced through `RepositoryPaths.ProjectName` — the same one rule, so this
  // cannot drift from the guards that already parse those attributes. Package references carry the package
  // name in `Include` directly.
  public static string[] Of(Assembly assembly)
  {
    ArgumentNullException.ThrowIfNull(assembly);

    return Of(assembly.GetName().Name!);
  }

  public static string[] Of(string assemblyName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

    return File.ReadAllLines(ProjectFileOf(assemblyName))
      .Select(line => line.Trim())
      .Where(line => DependencyElements.Any(element => line.Contains(element, StringComparison.Ordinal)))
      .Select(IncludeValue)
      .Where(value => value.Length > 0)
      .Select(value => value.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
        ? RepositoryPaths.ProjectName(value)
        : value)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();
  }

  // ⚠ THROWS RATHER THAN RETURNING NOTHING, AND THAT IS THE WHOLE SAFETY PROPERTY OF THIS CLASS.
  //
  // Every caller uses the result for a BAN — `Assert.Empty(forbidden)` or `Assert.DoesNotContain`. An empty
  // set satisfies all of those, so a helper that answered "no project file found" with `[]` would turn
  // every guard built on it green and blind, which is precisely the failure `GetReferencedAssemblies`
  // already produces and this class exists to end. A missing or duplicated project file is a broken
  // instrument and must say so out loud.
  //
  // Zero declared dependencies is NOT an error: `SSAS.HR.Contracts` is a leaf and legitimately declares
  // none. Only a missing FILE is.
  public static string ProjectFileOf(string assemblyName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

    // ---- ⚠ RECURSES UNDER `src/` WITH NO `bin`/`obj` EXCLUSION, AND IS SAFE BY SEARCH PATTERN (T-191).
    //
    // This walk DOES descend into every project's `obj/` and `bin/`. What protects it is the pattern:
    // **build output contains no `.csproj`** — `obj/` holds `.nuget.g.props`, `.AssemblyInfo.cs` and the
    // like, never a project file. *Measured 2026-09-07: zero `.csproj` files exist under any `bin` or `obj`
    // directory anywhere in `src/`.*
    //
    // ⚠⚠ **A SEARCH PATTERN IS A WEAKER MITIGATION THAN A FILTER BECAUSE NOTHING STATES IT** — which is the
    // only reason this paragraph exists. Widen the pattern to `*.csproj` plus anything generated, or to
    // `*.props`, and the protection is gone with no other line changing.
    //
    // ⚠ THIS ONE FAILS LOUD, WHICH THE SIBLING AT `PositionApplicationArchitectureTests` DOES NOT. The
    // contract below is EXACTLY ONE match; two would throw rather than silently pick the first. So a stray
    // project file appearing under build output would stop this class rather than quietly change an answer.
    var matches = Directory
      .GetFiles(Path.Combine(RepositoryRoot(), "src"), $"{assemblyName}.csproj", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
      .OrderBy(path => path, StringComparer.Ordinal)
      .ToArray();

    return matches.Length == 1
      ? matches[0]
      : throw new InvalidOperationException(
        $"expected exactly one src/**/{assemblyName}.csproj and found {matches.Length}. Every guard built " +
        "on this reads a ban from it, and a ban over an unread file passes silently.");
  }

  // `Include="..."`, taking the value between the first pair of quotes after the attribute name. Returns
  // empty for a line carrying the element name without the attribute — a closing tag, or a comment.
  private static string IncludeValue(string line)
  {
    const string marker = "Include=\"";

    var start = line.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
    {
      return string.Empty;
    }

    var value = line[(start + marker.Length)..];
    var end = value.IndexOf('"', StringComparison.Ordinal);

    return end < 0 ? string.Empty : value[..end];
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
