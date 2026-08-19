using SSAS.BuildingBlocks.Api.Transport;
using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Localization;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Infrastructure.Localization;

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
      typeof(ITenantLocalizationAdministrationReadService),
      typeof(ITenantLocalizationVersionReader),
      typeof(ILocalizationTextResolver),
      typeof(ILocalizationTenantCache),
      typeof(ILocalizationManagementAuditReadiness),
      typeof(IRequestTenantEligibility)
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
      typeof(PreviewTenantLocalizationOverrideCommand),
      typeof(GetTenantLocalizationHistoryQuery),
      typeof(LocalizationResolutionRequest),
      typeof(LocalizationExplicitBatchRequest),
      typeof(LocalizationGroupBatchRequest)
    };

    Assert.Empty(commands.SelectMany(type => type.GetProperties())
      .Where(property => Regex.IsMatch(property.Name, "TenantId|Actor|UserId", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)));
  }

  [Fact]
  public void Preview_handler_has_no_infrastructure_or_persistence_dependency()
  {
    var forbidden = new[] { "Infrastructure", "EntityFrameworkCore", "Data.SqlClient" };
    Assert.Empty(ForbiddenReferences(typeof(PreviewTenantLocalizationOverrideCommandHandler).Assembly, forbidden));
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
  public void Localization_phase_four_adds_only_approved_server_boundaries_and_no_ui_redis_audit_store_or_mutable_default_catalog()
  {
    var root = FindRepositoryRoot();
    var platformApi = SourceFiles(Path.Combine(root, "src", "Platform", "SSAS.Platform.API")).ToArray();
    var host = SourceFiles(Path.Combine(root, "src", "Host")).ToArray();
    var allowedApiFiles = new HashSet<string>(StringComparer.Ordinal)
    {
      "LocalizationRowVersionCodec.cs",
      "LocalizationTransportContracts.cs",
      "LocalizationApiErrorMapper.cs",
      "LocalizationEndpointRouteBuilderExtensions.cs",
      "LocalizationResponseSecurity.cs"
    };
    Assert.Empty(platformApi.Where(path => File.ReadAllText(path).Contains("Localization", StringComparison.Ordinal))
      .Where(path => !allowedApiFiles.Contains(Path.GetFileName(path))));
    var allowedHostFiles = new HashSet<string>(StringComparer.Ordinal)
    {
      "Program.cs",
      "HostServiceCollectionExtensions.cs",
      "LocalizationOpenApiOperationFilter.cs"
    };
    Assert.Empty(host.Where(path => File.ReadAllText(path).Contains("Localization", StringComparison.Ordinal))
      .Where(path => !allowedHostFiles.Contains(Path.GetFileName(path))));

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
    Assert.DoesNotContain("Audit", migrationSource, StringComparison.Ordinal);
    Assert.Equal(4, Regex.Matches(migrationSource, "migrationBuilder.CreateTable", RegexOptions.CultureInvariant).Count);
  }

  [Fact]
  public void Audit_readiness_is_application_owned_infrastructure_implemented_and_absent_from_domain()
  {
    Assert.Equal("SSAS.Platform.Application", typeof(ILocalizationManagementAuditReadiness).Assembly.GetName().Name);
    Assert.Equal("SSAS.Platform.Infrastructure", typeof(LocalizationManagementAuditReadiness).Assembly.GetName().Name);
    Assert.DoesNotContain(typeof(TenantLocalizationOverride).Assembly.GetTypes(), type =>
      type.Name.Contains("AuditReadiness", StringComparison.Ordinal));
  }

  [Fact]
  public void Every_localization_mutation_handler_retains_locked_tenant_eligibility_and_audit_readiness()
  {
    var root = FindRepositoryRoot();
    var handlers = new[]
    {
      "CreateTenantLocalizationOverrideCommandHandler.cs",
      "UpdateTenantLocalizationOverrideCommandHandler.cs",
      "UndoTenantLocalizationOverrideCommandHandler.cs",
      "RestoreTenantLocalizationDefaultCommandHandler.cs"
    };

    foreach (var handler in handlers)
    {
      var source = File.ReadAllText(Path.Combine(
        root, "src", "Platform", "SSAS.Platform.Application", "Localization", handler));
      Assert.Contains("GetEligibilityForUpdateAsync", source, StringComparison.Ordinal);
      Assert.Contains("LocalizationManagementAuditGuard.CheckAsync", source, StringComparison.Ordinal);
    }
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

  [Fact]
  public void Phase_five_localization_http_reuses_the_application_resolver_without_ef_or_a_second_fallback_algorithm()
  {
    var root = FindRepositoryRoot();
    var path = Path.Combine(root, "src", "Platform", "SSAS.Platform.API", "Localization", "LocalizationEndpointRouteBuilderExtensions.cs");
    var source = File.ReadAllText(path);

    Assert.Contains("ILocalizationTextResolver", source, StringComparison.Ordinal);
    Assert.Contains("ResolveTemplateGroupAsync", source, StringComparison.Ordinal);
    Assert.Contains("ResolveExplicitBatchAsync", source, StringComparison.Ordinal);
    Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
    Assert.DoesNotContain("DbSet", source, StringComparison.Ordinal);
    Assert.DoesNotContain("EntityFrameworkCore", source, StringComparison.Ordinal);
    Assert.DoesNotContain("TenantId", File.ReadAllText(Path.Combine(root, "src", "Platform", "SSAS.Platform.API", "Localization", "LocalizationTransportContracts.cs")), StringComparison.Ordinal);
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
