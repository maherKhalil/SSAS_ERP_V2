using System.Xml.Linq;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE GUARDS MUST ENFORCE THE SAME RULES ON EVERY OPERATING SYSTEM (TEST-001).
// ==================================================================================================
//
// The architecture dependency guards passed on Windows and silently enforced NOTHING on Linux, because a
// `ProjectReference` path written with backslashes reduced to a project name on one platform and to an
// unrecognisable string on the other. Nothing failed; the guards simply compared real projects against an
// empty set.
//
// These tests exist so that failure mode cannot come back. They are the only tests in the suite that would
// have caught it before a Linux runner did.
public sealed class RepositoryPathPortabilityTests
{
  private const string Expected = "SSAS.BuildingBlocks.Api";

  // ---- THE CORE PROPERTY: THE SEPARATOR DOES NOT CHANGE THE ANSWER.
  //
  // The Windows form is what MSBuild actually writes into every .csproj in this repository, and it is the
  // one that used to break. The others are here so the rule is pinned rather than incidentally true.
  [Theory]
  [InlineData(@"..\..\..\BuildingBlocks\SSAS.BuildingBlocks.Api\SSAS.BuildingBlocks.Api.csproj")]
  [InlineData("../../../BuildingBlocks/SSAS.BuildingBlocks.Api/SSAS.BuildingBlocks.Api.csproj")]
  [InlineData(@"..\../BuildingBlocks\SSAS.BuildingBlocks.Api/SSAS.BuildingBlocks.Api.csproj")]
  [InlineData("SSAS.BuildingBlocks.Api.csproj")]
  [InlineData(@"C:\repo\src\BuildingBlocks\SSAS.BuildingBlocks.Api\SSAS.BuildingBlocks.Api.csproj")]
  [InlineData("/home/runner/work/repo/src/BuildingBlocks/SSAS.BuildingBlocks.Api/SSAS.BuildingBlocks.Api.csproj")]
  public void A_project_reference_reduces_to_the_same_name_under_either_separator(string reference)
  {
    Assert.Equal(Expected, RepositoryPaths.ProjectName(reference));
  }

  // A project name contains dots, so only the FINAL extension may be removed. Stripping from the first dot
  // would turn every project in this repository into "SSAS".
  [Theory]
  [InlineData(@"..\SSAS.HR.Application\SSAS.HR.Application.csproj", "SSAS.HR.Application")]
  [InlineData("../SSAS.Platform.Infrastructure/SSAS.Platform.Infrastructure.csproj", "SSAS.Platform.Infrastructure")]
  [InlineData(@"..\..\..\Modules\HR\SSAS.HR.API\SSAS.HR.API.csproj", "SSAS.HR.API")]
  public void Only_the_final_extension_is_removed(string reference, string expected)
  {
    Assert.Equal(expected, RepositoryPaths.ProjectName(reference));
  }

  // ---- THE REAL REPOSITORY, PARSED THE WAY THE GUARDS PARSE IT.
  //
  // A normalizer that is correct in isolation is not the claim being made. This reads the actual .csproj
  // files and proves that every reference in them resolves to a project that EXISTS in the repository.
  //
  // On Linux before this fix, every one of these would have failed — which is precisely why the dependency
  // guards found no violations there.
  [Fact]
  public void Every_project_reference_in_the_repository_resolves_to_a_known_project()
  {
    var projectFiles = RepositoryProjectFiles();
    var known = projectFiles
      .Select(RepositoryPaths.ProjectNameFromFile)
      .ToHashSet(StringComparer.Ordinal);

    Assert.NotEmpty(known);

    var unresolved = new List<string>();
    foreach (var file in projectFiles)
    {
      foreach (var reference in References(file))
      {
        if (!known.Contains(reference))
        {
          unresolved.Add($"{RepositoryPaths.ProjectNameFromFile(file)} -> {reference}");
        }
      }
    }

    Assert.Empty(unresolved);
  }

  // ---- AND THE PARSE IS NOT VACUOUS.
  //
  // The failure this guards against made every reference set EMPTY, which turns every "no forbidden
  // dependency" assertion into a tautology. Naming references that are known to exist means an empty parse
  // fails here rather than passing everywhere else.
  [Fact]
  public void Known_real_dependencies_are_actually_discovered()
  {
    var references = RepositoryProjectFiles()
      .ToDictionary(RepositoryPaths.ProjectNameFromFile, file => References(file).ToArray(), StringComparer.Ordinal);

    // A BuildingBlocks edge, a module-internal edge, and the shared-API edge introduced in FP-006C5 — one
    // from each layer the guards reason about.
    Assert.Contains("SSAS.BuildingBlocks.SharedKernel", references["SSAS.BuildingBlocks.Domain"]);
    Assert.Contains("SSAS.HR.Domain", references["SSAS.HR.Application"]);
    Assert.Contains("SSAS.BuildingBlocks.Api", references["SSAS.HR.API"]);

    // The Host is the composition root and must reach the module APIs it registers.
    Assert.Contains("SSAS.HR.API", references["SSAS.Host.API"]);
    Assert.Contains("SSAS.Platform.API", references["SSAS.Host.API"]);

    // Every production project resolves at least one reference except the deliberate leaves, so a parse
    // that silently returned nothing everywhere cannot reach this line.
    Assert.NotEmpty(references.Values.Where(set => set.Length > 0));
  }

  // ---- NEGATIVE CONTROL: THE PARSER STILL DISTINGUISHES.
  //
  // A "fix" that mapped everything to one value, or that swallowed unknown input, would satisfy the tests
  // above. This proves different references still produce different names.
  [Fact]
  public void Different_references_do_not_collapse_to_one_name()
  {
    Assert.NotEqual(
      RepositoryPaths.ProjectName(@"..\SSAS.HR.Domain\SSAS.HR.Domain.csproj"),
      RepositoryPaths.ProjectName(@"..\SSAS.HR.Application\SSAS.HR.Application.csproj"));

    Assert.NotEqual(
      RepositoryPaths.ProjectName("../SSAS.Platform.API/SSAS.Platform.API.csproj"),
      RepositoryPaths.ProjectName(@"..\SSAS.HR.API\SSAS.HR.API.csproj"));
  }

  // ---- NOBODY MAY GO BACK TO THE FRAMEWORK HELPER FOR THIS.
  //
  // `Path.GetFileNameWithoutExtension` is correct for a path the local filesystem produced and WRONG for a
  // MSBuild `Include` attribute, and the two are indistinguishable at a glance — which is how this defect
  // arrived. A guard that reads a reference must go through RepositoryPaths, and this makes that a build
  // failure rather than a review note.
  [Fact]
  public void No_architecture_test_parses_a_project_reference_with_the_framework_path_helper()
  {
    var offenders = Directory
      .EnumerateFiles(Path.Combine(RepositoryRoot(), "tests", "Architecture.Tests"), "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => !string.Equals(Path.GetFileName(path), "RepositoryPaths.cs", StringComparison.Ordinal) &&
        !string.Equals(Path.GetFileName(path), "RepositoryPathPortabilityTests.cs", StringComparison.Ordinal))
      .Where(path =>
      {
        // Comments are stripped: these files EXPLAIN the banned call, and a scan that read the prose would
        // fail because someone documented the rule it enforces.
        var code = string.Join(
          "\n",
          File.ReadAllText(path).Split('\n').Select(line =>
          {
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            return comment >= 0 ? line[..comment] : line;
          }));

        return code.Contains("GetFileNameWithoutExtension", StringComparison.Ordinal);
      })
      .Select(Path.GetFileName)
      .ToArray();

    Assert.Empty(offenders);
  }

  private static IReadOnlyList<string> RepositoryProjectFiles() =>
  [
    .. Directory
      .EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
  ];

  private static IEnumerable<string> References(string projectFile) => XDocument
    .Load(projectFile)
    .Descendants("ProjectReference")
    .Select(reference => reference.Attribute("Include")?.Value)
    .Where(reference => !string.IsNullOrWhiteSpace(reference))
    .Select(reference => RepositoryPaths.ProjectName(reference!));

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
}
