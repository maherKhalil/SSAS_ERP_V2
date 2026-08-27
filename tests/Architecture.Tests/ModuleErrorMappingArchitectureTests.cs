using System.Reflection;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY ERROR A MODULE DECLARES IS MAPPED, BECAUSE THE FALLTHROUGH IS A 500 (T-076, T-078).
// ==================================================================================================
//
// ---- THE DEFECT, AND WHY IT IS INVISIBLE IN THE FILE THAT CAUSES IT.
//
// A handler returning `Result.Failure(SomeErrors.Whatever)` throws nothing and touches no logger. If the
// module's mapper has no arm for that code it falls through:
//
//     AttendanceApiErrorMapper.cs   _ => ApiErrors.WriteFailure
//     ApiErrors.cs                  WriteFailure = new(500, "request.failed")
//
// **The caller gets `500 request.failed` for a business refusal.** The handler reads correctly, the domain
// error reads correctly, and the defect is an ABSENCE in a second file — FP-006P's shape and FP-011's
// shape both.
//
// ---- THE FALLTHROUGH IS KEPT ON PURPOSE.
//
// A default is defensible: an error arriving from a layer the mapper does not know about should not crash
// the request. **What was missing is a guard that nothing RELIES on it**, so this asserts the reliance is
// zero rather than removing the safety net.
//
// ---- WHY THE MAPPED SIDE IS READ FROM SOURCE AND THE ERROR SIDE BY REFLECTION.
//
// Reflection cannot answer this question alone. `WriteFailure` is a DELIBERATE target in HR — three arms
// in `EmployeeApiErrorMapper` and one in `DepartmentApiErrorMapper` map to it on purpose — so calling
// `Map(error)` and comparing the result cannot distinguish "mapped to WriteFailure" from "fell through":
// both return the same singleton instance. The presence of a `"<code>" =>` arm is the only fact that
// separates them, and it lives in the text.
//
// The error side is reflection over the real `Error` fields rather than a regex over string literals,
// because a literal in a comment or a message is not a code and the field's own `Code` value is.
//
// ---- MAPPING SITES ARE NOT ALL NAMED `*ApiErrorMapper`.
//
// `EmployeeImportExportTransportContracts.cs:167-168` maps two `Employee.Position*` codes. Scoping this by
// filename would have reported both as unmapped — **it did, in the first measurement of T-078, and the
// correction took the finding from eight errors to five.** The sites are therefore listed explicitly.
public sealed class ModuleErrorMappingArchitectureTests
{
  private sealed record ModuleErrorSurface(
    string Module,
    Assembly[] DeclaringAssemblies,
    string[] MappingSites,
    string[] KnownUnmapped);

  private static ModuleErrorSurface[] Modules() =>
  [
    new(
      "Attendance",
      [
        typeof(SSAS.Attendance.Domain.Calendars.WorkingCalendar).Assembly,
        typeof(SSAS.Attendance.Application.Permissions.AttendancePermissionNames).Assembly
      ],
      [Path.Combine("src", "Modules", "Attendance", "SSAS.Attendance.API", "AttendanceApiErrorMapper.cs")],
      []),

    new(
      "GL",
      [
        typeof(SSAS.GL.Domain.Accounts.Account).Assembly,
        typeof(SSAS.GL.Application.Permissions.GlPermissionNames).Assembly
      ],
      [Path.Combine("src", "Modules", "Finance", "SSAS.GL.API", "GlApiErrorMapper.cs")],
      []),

    // ---- TWO ERRORS DECLARED AND NOT MAPPED. FOUND BY THIS GUARD, DELIBERATELY NOT FIXED.
    //
    // A status code is a contract decision and belongs to whoever owns the surface, not to the test that
    // noticed. Both concern overtime tiers on a pay element; both answer `500 request.failed` today.
    new(
      "Payroll",
      [
        typeof(SSAS.Payroll.Domain.Runs.PayrollRun).Assembly,
        typeof(SSAS.Payroll.Application.Permissions.PayrollPermissionNames).Assembly
      ],
      [Path.Combine("src", "Modules", "Payroll", "SSAS.Payroll.API", "PayrollApiErrorMapper.cs")],
      [
        "Payroll.PayElementOvertimeTierInvalid",
        "Payroll.PayElementOvertimeTierNotApplicable"
      ]),

    // ---- THREE MORE, SAME TREATMENT. All three are position-related employee errors.
    new(
      "HR",
      [
        typeof(SSAS.HR.Domain.Employees.Employee).Assembly,
        typeof(SSAS.HR.Application.Permissions.HrPermissionNames).Assembly
      ],
      [
        Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Departments", "DepartmentApiErrorMapper.cs"),
        Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Employees", "EmployeeApiErrorMapper.cs"),
        Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Positions", "PositionApiErrorMapper.cs"),
        Path.Combine("src", "Modules", "HR", "SSAS.HR.API", "Employees", "EmployeeImportExportTransportContracts.cs")
      ],
      [
        "Employee.PositionHistoryImmutable",
        "Employee.PositionInDifferentCompany",
        "Employee.PositionUnchanged"
      ])
  ];

  // ---- THE REGISTER IS AN EXACT SET, NOT A CEILING, AND BOTH DIRECTIONS ARE DELIBERATE.
  //
  // A "no more than N" rule passes when one error is mapped and another is added — the same swap argument
  // that named the anonymous routes in T-077, one file over. So mapping one of these five must also fail:
  // the register records DEBT, and paying it down is a thing someone should have to write down.
  [Theory]
  [MemberData(nameof(ModuleNames))]
  public void Every_error_a_module_declares_is_mapped_rather_than_falling_through(string moduleName)
  {
    var module = Modules().Single(candidate => candidate.Module == moduleName);

    var declared = DeclaredErrorCodes(module);

    // NOT VACUOUS, AND PER MODULE RATHER THAN AGGREGATED. An aggregate is satisfied by the largest module
    // while the smallest goes unexamined — the shape found in T-073, where one plane was a single policy
    // wide. A module whose enumeration comes back empty is a broken sweep, not a clean module.
    Assert.True(
      declared.Length > 0,
      $"No Error fields were found in {moduleName}'s assemblies. The sweep is broken, not the module: an " +
      "empty enumeration would make every assertion below pass over nothing.");

    var mapperSource = string.Concat(module.MappingSites.Select(ReadRepositoryFile));

    var unmapped = declared
      .Where(code => !mapperSource.Contains($"\"{code}\" =>", StringComparison.Ordinal))
      .OrderBy(code => code, StringComparer.Ordinal)
      .ToArray();

    // The message names the consequence, not a remedy. WHICH status a business error deserves is a
    // contract decision owned by the surface, and a test that asserted one would be choosing it.
    Assert.True(
      unmapped.SequenceEqual(module.KnownUnmapped.OrderBy(code => code, StringComparer.Ordinal), StringComparer.Ordinal),
      $"{moduleName}'s declared errors and its mapper have diverged. An unmapped error falls through to " +
      "ApiErrors.WriteFailure and answers 500 request.failed for what is usually a business refusal — no " +
      "exception, no log entry, and a handler that reads correctly. If one of these was just mapped, " +
      $"remove it from KnownUnmapped in the same commit.{Environment.NewLine}" +
      $"expected unmapped: {Format(module.KnownUnmapped)}{Environment.NewLine}" +
      $"actual unmapped:   {Format(unmapped)}");
  }

  // Named rather than derived from `Modules()`, so a module silently dropping out of that list fails here
  // instead of reducing the theory to three cases nobody counts.
  public static TheoryData<string> ModuleNames => new() { "Attendance", "GL", "Payroll", "HR" };

  [Fact]
  public void The_module_surface_list_covers_exactly_the_four_gateable_modules()
  {
    Assert.Equal(
      ["Attendance", "GL", "HR", "Payroll"],
      Modules().Select(module => module.Module).OrderBy(name => name, StringComparer.Ordinal));
  }

  // Reflection over the fields themselves. A `static readonly Error` is how every module declares one, and
  // the field's own `Code` is the value the mapper switches on — no regex over string literals, which
  // would also match a code quoted in a comment or repeated in a message.
  private static string[] DeclaredErrorCodes(ModuleErrorSurface module) =>
  [
    .. module.DeclaringAssemblies
      .SelectMany(assembly => assembly.GetTypes())
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
      .Where(field => field.IsInitOnly && field.FieldType == typeof(Error))
      .Select(field => ((Error)field.GetValue(null)!).Code)
      .Where(code => !string.IsNullOrWhiteSpace(code))
      .Distinct(StringComparer.Ordinal)
      .OrderBy(code => code, StringComparer.Ordinal)
  ];

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
