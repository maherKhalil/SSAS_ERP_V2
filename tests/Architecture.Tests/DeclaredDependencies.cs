using System.Reflection;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// WHAT A PROJECT CAN SEE, AS OPPOSED TO WHAT IT HAPPENS TO USE (272).
// ==================================================================================================
//
// ---- THE DEFECT THIS EXISTS TO REMOVE, MEASURED RATHER THAN ARGUED.
//
// `Assembly.GetReferencedAssemblies()` reads EMITTED METADATA, and **the C# compiler omits a reference no
// type is taken from**. So a project can declare a forbidden dependency in its `.csproj`, build, and report
// that dependency as ABSENT.
//
// Measured in item `269` (`3b9728c`): adding a forbidden `ProjectReference` to `SSAS.HR.API.csproj` left an
// assertion over `GetReferencedAssemblies()` GREEN, and the three pre-existing HR boundary guards stayed
// green under the same plant. Item `T-153` measured the same thing independently, item `169` reads the
// output directory for the same reason, and `EmploymentTypeAssumptionTests` records it at its own site.
// `Architecture-Principles.md` states it as a principle.
//
// ---- WHY THE DIFFERENCE MATTERS, AND IT IS NOT PEDANTRY.
//
// A declared-but-unused reference is LATENT CAPABILITY. The `.csproj` edit is merged, the friction is gone,
// and the next developer reaching for a forbidden type meets nothing in the way. **`can see` catches the
// capability; `does use` catches the consequence** — and by then the boundary has already been crossed.
//
// ⚠ SO NEITHER READING IS SIMPLY BETTER. A criterion about actual coupling — *reaches X only through the
// published contract* — is correctly served by `GetReferencedAssemblies`, and using this instead would
// report a project that declares a reference it never touches. **Pick the instrument the criterion's
// wording asks for:** *may not reference* / *cannot see* / *regardless of use* wants this one.
//
// ---- WHAT IT READS, AND THE BOUND ON THAT.
//
// `ProjectReference` and `PackageReference` `Include` attributes, matched ON THE ELEMENT rather than on the
// name. ⚠ A bare substring search for a forbidden name reports a false positive on PROSE: a comment in
// `SSAS.HR.Domain.csproj` cites `SSAS.Platform.Domain` as a naming precedent, and an early version of the
// `269` guard called that a violation on a clean tree.
//
// ⚠⚠ IT READS ONE LINE AT A TIME. MSBuild writes these elements on a single line and every project in this
// repository does; an `Include` split across lines would be invisible here. That is a real bound and it is
// stated rather than guarded, because no such element exists to test against.
internal static class DeclaredDependencies
{
  // The declared dependency names of the project that builds this assembly.
  //
  // Project references are reduced through `RepositoryPaths.ProjectName` — the same one rule, so this
  // cannot drift from the guards that already parse those attributes. Package references carry the package
  // name in `Include` directly.
  public static string[] Of(Assembly assembly)
  {
    ArgumentNullException.ThrowIfNull(assembly);

    return Of(assembly.GetName().Name!);
  }

  public static string[] Of(string assemblyName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

    return File.ReadAllLines(ProjectFileOf(assemblyName))
      .Select(line => line.Trim())
      .Where(line =>
        line.Contains("ProjectReference", StringComparison.Ordinal) ||
        line.Contains("PackageReference", StringComparison.Ordinal))
      .Select(IncludeValue)
      .Where(value => value.Length > 0)
      .Select(value => value.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
        ? RepositoryPaths.ProjectName(value)
        : value)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();
  }

  // ⚠ THROWS RATHER THAN RETURNING NOTHING, AND THAT IS THE WHOLE SAFETY PROPERTY OF THIS CLASS.
  //
  // Every caller uses the result for a BAN — `Assert.Empty(forbidden)` or `Assert.DoesNotContain`. An empty
  // set satisfies all of those, so a helper that answered "no project file found" with `[]` would turn
  // every guard built on it green and blind, which is precisely the failure `GetReferencedAssemblies`
  // already produces and this class exists to end. A missing or duplicated project file is a broken
  // instrument and must say so out loud.
  //
  // Zero declared dependencies is NOT an error: `SSAS.HR.Contracts` is a leaf and legitimately declares
  // none. Only a missing FILE is.
  public static string ProjectFileOf(string assemblyName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

    var matches = Directory
      .GetFiles(Path.Combine(RepositoryRoot(), "src"), $"{assemblyName}.csproj", SearchOption.AllDirectories)
      .OrderBy(path => path, StringComparer.Ordinal)
      .ToArray();

    return matches.Length == 1
      ? matches[0]
      : throw new InvalidOperationException(
        $"expected exactly one src/**/{assemblyName}.csproj and found {matches.Length}. Every guard built " +
        "on this reads a ban from it, and a ban over an unread file passes silently.");
  }

  // `Include="..."`, taking the value between the first pair of quotes after the attribute name. Returns
  // empty for a line carrying the element name without the attribute — a closing tag, or a comment.
  private static string IncludeValue(string line)
  {
    const string marker = "Include=\"";

    var start = line.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
    {
      return string.Empty;
    }

    var value = line[(start + marker.Length)..];
    var end = value.IndexOf('"', StringComparison.Ordinal);

    return end < 0 ? string.Empty : value[..end];
  }

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
         directory is not null;
         directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new InvalidOperationException("Repository root not found.");
  }
}
