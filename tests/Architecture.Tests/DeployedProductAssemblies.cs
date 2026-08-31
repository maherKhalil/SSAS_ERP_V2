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

  public static string[] NamesOf(IEnumerable<Assembly> assemblies) =>
    assemblies
      .Select(assembly => assembly.GetName().Name!)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();
}
