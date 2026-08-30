using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Api.Transport;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// A PRECONDITION KEEPS ITS OWN CODE IN EVERY MODULE THAT ANSWERS IT (T-268).
// ==================================================================================================
//
// `Company.SelectionRequired` was one of 129 domain codes collapsing into `request.invalid`. 128 of those
// say **fix your input**; this one says *an active company must be selected before company-scoped
// operations* — **you are not in a state where this input means anything.** The remedy is a different call
// followed by the same request unchanged, and a client that cannot distinguish it from a bad field name
// cannot offer the company picker.
//
// It is answered by four separate mappers. **The failure this guards is a fifth module adding an arm and
// reaching for `RequestInvalid` because that is what the neighbouring arms do** — which would restore the
// collapse for that module alone, silently, and only for callers of that one endpoint.
public sealed class PreconditionCodeArchitectureTests
{
  private const string PreconditionDomainCode = "Company.SelectionRequired";

  [Fact]
  public void No_mapper_answers_the_company_precondition_with_the_generic_request_error()
  {
    var arms = MapperArmsFor(PreconditionDomainCode);

    // ⚠ THE FLOOR READS THE QUANTITY THE ASSERTION READS: arms found for this code, which is exactly what
    // `collapsed` filters. Four exist. A regex that stopped matching arms would otherwise report "none
    // collapsed" about a set it never populated.
    Assert.True(arms.Count >= 4,
      $"only {arms.Count} mapper arms for {PreconditionDomainCode} were found; four exist, so the arm scan " +
      "has degraded and 'none collapsed' would be a statement about nothing.");

    var collapsed = arms
      .Where(arm => arm.Target.EndsWith("RequestInvalid", StringComparison.Ordinal))
      .Select(arm => $"{arm.File}: {arm.Target}")
      .ToArray();

    Assert.True(collapsed.Length == 0,
      "a mapper answers a PRECONDITION with the generic request error, so a caller of that module cannot " +
      "tell 'select a company first' from 'a field is malformed':\n  " + string.Join("\n  ", collapsed) +
      "\n\nUse ApiErrors.CompanySelectionRequired. The status is the category; the code is the instruction.");
  }

  // ⚠ THE CONTROL ON THE MATCHER ABOVE. The arm regex is the whole instrument, and a ban over arms it
  // failed to parse is green for the wrong reason. This proves it still recognises a collapsed arm by
  // finding the ones that legitimately exist for OTHER codes.
  [Fact]
  public void The_arm_scanner_still_recognises_arms_that_do_answer_with_the_generic_request_error()
  {
    var genericArms = MapperFiles()
      .SelectMany(file => Regex.Matches(File.ReadAllText(file), ArmPattern)
        .Select(match => match.Groups[2].Value))
      .Where(target => target.EndsWith("RequestInvalid", StringComparison.Ordinal))
      .ToArray();

    Assert.True(genericArms.Length >= 50,
      $"only {genericArms.Length} arms answering with the generic request error were found; 128 domain " +
      "codes still collapse into it, so the arm regex has stopped matching and the test above proves nothing.");
  }

  [Fact]
  public void The_precondition_code_is_distinct_from_the_generic_one()
  {
    Assert.NotEqual(ApiErrors.RequestInvalid.Code, ApiErrors.CompanySelectionRequired.Code);
    Assert.Equal("company.selection_required", ApiErrors.CompanySelectionRequired.Code);

    // Same status deliberately: it is a client error either way, and the actionable difference is carried
    // by the code rather than by the category.
    Assert.Equal(ApiErrors.RequestInvalid.StatusCode, ApiErrors.CompanySelectionRequired.StatusCode);
  }

  private const string ArmPattern = "\"([A-Za-z._]+)\"\\s*=>\\s*([A-Za-z.]+)\\s*,";

  private static List<(string File, string Target)> MapperArmsFor(string domainCode) =>
    [.. MapperFiles()
      .SelectMany(file => Regex.Matches(File.ReadAllText(file), ArmPattern)
        .Where(match => match.Groups[1].Value == domainCode)
        .Select(match => (File: Path.GetFileName(file), Target: match.Groups[2].Value)))];

  private static string[] MapperFiles() =>
    [.. Directory
      .EnumerateFiles(FindRepositoryRoot(), "*ApiErrorMapper.cs",
        SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
          StringComparison.Ordinal)
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
          StringComparison.Ordinal))];
  // The suite's own idiom for this, repeated rather than shared because every other file in this project
  // carries its own copy and a lone shared version would be the odd one out.
  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
      directory is not null;
      directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
