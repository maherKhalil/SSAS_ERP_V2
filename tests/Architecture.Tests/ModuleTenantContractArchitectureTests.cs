using System.Reflection;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Permissions;
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
        type != typeof(BranchAccessSummary) &&
        type != typeof(CompanyAccessSummary) &&
        // What a module DECLARES one of its permissions to be. Data a contributor hands over, with no
        // behaviour and no scope of its own -- the composer stamps that (ADR-012 r1.2).
        type != typeof(ModulePermissionDefinition))
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
    // ⚠ DECLARED AND EMITTED, BECAUSE THEY FAIL ON DIFFERENT DAYS (272). The emitted reading omits a
    // reference no type is taken from, so this project could DECLARE EF and pass until the first use — and
    // *an Application-layer module can reference it without pulling EF Core in* is a claim about what
    // consumers inherit, which is decided by the declaration rather than by current usage.
    //
    // ⚠⚠ FOUR EXERCISES FOR FOUR BRANCHES. The second ban is a disjunction over `SSAS.Platform`, `SSAS.HR`
    // and `SSAS.GL`; one control would prove only that one of the three can fire and leave two prefixes
    // untested. All four witnesses are the composition root or the EF host, both of which legitimately
    // declare what they are asked about.
    var host = DeclaredDependencies.Of("SSAS.Host.API");
    var declared = DeclaredDependencies.Of(TenancyAssembly);

    Assert.Contains(
      DeclaredDependencies.Of("SSAS.BuildingBlocks.Infrastructure"),
      name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    Assert.Contains(host, name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));
    Assert.Contains(host, name => name.StartsWith("SSAS.HR", StringComparison.Ordinal));
    Assert.Contains(host, name => name.StartsWith("SSAS.GL", StringComparison.Ordinal));

    Assert.DoesNotContain(
      TenancyAssembly.GetReferencedAssemblies(),
      reference => reference.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true);
    Assert.DoesNotContain(
      declared, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));

    // Nor on any module, in either direction. A contract project that referenced Platform would put every
    // consumer back where it started.
    Assert.DoesNotContain(
      TenancyAssembly.GetReferencedAssemblies(),
      reference => reference.Name?.StartsWith("SSAS.Platform", StringComparison.Ordinal) == true ||
        reference.Name?.StartsWith("SSAS.HR", StringComparison.Ordinal) == true ||
        reference.Name?.StartsWith("SSAS.GL", StringComparison.Ordinal) == true);
    Assert.DoesNotContain(
      declared,
      name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal) ||
        name.StartsWith("SSAS.HR", StringComparison.Ordinal) ||
        name.StartsWith("SSAS.GL", StringComparison.Ordinal));
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

    // ⚠⚠⚠ THE MODULE SET IS DERIVED, AND IT USED TO BE TWO NAMES.
    //
    // This loop filtered on `SSAS.HR.` and `SSAS.GL.` — the modules that existed when it was written.
    // **Payroll and Attendance shipped afterwards and were never added, so a test whose NAME claims
    // "modules consume without referencing one another" asserted it over half the modules.**
    //
    // ***ADDING THE TWO NAMES WOULD HAVE BEEN THE SAME DEFECT ONE COMMIT LATER***, leaving a fifth module to
    // be forgotten by the mechanism that forgot these two. **The prefixes come from the folders under
    // `src/Modules` instead, so a module is covered the day its project exists** — the same derivation
    // `PersistenceArchitectureTests.Every_domain_and_application_project_is_actually_examined` uses against
    // the same failure.
    var modulePrefixes = ModulePrefixes();

    // The anti-vacuity control. An empty or short set makes both bans below hold over nothing, and the test
    // passes loudest exactly then. Four modules today: HR, GL, Payroll, Attendance.
    Assert.True(modulePrefixes.Length >= 4,
      $"only {modulePrefixes.Length} module prefixes were derived from src/Modules — the derivation has " +
      "stopped matching and the isolation bans below cover nothing: " + string.Join(", ", modulePrefixes));

    foreach (var (project, references) in projects)
    {
      if (modulePrefixes.Any(prefix => project.StartsWith(prefix, StringComparison.Ordinal)))
      {
        Assert.DoesNotContain(
          references,
          reference => reference.StartsWith("SSAS.Platform.", StringComparison.Ordinal));
      }

      if (project.StartsWith("SSAS.Platform.", StringComparison.Ordinal))
      {
        Assert.DoesNotContain(
          references,
          reference => modulePrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)));
      }
    }
  }

  // ---- THE MODULE PREFIXES, FROM DISK RATHER THAN FROM A LIST.
  //
  // `src/Modules/Finance` ships `SSAS.GL.*`, so the FOLDER name is not the prefix — the prefix is taken from
  // the project names the folder actually contains, cut at the second dot. That is why this reads projects
  // rather than directories.
  private static string[] ModulePrefixes() =>
    [.. Directory
      .EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "Modules"), "*.csproj", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Select(RepositoryPaths.ProjectNameFromFile)
      .Select(name => name.Split('.'))
      .Where(segments => segments.Length >= 2)
      .Select(segments => $"{segments[0]}.{segments[1]}.")
      .Distinct(StringComparer.Ordinal)
      .OrderBy(prefix => prefix, StringComparer.Ordinal)];

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
        // The company scope of the acting user, needed so a module can constrain a company-owned read or
        // write to the companies that user may reach (FP-006C4). Platform owns the answer; HR must ask it.
        nameof(CompanyAccessSummary),
        // T-090's standing answer. A three-valued enum rather than a bool, so `default` is the CLOSED
        // answer and no caller has to decide what "I could not find that employee" means.
        nameof(EmploymentStanding),
        nameof(IBranchTransferAuthorizer),
        nameof(IBranchTransferScope),
        // The trusted execution branch, needed so a module can record which branch an operation happened in
        // on a record that is not itself branch-owned (FP-006C3).
        nameof(ICurrentBranchResolver),
        // The acting tenant user, needed so a module can name WHO is asking when resolving scope. It carries
        // no roles, permissions, session or claims: what they may DO stays with the permission pipeline.
        nameof(ICurrentTenantUser),
        // ---- ADDED BY T-090, AND THE FIRST ON THIS SEAM POINTING PLATFORM -> MODULE.
        //
        // `IUserEmployeeResolver` answers which employee a tenant user is, from the PLATFORM database.
        // Whether that employment has ended lives on `Employee` in the TENANT database and `ADR-030`
        // Decision 4 forbids the foreign key that would let the seam read it — so it has to ask, and HR is
        // the authority. A status copy on the Platform side would be a second source of truth.
        //
        // It cannot live in `SSAS.HR.Contracts`: no Platform project references any module, and keeping
        // that true is exactly what `ADR-012` is for. Same edge as its neighbour, opposite direction.
        nameof(IEmploymentStandingDirectory),
        // A module's own permission definitions, offered to the one composed catalog. Platform composes and
        // validates; the module owns the names. Without it a module's permissions cannot be granted to any
        // role, which is the FP-006 release blocker this contract closes (ADR-012 r1.2).
        nameof(IPermissionCatalogContributor),
        nameof(ITenantBranchAccessResolver),
        nameof(ITenantCompanyAccessResolver),
        // ---- ADDED BY FP-008 PHASE 4 (DEC-POS-0035), AND DELIBERATELY THE NARROWEST THING THAT WORKS.
        //
        // A module has to render an amount's currency, and the currency lives on a Platform-owned Company
        // that `SSAS.HR.*` cannot reference under `ADR-012`. One method, one company, an ISO code returned
        // as an opaque STRING — the value object, its ISO-4217 set and its immutability rule all stay
        // Platform-side.
        //
        // This guard is why the addition is visible: widening the shared set widens every module's blast
        // radius, so it is enumerated rather than allowed to grow by habit. Three alternatives were refused
        // for that reason — widening `CompanyAccessSummary` (an authorization DTO), composing at the Host
        // (breaks module ownership of its own response shapes), and promoting the value object (an
        // ADR-level change `DEC-POS-0015` reserved for a multi-currency requirement).
        nameof(ITenantCompanyCurrencyLookup),
        nameof(ITenantUnitOfWork),
        // ---- ADDED BY T-091. THE SECOND OF TWO GUARDS ON A TERMINATED EMPLOYEE.
        //
        // HR terminates an employee and Platform owns the account, so HR has to ask. Called synchronously
        // from the handler rather than raised as an event: the domain-event road has no outbox, so a
        // failing consumer would leave a terminated employee with a live account and an operator who
        // reasonably believes nothing happened.
        //
        // Points module -> Platform, like `IUserEmployeeResolver` and unlike `IEmploymentStandingDirectory`.
        nameof(ITenantUserDeactivator),
        // ADR-030's identity-to-employee mapping, needed so a module can answer "is the acting user this
        // employee" (T-084). It sits beside ICurrentTenantUser because it is the same seam and the second
        // half of the same question: a module asks WHO is acting, then WHICH employee that is.
        //
        // Deliberately NOT in SSAS.Platform.Contracts. That project exists, is empty, and no module
        // references it — adopting it would open the first module-to-Platform project reference in the
        // product, which is a structural precedent rather than a defect fix.
        //
        // Its surface is one method taking the tenant user EXPLICITLY. Not an identity service: a contract
        // that read its subject from ambient state could not be asked about anyone else and would grow.
        nameof(IUserEmployeeResolver),
        // The data half of the permission contribution: a name and the description a tenant administrator
        // reads. Deliberately carries NO scope -- the composer stamps Tenant, so a module cannot mint
        // cross-tenant PlatformSupport authority (ADR-012 r1.2).
        nameof(ModulePermissionDefinition)
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
        path => RepositoryPaths.ProjectNameFromFile(path),
        path => (IReadOnlyCollection<string>)XDocument.Load(path)
          .Descendants("ProjectReference")
          .Select(reference => reference.Attribute("Include")?.Value)
          .Where(reference => !string.IsNullOrWhiteSpace(reference))
          .Select(reference => RepositoryPaths.ProjectName(reference!))
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
