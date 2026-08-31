using System.Reflection;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE ASSEMBLIES THE PRODUCT ACTUALLY SHIPS, READ FROM DISK (item 169).
// ==================================================================================================
//
// ⚠ A HAND-MAINTAINED ASSEMBLY LIST IS A GUARD THAT SILENTLY STOPS COVERING THE PRODUCT AS IT GROWS.
// It does not fail on the day a new module appears; it simply stops looking there, and every assertion
// over it stays green. That is a latent vacuity with a schedule, and unlike a stated scope limit a reader
// cannot see it.
//
// ---- ⚠ WHY THE OUTPUT DIRECTORY AND NOT `GetReferencedAssemblies()`.
//
// The compiler omits a reference whose types are never used, so a test project that references a new
// module but touches none of its types would report the reference MISSING -- and the check would then
// agree with the stale list for the wrong reason. **Every project reference is copied to the output
// directory whether its types are used or not**, so the deployed set is independent of what the test code
// happens to mention.
//
// ---- WHY `RepositoryPaths` AND NOT THE FRAMEWORK HELPER.
//
// `RepositoryPathPortabilityTests` bans `Path.GetFileNameWithoutExtension` across these tests. Its stated
// reason is MSBuild `Include` attributes, whose backslashes that helper misreads on Linux -- and the paths
// here come from `Directory.GetFiles`, so that reason does not apply to this call. **The ban is blanket
// on purpose**: the two uses are indistinguishable at a glance, which is how the original defect arrived.
// Complying is cheaper than arguing, and an exception would remove the property the guard exists to hold.
//
// ---- WHAT THIS DOES NOT ESTABLISH.
//
// A module whose assembly is not referenced by `SSAS.Architecture.Tests` at all is not deployed here and
// is invisible to this too. That is a narrower gap than a hand-written list -- adding a project reference
// is how a module joins the test build in the first place -- but it is not nothing, and it is the reason
// this is a floor on coverage rather than a proof of it.
public static class DeployedProductAssemblies
{
  public static string[] NamesWithSuffix(params string[] suffixes) =>
    Directory.GetFiles(AppContext.BaseDirectory, "SSAS.*.dll")
      .Select(RepositoryPaths.ProjectNameFromFile)
      .Where(name => suffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal)))
      .Distinct(StringComparer.Ordinal)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

  // ==================================================================================================
  // WHICH DEPLOYED ASSEMBLIES ARE MODULES (item 171) -- A FACT ABOUT THE LAYOUT, NOT A SECOND LIST.
  // ==================================================================================================
  //
  // Item 169 left `ModuleErrorMapping` and `ModuleEnablement` unfixed because their lists are per-module
  // TYPES, and "which types count as a module" is a convention judgement. Asked the other way round it is
  // answerable without judgement: **a module is a project under `src/Modules/`.**
  //
  // ⚠ THE DIRECTORY NAME IS NOT THE ASSEMBLY PREFIX -- `src/Modules/Finance/` holds the `SSAS.GL.*`
  // projects -- so the project directory names are read, not the module folder names.
  //
  // This is derived from the repository layout rather than written down, which is what stops it being
  // "the defect wearing a hat": a new module appears here the moment its project directory exists, with
  // nobody deciding it is a module.
  public static string[] ModuleProjectNames(string suffix)
  {
    var modules = Path.Combine(RepositoryRoot(), "src", "Modules");

    return Directory.EnumerateDirectories(modules)
      .SelectMany(Directory.EnumerateDirectories)
      .Select(path => new DirectoryInfo(path).Name)
      .Where(name => name.EndsWith(suffix, StringComparison.Ordinal))
      .Distinct(StringComparer.Ordinal)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();
  }

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
    {
      directory = directory.Parent;
    }

    return directory!.FullName;
  }

  public static string[] NamesOf(IEnumerable<Assembly> assemblies) =>
    assemblies
      .Select(assembly => assembly.GetName().Name!)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();
}
