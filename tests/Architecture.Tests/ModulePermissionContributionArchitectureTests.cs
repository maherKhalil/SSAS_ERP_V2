using System.Reflection;
using System.Xml.Linq;
using SSAS.BuildingBlocks.Tenancy.Permissions;
using SSAS.HR.Application.Permissions;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE MODULE PERMISSION CONTRIBUTION SEAM (FP-006P, ADR-012 r1.2).
// ==================================================================================================
//
// A role may only be granted a permission the catalog defines. Platform owns the catalog and may not
// reference a business module, so HR's five `HR.Employees.*` constants belonged to no catalog: nothing
// could grant them, and every Employee endpoint refused every caller while every test passed, because
// tests mint permission claims and never travel the assignment path.
//
// These tests protect the properties that make the fix a seam rather than a shortcut: the contract is
// shared and owned by neither side, Platform never learns a module's vocabulary, the Host registers the
// set explicitly, and a contributed permission is held to the same rules as a Platform-owned one.
public sealed class ModulePermissionContributionArchitectureTests
{
  // ---- 1 AND 2. NEITHER SIDE REFERENCES THE OTHER.
  //
  // The whole point of the indirection, restated for the permission seam specifically so it fails on its
  // own terms rather than only inside the general dependency test.
  [Fact]
  public void Platform_and_hr_reach_the_permission_seam_without_referencing_one_another()
  {
    var projects = ProjectReferences();

    Assert.Contains("SSAS.BuildingBlocks.Tenancy", projects["SSAS.Platform.Application"]);
    Assert.Contains("SSAS.BuildingBlocks.Tenancy", projects["SSAS.HR.Application"]);

    Assert.DoesNotContain(
      projects["SSAS.Platform.Application"],
      reference => reference.StartsWith("SSAS.HR.", StringComparison.Ordinal));

    Assert.DoesNotContain(
      projects["SSAS.HR.Application"],
      reference => reference.StartsWith("SSAS.Platform.", StringComparison.Ordinal));
  }

  // ---- 3. THE CONTRACT IS THE APPROVED SHARED ONE.
  //
  // Not a Platform interface a module implements, and not an HR interface Platform reads: one contract in
  // the module-facing set, which is what lets a second module arrive without either side changing.
  [Fact]
  public void The_permission_contributor_is_a_shared_module_facing_contract()
  {
    var contract = typeof(IPermissionCatalogContributor);

    Assert.True(contract.IsInterface);
    Assert.Equal("SSAS.BuildingBlocks.Tenancy", contract.Assembly.GetName().Name);

    // HR implements it; Platform consumes it. Neither owns it.
    Assert.True(contract.IsAssignableFrom(typeof(HrPermissionCatalogContributor)));
    Assert.Equal("SSAS.HR.Application", typeof(HrPermissionCatalogContributor).Assembly.GetName().Name);
    Assert.Equal("SSAS.Platform.Application", typeof(ComposedPermissionCatalog).Assembly.GetName().Name);

    // ---- AND IT CANNOT CARRY A SCOPE.
    //
    // PlatformSupport is cross-tenant operator authority. A scope property here would be something a future
    // module could set and a reviewer would have to catch; with no property the escalation is
    // unrepresentable, and the composer stamps Tenant.
    Assert.DoesNotContain(
      typeof(ModulePermissionDefinition).GetProperties(),
      property => property.PropertyType == typeof(PermissionScope));
  }

  // ---- 4. PLATFORM NEVER LEARNS A MODULE'S VOCABULARY.
  //
  // A project reference is not the only kind of coupling. Writing a module permission name into Platform
  // would put that module's decisions inside Platform with the reference filed off, so the SOURCE is
  // checked, not just the assembly graph.
  [Fact]
  public void The_platform_catalog_contains_no_module_permission_literal()
  {
    var platformSources = Directory
      .EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "Platform"), "*.cs", SearchOption.AllDirectories)
      .Where(path => !IsBuildOutput(path))
      .ToArray();

    Assert.NotEmpty(platformSources);

    foreach (var path in platformSources)
    {
      var source = File.ReadAllText(path);

      Assert.DoesNotContain(ModuleLiteralProbe("HR"), source, StringComparison.Ordinal);
      Assert.DoesNotContain(ModuleLiteralProbe("GL"), source, StringComparison.Ordinal);
    }

    // And nothing module-shaped reached Platform's own set by another route.
    Assert.DoesNotContain(
      new PlatformPermissionCatalog().All,
      definition => definition.Name.Value.StartsWith("HR.", StringComparison.Ordinal) ||
        definition.Name.Value.StartsWith("GL.", StringComparison.Ordinal));
  }

  // ---- 5. THE HOST REGISTERS THE CONTRIBUTION EXPLICITLY.
  //
  // The composition root is the one place permitted to know which modules exist. If this line disappears,
  // HR's permissions silently stop being grantable, which is exactly the failure this slice closed.
  [Fact]
  public void The_host_registers_the_hr_permission_contributor_explicitly()
  {
    var program = File.ReadAllText(
      Path.Combine(RepositoryRoot(), "src", "Host", "SSAS.Host.API", "Program.cs"));

    Assert.Contains(
      "AddSingleton<IPermissionCatalogContributor, HrPermissionCatalogContributor>",
      program,
      StringComparison.Ordinal);

    // ---- AND IT COMPOSES THE CATALOG AT STARTUP.
    //
    // The catalog is a singleton, so a duplicate or malformed contribution would otherwise surface as a 500
    // on whichever request authorized first rather than as a host that refuses to start.
    Assert.Contains("GetRequiredService<IPermissionCatalog>()", program, StringComparison.Ordinal);
  }

  // ---- 6. NO REFLECTION-BASED DISCOVERY.
  //
  // ADR-012 forbids it, and a catalog that scanned assemblies would grant permissions nobody registered.
  [Fact]
  public void Permission_composition_uses_no_assembly_scanning()
  {
    var composer = ComposerSource();

    foreach (var probe in new[]
    {
      "GetTypes()", "GetExportedTypes()", "Assembly.Load", "AppDomain", "Activator."
    })
    {
      Assert.DoesNotContain(probe, composer, StringComparison.Ordinal);
    }

    // The set arrives as an injected enumerable, which is registration rather than discovery.
    var contributorParameter = typeof(ComposedPermissionCatalog)
      .GetConstructors()
      .Single()
      .GetParameters()
      .SingleOrDefault(parameter =>
        parameter.ParameterType == typeof(IEnumerable<IPermissionCatalogContributor>));

    Assert.NotNull(contributorParameter);
  }

  // ---- 7. CONTRIBUTED PERMISSIONS ARE HELD TO THE CATALOG'S OWN RULES.
  //
  // A separate, laxer validation path for module permissions would make the grammar advisory. The composer
  // runs the one canonical name validation and carries no grammar of its own.
  [Fact]
  public void The_composer_reuses_the_canonical_permission_name_validation()
  {
    var composer = ComposerSource();

    Assert.Contains("PermissionName.Create(", composer, StringComparison.Ordinal);

    // No second grammar. Any re-implementation would have to split the name or inspect its characters, so
    // the absence of both is what makes "it reuses the value object" a fact rather than a claim.
    Assert.DoesNotContain(".Split(", composer, StringComparison.Ordinal);
    Assert.DoesNotContain("IsAsciiLetter", composer, StringComparison.Ordinal);
    Assert.DoesNotContain("IsIdentifierSegment", composer, StringComparison.Ordinal);
  }

  // ---- 9. THE COMPOSED CATALOG CANNOT BE MUTATED AFTER CONSTRUCTION.
  //
  // Everything is decided once, at composition. A registration method or a settable member would mean the
  // set a request is authorized against is not necessarily the set composition approved.
  [Fact]
  public void The_composed_catalog_is_immutable_after_construction()
  {
    var catalog = typeof(ComposedPermissionCatalog);

    Assert.True(catalog.IsSealed);

    Assert.All(
      catalog.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
      field => Assert.True(field.IsInitOnly));

    Assert.Empty(catalog.GetProperties().Where(property => property.CanWrite));
    Assert.DoesNotContain(
      catalog.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly),
      method => method.Name.StartsWith("Add", StringComparison.Ordinal) ||
        method.Name.StartsWith("Register", StringComparison.Ordinal) ||
        method.Name.StartsWith("Remove", StringComparison.Ordinal));
  }

  // ---- 10. THE DEFINITIONS STAY CODE-OWNED, AND DERIVE FROM ONE SOURCE.
  //
  // Permissions are not tenant-defined (DEC-EMP-0030), and HR must not carry two spellings of a name: the
  // constant the endpoints require IS the constant the catalog registers, or a permission would be
  // grantable and authorize nothing while looking correct in both places.
  [Fact]
  public void The_hr_contribution_derives_from_the_single_code_owned_name_set()
  {
    var contributed = new HrPermissionCatalogContributor().Permissions
      .Select(permission => permission.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    var constants = typeof(HrPermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral && field.FieldType == typeof(string))
      .Select(field => (string)field.GetRawConstantValue()!)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(constants, contributed);

    // The contributor NAMES the constants rather than restating the strings, so the two cannot drift.
    var source = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Modules", "HR", "SSAS.HR.Application", "Permissions",
      "HrPermissionCatalogContributor.cs"));

    Assert.DoesNotContain(ModuleLiteralProbe("HR"), source, StringComparison.Ordinal);
    Assert.Contains("HrPermissionNames.ViewEmployees", source, StringComparison.Ordinal);
  }

  // A quoted module prefix, built rather than written, so this file does not itself contain the literal it
  // forbids.
  private static string ModuleLiteralProbe(string modulePrefix) =>
    string.Concat("\"", modulePrefix, ".");

  private static string ComposerSource() => File.ReadAllText(Path.Combine(
    RepositoryRoot(), "src", "Platform", "SSAS.Platform.Application", "Permissions",
    "ComposedPermissionCatalog.cs"));

  private static bool IsBuildOutput(string path) =>
    path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
    path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

  private static Dictionary<string, IReadOnlyCollection<string>> ProjectReferences()
  {
    var root = RepositoryRoot();

    return Directory
      .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
      .Where(path => !IsBuildOutput(path))
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
