namespace SSAS.Architecture.Tests;

// ==================================================================================================
// ONE INTERPRETATION OF A REPOSITORY PATH, ON EVERY OPERATING SYSTEM (TEST-001).
// ==================================================================================================
//
// ---- THE DEFECT THIS EXISTS TO REMOVE.
//
// The architecture guards read `ProjectReference Include="..."` out of the .csproj files and reduced each
// one to a project name with `Path.GetFileNameWithoutExtension`. MSBuild writes those attributes with
// BACKSLASHES:
//
//     ..\..\..\BuildingBlocks\SSAS.BuildingBlocks.Api\SSAS.BuildingBlocks.Api.csproj
//
// On Windows a backslash is a directory separator, so that call returned `SSAS.BuildingBlocks.Api`. On
// Linux it is an ordinary filename character, so the same call returned the ENTIRE string minus `.csproj`.
//
// The result was not a crash. Every dependency name became unrecognisable, every lookup missed, and the
// guards compared real projects against an empty set — so the rules that enforce ADR-012 module isolation
// passed on Linux by examining nothing. That is the worst shape a guard can fail in: green, and blind.
//
// ---- WHY THIS IS NOT `OperatingSystem.IsWindows()`.
//
// Branching on the host would preserve two behaviours and leave the question "which one is correct?"
// permanently open. A `ProjectReference` is REPOSITORY METADATA, not a filesystem path on the machine
// running the test: MSBuild accepts both separators and normalises them itself, so the correct reading is
// the same everywhere. There is one rule here and no platform check anywhere in the file.
internal static class RepositoryPaths
{
  // Both separators, always, regardless of host. `Path.DirectorySeparatorChar` is deliberately not
  // consulted — what this parses is written by MSBuild, not by the local filesystem.
  private static readonly char[] Separators = ['\\', '/'];

  // The project name a `ProjectReference Include` attribute points at.
  //
  // Takes the last segment under either separator and drops the extension. `Path.GetFileNameWithoutExtension`
  // is not used on the SEGMENT either, because a project name legitimately contains dots
  // (`SSAS.BuildingBlocks.Api.csproj`) and only the FINAL extension may be removed.
  public static string ProjectName(string reference)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(reference);

    var trimmed = reference.Trim();
    var lastSeparator = trimmed.LastIndexOfAny(Separators);
    var segment = lastSeparator >= 0 ? trimmed[(lastSeparator + 1)..] : trimmed;

    var extension = segment.LastIndexOf('.');

    return extension > 0 ? segment[..extension] : segment;
  }

  // The same reduction for a path produced by the local filesystem — `Directory.EnumerateFiles`, say.
  //
  // It routes through the identical rule ON PURPOSE. Those paths carry the host's own separator and would
  // be handled correctly by the framework helper, but keeping two reductions in play is how the two drift
  // into disagreeing about what a project is called.
  public static string ProjectNameFromFile(string projectFilePath) => ProjectName(projectFilePath);
}
