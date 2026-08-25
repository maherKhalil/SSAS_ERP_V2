using System.Xml.Linq;

namespace SSAS.Architecture.Tests;

public sealed class ProjectDependencyArchitectureTests
{
  private static readonly ProjectGraph Graph = ProjectGraph.Load();

  [Fact]
  public void Domain_projects_do_not_reference_application_api_infrastructure_or_host()
  {
    var violations = Graph.Projects
      .Where(project => project.Name.EndsWith(".Domain", StringComparison.Ordinal))
      .SelectMany(project => project.References
        .Where(reference => IsAnyLayer(reference, "Application", "API", "Infrastructure") || reference == "SSAS.Host.API")
        .Select(reference => $"{project.Name} -> {reference}"));

    Assert.Empty(violations);
  }

  [Fact]
  public void Application_projects_do_not_reference_api_infrastructure_or_host()
  {
    var violations = Graph.Projects
      .Where(project => project.Name.EndsWith(".Application", StringComparison.Ordinal))
      .SelectMany(project => project.References
        .Where(reference => IsAnyLayer(reference, "API", "Infrastructure") || reference == "SSAS.Host.API")
        .Select(reference => $"{project.Name} -> {reference}"));

    Assert.Empty(violations);
  }

  [Fact]
  public void Module_infrastructure_projects_do_not_reference_another_module_infrastructure_project()
  {
    var violations = Graph.ModuleProjects("Infrastructure")
      .SelectMany(project => project.References
        .Where(reference => ProjectGraph.IsModuleProject(reference, "Infrastructure") &&
          !StringComparer.Ordinal.Equals(GetModuleName(project.Name), GetModuleName(reference)))
        .Select(reference => $"{project.Name} -> {reference}"));

    Assert.Empty(violations);
  }

  [Fact]
  public void Module_api_projects_do_not_reference_another_module_directly()
  {
    var violations = Graph.ModuleProjects("API")
      .SelectMany(project => project.References
        .Where(reference => ProjectGraph.IsModuleProject(reference) &&
          !StringComparer.Ordinal.Equals(GetModuleName(project.Name), GetModuleName(reference)))
        .Select(reference => $"{project.Name} -> {reference}"));

    Assert.Empty(violations);
  }

  [Fact]
  [Trait("NonFunctional", "NFR-AUTH-0303")]
  [Trait("Scenario", "TS-AUTH-0072")]
  public void Business_modules_do_not_directly_reference_one_another()
  {
    var violations = Graph.Projects
      .Where(project => ProjectGraph.IsModuleProject(project.Name))
      .SelectMany(project => project.References
        .Where(reference => ProjectGraph.IsModuleProject(reference) &&
          !StringComparer.Ordinal.Equals(GetModuleName(project.Name), GetModuleName(reference)))
        .Select(reference => $"{project.Name} -> {reference}"));

    Assert.Empty(violations);
  }

  [Fact]
  public void Module_api_projects_do_not_reference_any_infrastructure_project()
  {
    var violations = Graph.ModuleProjects("API")
      .SelectMany(project => project.References
        .Where(reference => reference.EndsWith(".Infrastructure", StringComparison.Ordinal))
        .Select(reference => $"{project.Name} -> {reference}"));

    Assert.Empty(violations);
  }

  [Fact]
  public void Host_references_only_approved_module_api_and_infrastructure_projects()
  {
    var host = Graph.GetProject("SSAS.Host.API");
    var allowedReferences = new HashSet<string>(StringComparer.Ordinal)
    {
      "SSAS.Platform.API",
      "SSAS.HR.API",
      "SSAS.GL.API",
      "SSAS.Platform.Infrastructure",
      "SSAS.HR.Infrastructure",
      "SSAS.GL.Infrastructure",

      // ---- PAYROLL JOINS THE INVENTORY BY ACKNOWLEDGEMENT, WHICH IS WHAT THIS LIST IS FOR (FP-012).
      //
      // An EXACT allowlist, deliberately. The Host is where a module becomes reachable, so a reference
      // appearing here without a human noticing is a module wired into the product by accident. Adding
      // Payroll to the Host failed this guard on the first run, exactly as intended.
      "SSAS.Payroll.API",
      "SSAS.Payroll.Infrastructure"

      // ---- AND ATTENDANCE, BY THE SAME ACKNOWLEDGEMENT (FP-013).
      //
      // This guard failed on the first run after wiring Attendance into the Host, exactly as it did for
      // Payroll and exactly as intended. The list is an INVENTORY OF WHAT IS DELIBERATELY REACHABLE, so
      // extending it is the acknowledgement — not a formality to be done reflexively when the test goes red.
      ,"SSAS.Attendance.API",
      "SSAS.Attendance.Infrastructure"
    };

    Assert.All(host.References, reference => Assert.Contains(reference, allowedReferences));
  }

  [Fact]
  public void Production_project_dependencies_are_acyclic()
  {
    var cycle = Graph.FindCycle();

    Assert.Null(cycle);
  }

  [Fact]
  public void Building_blocks_follow_the_documented_dependency_direction()
  {
    AssertProjectReferences("SSAS.BuildingBlocks.SharedKernel", []);
    AssertProjectReferences("SSAS.BuildingBlocks.Contracts", []);
    AssertProjectReferences("SSAS.BuildingBlocks.Domain", ["SSAS.BuildingBlocks.SharedKernel"]);
    AssertProjectReferences(
      "SSAS.BuildingBlocks.Application",
      ["SSAS.BuildingBlocks.Domain", "SSAS.BuildingBlocks.Contracts", "SSAS.BuildingBlocks.SharedKernel"]);
    AssertProjectReferences(
      "SSAS.BuildingBlocks.Infrastructure",
      [
        "SSAS.BuildingBlocks.Application",
        "SSAS.BuildingBlocks.Domain",
        "SSAS.BuildingBlocks.Contracts",
        "SSAS.BuildingBlocks.SharedKernel"
      ]);
  }

  // ==================================================================================================
  // THE SHARED API BOUNDARY (FP-006C5).
  // ==================================================================================================
  //
  // HR is the first business module to expose HTTP endpoints, and every transport primitive it needs lived
  // in SSAS.Platform.API. ADR-012 makes SSAS.Platform.* a module, so HR.API referencing it would have been a
  // module-to-module reference — refused by the rules above.
  //
  // SSAS.BuildingBlocks.Api is the approved answer: the primitives that are no single module's to own.
  // These tests exist so the boundary got MORE explicit rather than weaker — the alternative on the table
  // was excluding API projects from the module rule, which would have permitted exactly what it forbids.

  // ---- IT DEPENDS ON NOTHING.
  //
  // Not on a module, not on a layer, not on BuildingBlocks.Application or Domain. Every module API compiles
  // against it, so a single reference here would put that dependency in every module's transport — and a
  // reference to a MODULE would reintroduce the coupling the project was created to remove.
  [Fact]
  public void The_shared_api_project_references_no_module_and_no_layer()
  {
    AssertProjectReferences("SSAS.BuildingBlocks.Api", []);
  }

  // ---- AND MODULE API PROJECTS MAY DEPEND ON IT.
  //
  // The rule that forbids module-to-module API references stays exactly as strict; this records that the
  // shared project is the sanctioned way to satisfy the need that would otherwise break it.
  [Fact]
  public void Module_api_projects_may_reference_only_the_shared_api_project_for_transport()
  {
    var moduleApis = Graph.ModuleProjects("API").ToArray();

    Assert.NotEmpty(moduleApis);

    foreach (var project in moduleApis)
    {
      var crossModule = project.References
        .Where(reference => ProjectGraph.IsModuleProject(reference) &&
          !StringComparer.Ordinal.Equals(GetModuleName(project.Name), GetModuleName(reference)))
        .ToArray();

      Assert.Empty(crossModule);
    }

    // Named explicitly, so "HR.API must not reach Platform.API" is a stated fact rather than something a
    // reader has to derive from the general rule.
    Assert.DoesNotContain("SSAS.Platform.API", Graph.GetProject("SSAS.HR.API").References);
    Assert.DoesNotContain("SSAS.HR.API", Graph.GetProject("SSAS.Platform.API").References);
    Assert.DoesNotContain("SSAS.HR.API", Graph.GetProject("SSAS.GL.API").References);
    Assert.DoesNotContain("SSAS.Platform.API", Graph.GetProject("SSAS.GL.API").References);
  }

  // ---- ONLY THE APPROVED PRIMITIVES LIVE THERE.
  //
  // ENUMERATED ON PURPOSE. A shared project with no membership rule becomes the place everything ends up,
  // and every addition is a new dependency for every module. Adding a type here fails this test and forces
  // the case to be made — which is the conversation that should happen.
  [Fact]
  public void The_shared_api_project_contains_only_the_approved_transport_primitives()
  {
    var exported = typeof(SSAS.BuildingBlocks.Api.Transport.ApiError).Assembly
      .GetExportedTypes()
      .Select(type => type.FullName)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(
      [
        // The canonical policy-name spelling, shared by the Host that reads it and the endpoints that emit it.
        "SSAS.BuildingBlocks.Api.Authorization.PermissionPolicyNames",
        // A (status, code) pair, and the five generic transport failures every module hits.
        "SSAS.BuildingBlocks.Api.Transport.ApiError",
        "SSAS.BuildingBlocks.Api.Transport.ApiErrors",
        // The RFC 7807 projection.
        "SSAS.BuildingBlocks.Api.Transport.ApiProblems",
        // The response security headers. Shared for the same reason as the rest: two "single sources of
        // truth" for the same headers would drift, and the FP-006 contract requires HR to set them.
        "SSAS.BuildingBlocks.Api.Transport.ApiResponseSecurity",
        // "This endpoint requires permission X" — the mechanism, never the permissions.
        "SSAS.BuildingBlocks.Api.Transport.PermissionEndpointConventions",
        // ---- FP-009 PHASE 2. THE PER-ENDPOINT BODY CEILING, AND THE CASE FOR IT BEING HERE.
        //
        // This guard exists to force the conversation, so: it takes a number of bytes and sets a transport
        // feature. It names no module, no route, no permission and no business concept — the same test
        // `RequirePermission` passes, and it lives in that method's own file because the two are the same
        // kind of thing: a convention a route declares about itself.
        //
        // The METADATA record is exported alongside it deliberately. A ceiling that only took effect at
        // request time could not be asserted under a server that does not enforce body limits, and the
        // harness runs on `TestServer`, which does not. Publishing what a route DECLARES is what makes the
        // ceiling checkable without eleven megabytes of request.
        "SSAS.BuildingBlocks.Api.Transport.RequestSizeEndpointConventions",
        "SSAS.BuildingBlocks.Api.Transport.RequestSizeEndpointConventions+MaxRequestBodySizeMetadata",
        // One rowversion wire format for the whole estate.
        "SSAS.BuildingBlocks.Api.Transport.RowVersionCodec",
        // ---- FP-009. THE CSV BODY READER, AND THE CASE FOR IT BEING HERE RATHER THAN IN HR.
        //
        // This guard exists to force exactly this conversation, so: it is here because it knows a content
        // type, an encoding and a byte order mark, and nothing about an employee. Every argument for
        // `StrictRequestReader` being shared applies to it unchanged — a second module accepting a CSV body
        // would otherwise write a second decoder, and the two would disagree about the one thing that
        // matters, which is what to do with bytes that will not decode.
        //
        // A SIBLING, NOT A WIDENING. `ReadStrictJsonAsync` opens with `HasJsonContentType()` and that line
        // is its contract; teaching it a second content type would make its guarantees conditional. Two
        // types, two gates, neither one branching on the other's.
        "SSAS.BuildingBlocks.Api.Transport.StrictCsvReader",
        // Strict JSON and query parsing.
        "SSAS.BuildingBlocks.Api.Transport.StrictRequestReader"
      ],
      exported);
  }

  // ---- AND NOTHING THERE KNOWS A BUSINESS CONCEPT.
  //
  // The failure this prevents is gradual: one module's error code added "because another module will need it
  // too", until the shared project is a business vocabulary that every module inherits. Transport failures
  // are generic; employees, companies, branches and tenants are not.
  [Fact]
  public void The_shared_api_project_names_no_business_concept()
  {
    // ---- WHAT COUNTS AS A BUSINESS CONCEPT HERE.
    //
    // Aggregate names and the ownership columns that carry them. Note "TenantId" rather than bare "Tenant":
    // the TENANT PLANE is legitimate authorization vocabulary in this project — PermissionPolicyNames must be
    // able to distinguish the tenant plane from the platform-support plane, and that distinction is exactly
    // the ADR-015 contract this project is allowed to own. What it must never learn is the tenant, company,
    // branch or employee as DATA: an ownership column, an entity, or an error code about one.
    string[] businessWords =
      ["Employee", "Company", "Branch", "TenantId", "TenantOwned", "Ledger", "Journal"];

    var sharedApiSources = Directory
      .EnumerateFiles(
        Path.Combine(RepositoryRoot(), "src", "BuildingBlocks", "SSAS.BuildingBlocks.Api"), "*.cs",
        SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    var offenders = new List<string>();
    foreach (var path in sharedApiSources)
    {
      // Comments are stripped first: these files EXPLAIN why business vocabulary is excluded, and a scan
      // that read the prose would fail because someone documented the rule it enforces.
      var code = string.Join(
        Environment.NewLine,
        File.ReadAllText(path).Split('\n').Select(line =>
        {
          var comment = line.IndexOf("//", StringComparison.Ordinal);
          return comment >= 0 ? line[..comment] : line;
        }));

      offenders.AddRange(businessWords
        .Where(word => code.Contains(word.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal))
        .Select(word => $"{Path.GetFileName(path)} -> {word}"));
    }

    Assert.Empty(offenders);
  }

  // ---- ENDPOINTS STAY WITH THEIR MODULE.
  //
  // The shared project supplies the mechanism; it must never acquire a route, a DTO or a handler. Those
  // belong to whoever owns the concept.
  [Fact]
  public void Business_endpoints_and_contracts_stay_in_their_own_module()
  {
    var sharedApiTypes = typeof(SSAS.BuildingBlocks.Api.Transport.ApiError).Assembly.GetTypes();

    Assert.DoesNotContain(sharedApiTypes, type =>
      type.Name.EndsWith("EndpointRouteBuilderExtensions", StringComparison.Ordinal) ||
      type.Name.EndsWith("Request", StringComparison.Ordinal) ||
      type.Name.EndsWith("Response", StringComparison.Ordinal) ||
      type.Name.EndsWith("ApiErrorMapper", StringComparison.Ordinal));
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

    throw new InvalidOperationException("Repository root not found.");
  }

  private static bool IsAnyLayer(string projectName, params string[] layers)
  {
    return layers.Any(layer => projectName.EndsWith($".{layer}", StringComparison.Ordinal));
  }

  private static string? GetModuleName(string projectName)
  {
    return projectName switch
    {
      var name when name.StartsWith("SSAS.Platform.", StringComparison.Ordinal) => "Platform",
      var name when name.StartsWith("SSAS.HR.", StringComparison.Ordinal) => "HR",
      var name when name.StartsWith("SSAS.GL.", StringComparison.Ordinal) => "GL",
      _ => null
    };
  }

  private static void AssertProjectReferences(string projectName, IReadOnlyCollection<string> expectedReferences)
  {
    var actualReferences = Graph.GetProject(projectName).References.OrderBy(reference => reference, StringComparer.Ordinal);
    var orderedExpectedReferences = expectedReferences.OrderBy(reference => reference, StringComparer.Ordinal);

    Assert.Equal(orderedExpectedReferences, actualReferences);
  }

  private sealed class ProjectGraph
  {
    private readonly IReadOnlyDictionary<string, ProjectNode> projects;

    private ProjectGraph(IReadOnlyDictionary<string, ProjectNode> projects)
    {
      this.projects = projects;
    }

    public IEnumerable<ProjectNode> Projects => projects.Values;

    public static ProjectGraph Load()
    {
      var repositoryRoot = FindRepositoryRoot();
      var productionProjects = Directory
        .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
          !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
      var nodes = productionProjects
        .Select(path => CreateProjectNode(path))
        .ToDictionary(project => project.Name, StringComparer.Ordinal);

      return new ProjectGraph(nodes);
    }

    public ProjectNode GetProject(string projectName)
    {
      return projects.TryGetValue(projectName, out var project)
        ? project
        : throw new InvalidOperationException($"Project '{projectName}' was not found in the production graph.");
    }

    public IEnumerable<ProjectNode> ModuleProjects(string layer)
    {
      return projects.Values.Where(project => IsModuleProject(project.Name, layer));
    }

    public static bool IsModuleProject(string projectName, string? layer = null)
    {
      var isModule = GetModuleName(projectName) is not null;

      return isModule && (layer is null || projectName.EndsWith($".{layer}", StringComparison.Ordinal));
    }

    public string? FindCycle()
    {
      var states = new Dictionary<string, VisitState>(StringComparer.Ordinal);
      var path = new List<string>();

      foreach (var project in projects.Values)
      {
        if (Visit(project.Name, states, path, out var cycle))
        {
          return cycle;
        }
      }

      return null;
    }

    private static string FindRepositoryRoot()
    {
      foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
      {
        for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
        {
          if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
          {
            return directory.FullName;
          }
        }
      }

      throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
    }

    private static ProjectNode CreateProjectNode(string projectPath)
    {
      var document = XDocument.Load(projectPath);
      var references = document
        .Descendants("ProjectReference")
        .Select(reference => reference.Attribute("Include")?.Value)
        .Where(reference => !string.IsNullOrWhiteSpace(reference))
        .Select(reference => RepositoryPaths.ProjectName(reference!))
        .OrderBy(reference => reference, StringComparer.Ordinal)
        .ToArray();

      return new ProjectNode(RepositoryPaths.ProjectNameFromFile(projectPath), references);
    }

    private bool Visit(
      string projectName,
      IDictionary<string, VisitState> states,
      ICollection<string> path,
      out string? cycle)
    {
      if (states.TryGetValue(projectName, out var state))
      {
        if (state == VisitState.Visiting)
        {
          cycle = string.Join(" -> ", path.Append(projectName));
          return true;
        }

        cycle = null;
        return false;
      }

      states[projectName] = VisitState.Visiting;
      path.Add(projectName);

      foreach (var reference in GetProject(projectName).References.Where(projects.ContainsKey))
      {
        if (Visit(reference, states, path, out cycle))
        {
          return true;
        }
      }

      path.Remove(projectName);
      states[projectName] = VisitState.Visited;
      cycle = null;
      return false;
    }

    private enum VisitState
    {
      Visiting,
      Visited
    }
  }

  private sealed record ProjectNode(string Name, IReadOnlyCollection<string> References);
}
