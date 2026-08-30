using System.Reflection;
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

  // ⚠ THE CONSTANT IS DECLARED FOUR TIMES, SO THE RISK IS DRIFT BETWEEN THEM.
  //
  // It cannot live in the shared `ApiErrors`: `The_shared_api_project_names_no_business_concept` refuses a
  // business noun in BuildingBlocks, and it refused this very constant when it was written there. **So the
  // repetition is the rule being obeyed** -- and the cost of obeying it is that four declarations can
  // disagree. One mapper answering `company.selection_requires` or a 409 would be invisible to every other
  // test, and visible to a caller of exactly one module.
  [Fact]
  public void All_four_declarations_of_the_precondition_code_agree_and_differ_from_the_generic_one()
  {
    var declared = ModuleApiAssemblies()
      .SelectMany(assembly => assembly.GetTypes())
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
      .Where(field => field.FieldType == typeof(ApiError)
        && field.Name == "CompanySelectionRequired")
      .Select(field => (Owner: field.DeclaringType!.Name, Error: (ApiError)field.GetValue(null)!))
      .ToArray();

    // The floor reads the quantity the assertions read. Four mappers answer this code; a reflection walk
    // that found fewer would let 'they all agree' be true of a set too small to disagree.
    Assert.True(declared.Length >= 4,
      $"only {declared.Length} declarations of CompanySelectionRequired were found across the module API " +
      "assemblies; four mappers answer this code, so the walk has degraded.");

    var disagreeing = declared
      .Where(row => row.Error.Code != "company.selection_required" || row.Error.StatusCode != 400)
      .Select(row => $"{row.Owner}: {row.Error.StatusCode} {row.Error.Code}")
      .ToArray();

    Assert.True(disagreeing.Length == 0,
      "a mapper declares the company precondition differently from the others, so callers of that one " +
      "module get a different answer for the same condition:\n  " + string.Join("\n  ", disagreeing));

    // Distinct from the generic code, which is the whole point of the item...
    Assert.NotEqual(ApiErrors.RequestInvalid.Code, declared[0].Error.Code);

    // ...but deliberately the SAME status. It is a client error either way; the actionable difference is
    // carried by the code, because the code is what a client branches on.
    Assert.Equal(ApiErrors.RequestInvalid.StatusCode, declared[0].Error.StatusCode);
  }

  private static Assembly[] ModuleApiAssemblies() =>
    [.. Directory
      .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.API.dll")
      .Select(RepositoryPaths.ProjectName)
      .Where(name => name is not null)
      .Distinct(StringComparer.Ordinal)
      .Select(name => Assembly.Load(name!))];

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
