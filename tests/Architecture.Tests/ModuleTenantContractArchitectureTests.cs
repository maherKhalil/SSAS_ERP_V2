using System.Reflection;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Persistence;

namespace SSAS.Architecture.Tests;

// THE MODULE-FACING TENANT CONTRACT BOUNDARY (FP-006C3-pre, ADR-012, ADR-017).
//
// ADR-012 forbids one module referencing another's internals and permits cross-module consumption only
// through approved contracts or explicitly authorized module-facing abstractions. `SSAS.Platform.*` is a
// module under that rule, so a business module cannot reach the tenant execution plane directly.
//
// `SSAS.BuildingBlocks.Tenancy` is that authorized abstraction set. These tests protect the two properties
// that make it work: that it stays a CONTRACT project with no implementation and no persistence dependency,
// and that the direct module-to-module reference it exists to avoid has not quietly reappeared.
public sealed class ModuleTenantContractArchitectureTests
{
  private static readonly Assembly TenancyAssembly = typeof(IBranchTransferScope).Assembly;

  // ---- IT IS CONTRACTS ONLY. An implementation here would be code every module inherits whether it wants
  // it or not, and would give the shared project a reason to grow dependencies.
  [Fact]
  public void The_tenancy_project_contains_only_contracts()
  {
    var concrete = TenancyAssembly.GetTypes()
      .Where(type => type.IsClass && !type.IsAbstract)
      // Immutable value carriers and error catalogues are part of a contract's vocabulary, not behaviour.
      .Where(type => type != typeof(BranchTransferDeclaration) &&
        type != typeof(BranchTransferErrors) &&
        type != typeof(BranchAccessSummary))
      .Where(type => !type.IsCompilerGenerated())
      .Select(type => type.FullName)
      .ToArray();

    Assert.Empty(concrete);
  }

  // ---- AND IT CARRIES NO PERSISTENCE DEPENDENCY, so an Application-layer module can reference it without
  // pulling EF Core in. The one EF-shaped contract modules need lives in BuildingBlocks.Infrastructure.
  [Fact]
  public void The_tenancy_project_does_not_depend_on_entity_framework()
  {
    Assert.DoesNotContain(
      TenancyAssembly.GetReferencedAssemblies(),
      reference => reference.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true);

    // Nor on any module, in either direction. A contract project that referenced Platform would put every
    // consumer back where it started.
    Assert.DoesNotContain(
      TenancyAssembly.GetReferencedAssemblies(),
      reference => reference.Name?.StartsWith("SSAS.Platform", StringComparison.Ordinal) == true ||
        reference.Name?.StartsWith("SSAS.HR", StringComparison.Ordinal) == true ||
        reference.Name?.StartsWith("SSAS.GL", StringComparison.Ordinal) == true);
  }

  // ---- BOTH SIDES REFERENCE THE CONTRACTS, AND NEITHER REFERENCES THE OTHER.
  //
  // This is the whole point of the indirection, and it is the assertion that fails first if someone "just
  // adds a reference" to get something building.
  [Fact]
  public void Platform_implements_and_modules_consume_without_referencing_one_another()
  {
    var projects = ProjectReferences();

    Assert.Contains("SSAS.BuildingBlocks.Tenancy", projects["SSAS.Platform.Application"]);
    Assert.Contains("SSAS.BuildingBlocks.Tenancy", projects["SSAS.HR.Application"]);
    Assert.Contains("SSAS.BuildingBlocks.Tenancy", projects["SSAS.HR.Infrastructure"]);

    foreach (var (project, references) in projects)
    {
      if (project.StartsWith("SSAS.HR.", StringComparison.Ordinal) ||
        project.StartsWith("SSAS.GL.", StringComparison.Ordinal))
      {
        Assert.DoesNotContain(
          references,
          reference => reference.StartsWith("SSAS.Platform.", StringComparison.Ordinal));
      }

      if (project.StartsWith("SSAS.Platform.", StringComparison.Ordinal))
      {
        Assert.DoesNotContain(
          references,
          reference => reference.StartsWith("SSAS.HR.", StringComparison.Ordinal) ||
            reference.StartsWith("SSAS.GL.", StringComparison.Ordinal));
      }
    }
  }

  // ---- A MODULE MAPS ITS OWN ENTITIES THROUGH A CONTRACT NEITHER SIDE OWNS.
  //
  // ITenantModelContributor lives in BuildingBlocks.Infrastructure — which already owns EF — so Platform can
  // call it without knowing who implements it, and a module can implement it without referencing Platform.
  [Fact]
  public void The_tenant_model_contributor_is_a_shared_infrastructure_contract()
  {
    var contributor = typeof(ITenantModelContributor);

    Assert.True(contributor.IsInterface);
    Assert.Equal("SSAS.BuildingBlocks.Infrastructure", contributor.Assembly.GetName().Name);

    var configure = contributor.GetMethod(nameof(ITenantModelContributor.Configure));
    Assert.NotNull(configure);
    Assert.Equal(typeof(ModelBuilder), configure!.GetParameters().Single().ParameterType);

    // The tenant context accepts a SET of them, optionally: the maintenance and schema-tooling paths supply
    // none, and that is a different model rather than a degraded one.
    var parameter = typeof(SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContext)
      .GetConstructors()
      .Single()
      .GetParameters()
      .SingleOrDefault(candidate =>
        candidate.ParameterType == typeof(IEnumerable<ITenantModelContributor>));

    Assert.NotNull(parameter);
    Assert.True(parameter!.IsOptional);
  }

  // ---- THE CONTRIBUTOR SET IS PART OF THE MODEL CACHE KEY.
  //
  // EF caches one model per context type by default, so a context built with no contributors and one built
  // with HR's would otherwise share whichever model happened to be created first in the process. The failure
  // is silent and order-dependent, which is exactly why it is pinned here rather than left to review.
  [Fact]
  public void The_tenant_model_cache_key_accounts_for_the_contributor_set()
  {
    var factory = typeof(SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContext).Assembly
      .GetType("SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantModelCacheKeyFactory");

    Assert.NotNull(factory);
    Assert.Contains(
      typeof(Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory),
      factory!.GetInterfaces());

    // Installed by the context itself rather than at each option-building site, so a caller that forgot
    // cannot silently reintroduce the shared-model bug.
    var source = ReadTenantDbContextSource();
    Assert.Contains("ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>", source, StringComparison.Ordinal);

    // And contributions are applied BEFORE base.OnModelCreating, so contributed entities receive the global
    // tenant query filter. An entity added afterwards would be unfiltered — a silent cross-tenant leak.
    var contributeIndex = source.IndexOf("contributor.Configure(modelBuilder)", StringComparison.Ordinal);
    var baseIndex = source.IndexOf("base.OnModelCreating(modelBuilder)", StringComparison.Ordinal);

    Assert.True(contributeIndex > 0 && baseIndex > contributeIndex);
  }

  // ---- ONLY WHAT A MODULE MUST CALL LIVES IN THE SHARED SET.
  //
  // Every type here permanently widens its own blast radius, so the set is enumerated rather than left to
  // grow by habit. Adding one is a deliberate act that updates this list.
  [Fact]
  public void The_shared_contract_set_is_exactly_what_modules_need()
  {
    var exported = TenancyAssembly.GetExportedTypes()
      .Where(type => !type.IsCompilerGenerated())
      .Select(type => type.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(
      [
        nameof(BranchAccessSummary),
        nameof(BranchTransferDeclaration),
        nameof(BranchTransferErrors),
        nameof(BranchTransferMode),
        nameof(IBranchTransferAuthorizer),
        nameof(IBranchTransferScope),
        // The trusted execution branch, needed so a module can record which branch an operation happened in
        // on a record that is not itself branch-owned (FP-006C3).
        nameof(ICurrentBranchResolver),
        // The acting tenant user, needed so a module can name WHO is asking when resolving scope. It carries
        // no roles, permissions, session or claims: what they may DO stays with the permission pipeline.
        nameof(ICurrentTenantUser),
        nameof(ITenantBranchAccessResolver),
        nameof(ITenantUnitOfWork)
      ],
      exported);
  }

  private static Dictionary<string, IReadOnlyCollection<string>> ProjectReferences()
  {
    var root = RepositoryRoot();

    return Directory
      .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .ToDictionary(
        path => Path.GetFileNameWithoutExtension(path)!,
        path => (IReadOnlyCollection<string>)XDocument.Load(path)
          .Descendants("ProjectReference")
          .Select(reference => reference.Attribute("Include")?.Value)
          .Where(reference => !string.IsNullOrWhiteSpace(reference))
          .Select(reference => Path.GetFileNameWithoutExtension(reference!))
          .ToArray(),
        StringComparer.Ordinal);
  }

  private static string ReadTenantDbContextSource() => File.ReadAllText(Path.Combine(
    RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure",
    "Persistence", "TenantErp", "TenantDbContext.cs"));

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

internal static class ArchitectureTypeExtensions
{
  public static bool IsCompilerGenerated(this Type type) =>
    type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false) ||
    type.Name.Contains('<', StringComparison.Ordinal);
}
