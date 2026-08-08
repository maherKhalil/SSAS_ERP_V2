using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Domain.Localization;

namespace SSAS.Architecture.Tests;

public sealed class LocalizationArchitectureTests
{
  [Fact]
  public void Localization_building_blocks_has_no_platform_persistence_http_or_cache_dependency()
  {
    var forbidden = new[]
    {
      "SSAS.Platform",
      "Microsoft.EntityFrameworkCore",
      "Microsoft.Data.SqlClient",
      "Microsoft.AspNetCore",
      "Microsoft.Extensions.Caching"
    };
    var violations = typeof(ResourceKey).Assembly.GetReferencedAssemblies()
      .Where(reference => forbidden.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
      .Select(reference => reference.Name)
      .ToArray();

    Assert.Empty(violations);
  }

  [Fact]
  public void Platform_localization_domain_and_application_respect_layer_boundaries()
  {
    var domainForbidden = new[] { "Infrastructure", "EntityFrameworkCore", "AspNetCore", "Data.SqlClient" };
    var applicationForbidden = new[] { "Infrastructure", "EntityFrameworkCore", "AspNetCore", "Data.SqlClient" };
    Assert.Empty(ForbiddenReferences(typeof(TenantLocalizationOverride).Assembly, domainForbidden));
    Assert.Empty(ForbiddenReferences(typeof(LocalizationTextResolver).Assembly, applicationForbidden));
  }

  [Fact]
  public void Localization_application_boundaries_expose_neither_queryables_nor_persistence_types()
  {
    var boundaryTypes = new[]
    {
      typeof(ITenantLocalizationSettingsRepository),
      typeof(ITenantLocalizationOverrideRepository),
      typeof(ITenantLocalizationOverrideReadService),
      typeof(ITenantLocalizationVersionReader),
      typeof(ILocalizationTextResolver),
      typeof(ILocalizationTenantCache)
    };
    var signatures = boundaryTypes.SelectMany(type => type.GetMethods())
      .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
      .Select(type => type.ToString())
      .ToArray();

    Assert.DoesNotContain(signatures, signature => signature.Contains("IQueryable", StringComparison.Ordinal));
    Assert.DoesNotContain(signatures, signature => signature.Contains("DbContext", StringComparison.Ordinal));
    Assert.DoesNotContain(signatures, signature => signature.Contains("DbSet", StringComparison.Ordinal));
    Assert.DoesNotContain(signatures, signature => signature.Contains("EntityEntry", StringComparison.Ordinal));
  }

  [Fact]
  public void Localization_commands_never_accept_tenant_or_actor_identity()
  {
    var commands = new[]
    {
      typeof(CreateTenantLocalizationOverrideCommand),
      typeof(UpdateTenantLocalizationOverrideCommand),
      typeof(UndoTenantLocalizationOverrideCommand),
      typeof(RestoreTenantLocalizationDefaultCommand),
      typeof(GetTenantLocalizationHistoryQuery),
      typeof(LocalizationResolutionRequest),
      typeof(LocalizationExplicitBatchRequest),
      typeof(LocalizationGroupBatchRequest)
    };

    Assert.Empty(commands.SelectMany(type => type.GetProperties())
      .Where(property => Regex.IsMatch(property.Name, "TenantId|Actor|UserId", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)));
  }

  [Fact]
  public void Localization_domain_events_contain_no_text_placeholder_or_transport_data()
  {
    var events = typeof(TenantLocalizationOverride).Assembly.GetTypes()
      .Where(type => typeof(DomainEvent).IsAssignableFrom(type))
      .Where(type => type.Namespace == "SSAS.Platform.Domain.Localization.Events")
      .ToArray();
    var violations = events.SelectMany(type => type.GetProperties()
      .Where(property => Regex.IsMatch(
        property.Name,
        "Value|Text|Placeholder|Http|Claim|Credential|Secret|Password|Token|Request|Trace",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
      .Select(property => $"{type.Name}.{property.Name}"))
      .ToArray();

    Assert.Equal(4, events.Length);
    Assert.Empty(violations);
  }

  [Fact]
  public void Localization_history_has_no_public_mutation_or_setter_api()
  {
    var type = typeof(TenantLocalizationOverrideVersion);
    Assert.Empty(type.GetProperties().Where(property => property.SetMethod?.IsPublic == true));
    Assert.Empty(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
      .Where(method => Regex.IsMatch(method.Name, "^(Set|Update|Delete|Remove|Restore|Undo)", RegexOptions.CultureInvariant)));
  }

  [Fact]
  public void Milestone_one_adds_no_http_openapi_ui_redis_or_mutable_default_catalog()
  {
    var root = FindRepositoryRoot();
    var platformApi = SourceFiles(Path.Combine(root, "src", "Platform", "SSAS.Platform.API")).ToArray();
    var host = SourceFiles(Path.Combine(root, "src", "Host")).ToArray();
    Assert.Empty(platformApi.Concat(host).Where(path => File.ReadAllText(path).Contains("Localization", StringComparison.Ordinal)));

    var uiRoot = Path.Combine(root, "src", "UI");
    if (Directory.Exists(uiRoot))
    {
      Assert.Empty(Directory.EnumerateFiles(uiRoot, "*localization*", SearchOption.AllDirectories));
    }

    var productionFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
      .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".csproj", StringComparison.Ordinal))
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .ToArray();
    Assert.Empty(productionFiles.Where(path => Regex.IsMatch(
      File.ReadAllText(path),
      "StackExchange\\.Redis|IDistributedCache|AddStackExchangeRedisCache",
      RegexOptions.CultureInvariant)));

    var migration = Directory.EnumerateFiles(
        Path.Combine(root, "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations"),
        "*AddLocalizationCore.cs")
      .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));
    var migrationSource = File.ReadAllText(migration);
    Assert.DoesNotContain("LocalizationResources", migrationSource, StringComparison.Ordinal);
    Assert.DoesNotContain("LocalizationDefault", migrationSource, StringComparison.Ordinal);
    Assert.Equal(4, Regex.Matches(migrationSource, "migrationBuilder.CreateTable", RegexOptions.CultureInvariant).Count);
  }

  [Fact]
  public void Localization_diagnostics_never_log_localized_or_placeholder_values()
  {
    var path = Path.Combine(
      FindRepositoryRoot(),
      "src", "Platform", "SSAS.Platform.Infrastructure", "Localization", "LocalizationDiagnostics.cs");
    var source = File.ReadAllText(path);

    Assert.DoesNotContain("LocalizedValue", source, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("PlaceholderValue", source, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotMatch("Log(?:Warning|Error|Information).*\\b(Text|Value|Placeholder)\\b", source);
  }

  private static IEnumerable<string> ForbiddenReferences(Assembly assembly, IReadOnlyCollection<string> forbidden) =>
    assembly.GetReferencedAssemblies()
      .Where(reference => forbidden.Any(part => reference.Name?.Contains(part, StringComparison.Ordinal) == true))
      .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}");

  private static IEnumerable<string> SourceFiles(string directory) =>
    Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

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
