using System.Reflection;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY ERROR A MAPPING SITE IS RESPONSIBLE FOR IS MAPPED, BECAUSE THE FALLTHROUGH IS A 500.
// ==================================================================================================
//
// ---- THE DEFECT.
//
// A handler returning `Result.Failure(SomeErrors.Whatever)` throws nothing and touches no logger. If the
// site that answers for that route has no arm for the code it falls through:
//
//     AttendanceApiErrorMapper.cs   _ => ApiErrors.WriteFailure
//     ApiErrors.cs                  WriteFailure = new(500, "request.failed")
//
// **The caller gets `500 request.failed` for a business refusal.** The handler reads correctly and the
// defect is an ABSENCE in a second file. `EmployeeApiErrorMapper.cs:108` says so in its own words —
// *"a code with no arm falls through to a 500 that would disclose by its own strangeness"* — six lines
// above six codes it does not map.
//
// ---- THE UNIT IS THE SITE, NOT THE MODULE. THIS FILE GOT THAT WRONG ONCE (T-078, corrected T-079).
//
// The first version concatenated a module's mapping sites and asked whether a code appeared in ANY of
// them. That treats a code mapped for one surface as mapped for all, and it **undercounted HR by half**:
// `Employee.PositionNotFound` is mapped in `EmployeeImportExportTransportContracts` for the import
// surface and NOT in `EmployeeApiErrorMapper`, which is what `POST /api/hr/employees` and
// `POST /{employeeId}/change-position` actually use. Both answer 500 today.
//
// *Is this code mapped?* is only meaningful relative to the surface that can raise it.
//
// ---- WHY RESPONSIBILITY IS DECLARED AND NOT DERIVED. THE ANSWER IS "IT CANNOT BE".
//
// Deriving route -> mapper -> reachable errors is not tractable here, and not for one reason:
//
//   1. `Microsoft.CodeAnalysis` is referenced by no test project. There is no call graph to walk.
//   2. The reachable set spans layers: `ChangeEmployeePositionCommandHandler` returns eight errors and
//      calls `Employee.ChangePosition`, which returns two more (`Employee.cs:571,579`).
//   3. It crosses interfaces — `IEmployeeRepository` — whose bindings are composition facts.
//   4. It crosses modules legitimately: `PayrollApiErrorMapper` maps `Company.*`, `GlApiErrorMapper`
//      maps `Company.*` and `Persistence.*`. A site's candidate set is not bounded by its module.
//
// So responsibility is DECLARED by code family. A wrong derivation accuses innocents; a declaration is
// reviewable, and a new family has to be added by a person — H9's argument for its exact inventory.
//
// ---- A SITE MAY MAP CODES OUTSIDE ITS FAMILIES, AND THAT IS NOT PENALISED.
//
// The import site maps three `Employee.Position*` codes because an import surfaces employee errors. Only
// the families a site is RESPONSIBLE for are asserted; anything extra is a site being helpful.
public sealed class ModuleErrorMappingArchitectureTests
{
  private sealed record MappingSite(
    string Site,
    Assembly[] DeclaringAssemblies,
    string SourcePath,
    string[] ResponsibleFamilies,
    string[] KnownUnmapped);

  private static readonly Assembly[] AttendanceAssemblies =
  [
    typeof(SSAS.Attendance.Domain.Calendars.WorkingCalendar).Assembly,
    typeof(SSAS.Attendance.Application.Permissions.AttendancePermissionNames).Assembly
  ];

  private static readonly Assembly[] GlAssemblies =
  [
    typeof(SSAS.GL.Domain.Accounts.Account).Assembly,
    typeof(SSAS.GL.Application.Permissions.GlPermissionNames).Assembly
  ];

  private static readonly Assembly[] PayrollAssemblies =
  [
    typeof(SSAS.Payroll.Domain.Runs.PayrollRun).Assembly,
    typeof(SSAS.Payroll.Application.Permissions.PayrollPermissionNames).Assembly
  ];

  private static readonly Assembly[] HrAssemblies =
  [
    typeof(SSAS.HR.Domain.Employees.Employee).Assembly,
    typeof(SSAS.HR.Application.Permissions.HrPermissionNames).Assembly
  ];

  private static MappingSite[] Sites() =>
  [
    new("AttendanceApiErrorMapper", AttendanceAssemblies,
      Path.Combine("src", "Modules", "Attendance", "SSAS.Attendance.API", "AttendanceApiErrorMapper.cs"),
      ["Attendance"], []),

    new("GlApiErrorMapper", GlAssemblies,
      Path.Combine("src", "Modules", "Finance", "SSAS.GL.API", "GlApiErrorMapper.cs"),
      ["Gl"], []),

    // ---- TWO OVERTIME-TIER ERRORS DECLARED AND NOT MAPPED. FOUND, DELIBERATELY NOT FIXED.
    //
    // A status code is a contract decision owned by the surface, not by the test that noticed.
    new("PayrollApiErrorMapper", PayrollAssemblies,
      Path.Combine("src", "Modules", "Payroll", "SSAS.Payroll.API", "PayrollApiErrorMapper.cs"),
      ["Payroll"], []),

    new("DepartmentApiErrorMapper", HrAssemblies,
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Departments", "DepartmentApiErrorMapper.cs"),
      ["Department"], []),

    new("PositionApiErrorMapper", HrAssemblies,
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Positions", "PositionApiErrorMapper.cs"),
      ["Position"], []),

    // ---- SIX. THE DEFECT THAT PROVED THE MODULE-LEVEL VERSION WRONG.
    //
    // `PositionEndpointRouteBuilderExtensions.cs:806-808` says *"The EMPLOYEE mapper ... Its
    // `Employee.Position*` arms are the ones that describe an unusable destination here."* **There are
    // none.** `POST /api/hr/employees` with an unknown position and every `Employee.Position*` refusal on
    // `change-position` answer 500 today.
    //
    // They are listed together because `BR-PLT-0002` requires them to share a wire answer: mapping some
    // while others fall through to 500 makes the two distinguishable, which is the disclosure the
    // collapse exists to prevent. **Ruling three of six would be worse than ruling none.**
    new("EmployeeApiErrorMapper", HrAssemblies,
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Employees", "EmployeeApiErrorMapper.cs"),
      ["Employee"], []),

    // ---- FIVE MORE, FOUND ONLY BY SPLITTING THE SITES. ALL REACHABLE, ALL VERIFIED.
    //
    // `ImportEmployeesCommandHandler.cs:624-626` returns the run factory's failure directly, and
    // `ExportEmployeesQueryHandler.cs:174-177` does the same. `ImportKey.Create`'s failure is returned at
    // `ImportEmployeesCommandHandler.cs:97-101`. So a bad import key, file name or row count answers 500.
    //
    // **These were nearly reported as unreachable.** A search for `EmployeeImportRun.Create` returned no
    // call sites — because the factories are `Validated`, `Applied` and `Refused`. An absence is only
    // evidence when the name searched for is the right one.
    new("EmployeeImportExportTransportContracts", HrAssemblies,
      Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Employees", "EmployeeImportExportTransportContracts.cs"),
      ["EmployeeImport", "EmployeeImportRun", "EmployeeExportRun"], [])
  ];

  // ---- THE REGISTER IS AN EXACT SET, NOT A CEILING.
  //
  // A "no more than N" rule passes when one error is mapped and another added — the swap argument from
  // T-077. So mapping one of these thirteen must also fail: the register records DEBT, and paying it down
  // is a thing someone should have to write down.
  [Theory]
  [MemberData(nameof(SiteNames))]
  public void Every_error_a_site_is_responsible_for_is_mapped_rather_than_falling_through(string siteName)
  {
    var site = Sites().Single(candidate => candidate.Site == siteName);

    var responsible = ResponsibleCodes(site);

    // NOT VACUOUS, AND PER SITE RATHER THAN PER MODULE OR AGGREGATED. An aggregate is satisfied by the
    // largest site while the smallest goes unexamined — T-073's one-policy-wide plane. A site whose
    // enumeration comes back empty is a broken sweep or a misspelt family, not a clean site.
    Assert.True(
      responsible.Length > 0,
      $"{siteName} is responsible for families [{string.Join(", ", site.ResponsibleFamilies)}] and no " +
      "declared Error matches any of them. Either the family is misspelt or the sweep is broken — an " +
      "empty enumeration would make the assertion below pass over nothing.");

    var source = ReadRepositoryFile(site.SourcePath);

    var unmapped = responsible
      .Where(code => !source.Contains($"\"{code}\" =>", StringComparison.Ordinal))
      .OrderBy(code => code, StringComparer.Ordinal)
      .ToArray();

    // The message names the consequence, not a remedy. WHICH status a business error deserves is a
    // contract decision owned by the surface, and a test asserting one would be choosing it.
    Assert.True(
      unmapped.SequenceEqual(site.KnownUnmapped.OrderBy(code => code, StringComparer.Ordinal), StringComparer.Ordinal),
      $"{siteName}'s responsible errors and its arms have diverged. An unmapped error falls through to a " +
      "500 for what is usually a business refusal — no exception, no log entry, and a handler that reads " +
      "correctly. If one was just mapped, remove it from KnownUnmapped in the same commit." +
      $"{Environment.NewLine}expected unmapped: {Format(site.KnownUnmapped)}" +
      $"{Environment.NewLine}actual unmapped:   {Format(unmapped)}");
  }

  // Named rather than derived from `Sites()`, so a site silently dropping out of that list fails here
  // instead of reducing the theory to six cases nobody counts.
  public static TheoryData<string> SiteNames => new()
  {
    "AttendanceApiErrorMapper",
    "GlApiErrorMapper",
    "PayrollApiErrorMapper",
    "DepartmentApiErrorMapper",
    "PositionApiErrorMapper",
    "EmployeeApiErrorMapper",
    "EmployeeImportExportTransportContracts"
  };

  [Fact]
  public void The_site_list_and_the_named_cases_are_the_same_set()
  {
    Assert.Equal(
      SiteNames.Select(row => (string)row[0]).OrderBy(name => name, StringComparer.Ordinal),
      Sites().Select(site => site.Site).OrderBy(name => name, StringComparer.Ordinal));
  }

  // ---- EVERY DECLARED ERROR IS SOMEBODY'S. THE HALF THAT CATCHES A FAMILY NOBODY CLAIMED.
  //
  // Without this, adding an error in a NEW family — `Timesheet.*` — would be covered by no site and
  // examined by nothing, and every per-site assertion above would stay green. That is the same shape as a
  // route outside an inventory, and it is the way this guard would quietly stop covering the module it
  // was written for.
  [Fact]
  public void Every_declared_error_family_is_owned_by_exactly_one_site()
  {
    var claimed = Sites()
      .SelectMany(site => site.ResponsibleFamilies)
      .ToArray();

    Assert.Equal(claimed.Length, claimed.Distinct(StringComparer.Ordinal).Count());

    var declared = Sites()
      .SelectMany(site => site.DeclaringAssemblies)
      .Distinct()
      .SelectMany(ErrorCodesIn)
      .Select(Family)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(family => family, StringComparer.Ordinal)
      .ToArray();

    var unclaimed = declared
      .Where(family => !claimed.Contains(family, StringComparer.Ordinal))
      .ToArray();

    Assert.True(
      unclaimed.Length == 0,
      "A declared error family is claimed by no mapping site, so nothing asserts its codes are mapped and " +
      "every arm of this guard passes over it. Give it to the site that answers for the routes raising " +
      $"it:{Environment.NewLine}{string.Join(Environment.NewLine, unclaimed)}");
  }

  private static string[] ResponsibleCodes(MappingSite site) =>
  [
    .. site.DeclaringAssemblies
      .Distinct()
      .SelectMany(ErrorCodesIn)
      .Where(code => site.ResponsibleFamilies.Contains(Family(code), StringComparer.Ordinal))
      .Distinct(StringComparer.Ordinal)
      .OrderBy(code => code, StringComparer.Ordinal)
  ];

  // Reflection over the fields themselves. A `static readonly Error` is how every module declares one and
  // the field's own `Code` is the value a mapper switches on — no regex over string literals, which would
  // also match a code quoted in a comment or repeated in a message.
  private static IEnumerable<string> ErrorCodesIn(Assembly assembly) =>
    assembly.GetTypes()
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
      .Where(field => field.IsInitOnly && field.FieldType == typeof(Error))
      .Select(field => ((Error)field.GetValue(null)!).Code)
      .Where(code => !string.IsNullOrWhiteSpace(code));

  private static string Family(string code)
  {
    var separator = code.IndexOf('.', StringComparison.Ordinal);

    return separator <= 0 ? code : code[..separator];
  }

  private static string Format(IEnumerable<string> codes)
  {
    var listed = codes.ToArray();

    return listed.Length == 0 ? "(none)" : string.Join(", ", listed);
  }

  private static string ReadRepositoryFile(string relativePath) =>
    File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

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
