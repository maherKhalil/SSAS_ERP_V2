using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

public sealed class PersistenceArchitectureTests
{
  [Fact]
  public void Production_source_does_not_define_a_generic_repository()
  {
    var matches = ProductionSourceFiles()
      .Where(path => Regex.IsMatch(File.ReadAllText(path), @"\b(?:I)?Repository\s*<", RegexOptions.CultureInvariant))
      .ToArray();

    Assert.Empty(matches);
  }

  [Fact]
  public void Domain_and_application_projects_remain_entity_framework_free()
  {
    var violations = ProductionSourceFiles()
      .Where(path => IsDomainOrApplicationPath(path))
      .Where(path => File.ReadAllText(path).Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(violations);
  }

  [Fact]
  public void Application_boundaries_do_not_expose_iqueryable()
  {
    var violations = ProductionSourceFiles()
      .Where(path => path.Contains(".Application", StringComparison.Ordinal))
      .Where(path => File.ReadAllText(path).Contains("IQueryable", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(violations);
  }

  [Fact]
  public void Platform_identity_access_has_no_physical_delete_operation()
  {
    var violations = ProductionSourceFiles()
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
    var violations = ProductionSourceFiles()
      .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Platform{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => Regex.IsMatch(
        File.ReadAllText(path),
        @"Log(?:Trace|Debug|Information|Warning|Error|Critical)\s*\([^;]*(?:password|secret|token|claims?)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
      .ToArray();

    Assert.Empty(violations);
  }

  private static IEnumerable<string> ProductionSourceFiles() => Directory
    .EnumerateFiles(FindRepositoryRoot(), "*.cs", SearchOption.AllDirectories)
    .Where(path => path.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

  private static bool IsDomainOrApplicationPath(string path) => path.Contains(".Domain", StringComparison.Ordinal) ||
    path.Contains(".Application", StringComparison.Ordinal);

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
