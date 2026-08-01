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
      "SSAS.GL.Infrastructure"
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
        .Select(reference => Path.GetFileNameWithoutExtension(reference!))
        .OrderBy(reference => reference, StringComparer.Ordinal)
        .ToArray();

      return new ProjectNode(Path.GetFileNameWithoutExtension(projectPath), references);
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
