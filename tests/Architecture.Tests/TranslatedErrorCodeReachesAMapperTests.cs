using System.Reflection;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY ERROR A HANDLER DELIBERATELY TRANSLATES INTO REACHES SOME MAPPER ARM (T-252).
// ==================================================================================================
//
// ---- WHY THE EXISTING GUARD CANNOT ASK THIS.
//
// `ModuleErrorMappingArchitectureTests` derives each site's responsibilities FROM THAT SITE'S HANDLER
// TYPES, so it answers *"does this mapper handle everything its module produces?"*. It cannot answer
// *"is there a translated error code belonging to no mapping site at all?"* — **it iterates sites, and a
// code no site claims is invisible to it.**
//
// `Tenant.CodeExists` is exactly that: `CreateTenantCommandHandler` translates a unique violation into it
// on purpose, and no mapper anywhere has an arm, so it would fall to the default and answer **500**.
//
// ---- ⚠ THIS ASKS THE MAPPERS WHAT THEY DO, RATHER THAN WHAT THEIR SOURCE LOOKS LIKE.
//
// A first attempt at this question scanned mapper sources for `ApiError <name> = new(<status>,` and
// reported `localization.override_already_exists` as unmapped. **It is mapped** —
// `LocalizationApiErrorMapper` simply declares its own `LocalizationApiError` type instead of the shared
// one, so a scan keyed on the shared type is blind to that entire mapper.
//
// So this INVOKES each mapper and reads the answer. A mapper that declares its own result type is handled
// for free, because the question asked is "what status comes back", not "what does the declaration look
// like".
//
// The discriminator is a comparison rather than a magic number: an obviously unknown code is mapped first,
// and a real code is "reached" when some mapper answers it DIFFERENTLY from that. **Hard-coding 500 would
// break silently the day the default changed.**
public sealed class TranslatedErrorCodeReachesAMapperTests
{
  // Every code a handler translates a unique-constraint violation into. Fourteen, enumerated in T-249 by
  // walking the handlers; listed rather than rediscovered because the list is the subject of the test.
  private static readonly string[] TranslatedCodes =
  [
    "Attendance.WorkingCalendarNameConflict",
    "Attendance.LeaveBalanceConflict",
    "Attendance.LeaveTypeCodeConflict",
    "Gl.AccountCodeConflict",
    "Gl.FiscalYearCodeConflict",
    "Gl.JournalNumberConflict",
    "Payroll.PayElementCodeConflict",
    "Payroll.PeriodConflict",
    "Payroll.RunConflict",
    "Company.CodeConflict",
    "localization.override_already_exists",
    "PlatformSupport.PermissionAlreadyAssigned",
    "PlatformSupport.PrincipalAlreadyExists",
    "Tenant.CodeExists"
  ];

  // ⚠ EXEMPT WHILE — AND ONLY WHILE — NOTHING CAN REACH ITS HANDLER.
  //
  // `Tenant.CodeExists` has no arm because there is no tenant administration surface: no endpoint reaches
  // `CreateTenantCommandHandler`. Adding the arm to an arbitrary mapper would be a guess dressed as a
  // decision, since no mapper serves a route that does not exist.
  //
  // **The exemption is tied to the fact that justifies it** by the second test below, which reddens the
  // moment an endpoint reaches that handler. `OWNER-DECISIONS.md` entry 2 covers tenant administration, so
  // this is scheduled work rather than a hypothetical surface — and on the day it lands, the first
  // duplicate tenant code would otherwise ship a 500.
  private const string UnreachableUntilTenantAdminExists = "Tenant.CodeExists";

  [Fact]
  public void Every_translated_error_code_is_answered_by_some_mapper()
  {
    var mappers = MapperMethods();

    // Anti-vacuity, and it asserts WHICH mappers exist rather than assuming the shape of any of them.
    // Two of the ten declare a `Map` this test cannot invoke (extra parameters, or a per-site overload);
    // that is recorded as a floor rather than silently reducing the population.
    Assert.True(mappers.Count >= 8,
      $"only {mappers.Count} invokable mapper methods were discovered; the search has degraded and every " +
      "code below would look unmapped for the wrong reason.");

    var unreachable = new List<string>();

    foreach (var code in TranslatedCodes)
    {
      if (code == UnreachableUntilTenantAdminExists)
      {
        continue;
      }

      if (!IsAnsweredBySomeMapper(mappers, code))
      {
        unreachable.Add(code);
      }
    }

    Assert.True(unreachable.Count == 0,
      "a handler deliberately translates a persistence failure into this code, and NO mapper answers it — " +
      "so the translation is undone at the boundary and the caller gets the default:\n  " +
      string.Join("\n  ", unreachable));
  }

  // The exemption's falsifier. It does not check that the arm is missing; it checks the REASON is still
  // true, which is the only thing that makes the omission defensible.
  [Fact]
  public void The_exempt_code_is_still_unreachable_because_no_endpoint_serves_it()
  {
    var reachable = ApiAssemblies()
      .SelectMany(assembly => assembly.GetTypes())
      .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
      .SelectMany(method => method.GetParameters())
      .Any(parameter => parameter.ParameterType.Name == "CreateTenantCommandHandler");

    Assert.False(reachable,
      $"an API surface now reaches CreateTenantCommandHandler, so `{UnreachableUntilTenantAdminExists}` " +
      "can be returned over the wire — where it has no mapper arm and answers 500 for a duplicate tenant " +
      "code. Add the arm to whichever mapper serves that route and remove this exemption.");
  }

  // ⚠ "ANSWERED" IS DECIDED BY COMPARISON, NOT BY A CONSTANT. An unknown code establishes what the default
  // looks like on each mapper; a real code counts as reached when some mapper answers it differently.
  private static bool IsAnsweredBySomeMapper(IReadOnlyCollection<MethodInfo> mappers, string code)
  {
    var unknown = new Error("Nothing.MapsThis", "A code no mapper can know.");

    foreach (var mapper in mappers)
    {
      var parameters = mapper.GetParameters();

      // `TryMap(string, out T)`: the bool IS the answer, so no comparison is needed.
      if (parameters.Length == 2)
      {
        var args = new object?[] { code, null };
        if (mapper.Invoke(null, args) is true)
        {
          return true;
        }

        continue;
      }

      var fallback = Describe(mapper.Invoke(null, [unknown]));
      var actual = Describe(mapper.Invoke(null, [new Error(code, "probe")]));

      if (actual is not null && actual != fallback)
      {
        return true;
      }
    }

    return false;
  }

  // Reads the result WITHOUT naming its type: `LocalizationApiErrorMapper` returns its own, and a guard
  // keyed on the shared type would be blind to it — which is the defect this test was written after.
  private static string? Describe(object? result)
  {
    if (result is null)
    {
      return null;
    }

    var type = result.GetType();
    var status = type.GetProperty("StatusCode")?.GetValue(result);
    var code = type.GetProperty("Code")?.GetValue(result);
    return $"{status}|{code}";
  }

  // ⚠ THE MAPPERS ARE HETEROGENEOUS, AND ASSUMING ONE SHAPE IS HOW THIS TEST FAILED TWICE.
  //
  // Three entry points exist today:
  //   * `Map(Error)`                              -- most mappers
  //   * `MapPosition/MapJobGrade/MapSalaryGrade`  -- Position, one arm per route group
  //   * `TryMap(string, out LocalizationApiError)` -- Localization, a different contract entirely
  //
  // A first version matched only the shared `ApiError` TYPE and missed Localization. A second matched only
  // the `Map(Error)` SIGNATURE and missed it again. **Both times the same mapper, for a different reason,
  // and both times the test went red pointing at a code that was mapped all along.**
  private static List<MethodInfo> MapperMethods() =>
    [.. MapperTypes()
      .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
      .Where(IsRecognisedEntryPoint)];

  private static bool IsRecognisedEntryPoint(MethodInfo method)
  {
    if (!method.Name.StartsWith("Map", StringComparison.Ordinal)
      && !method.Name.StartsWith("TryMap", StringComparison.Ordinal))
    {
      return false;
    }

    var parameters = method.GetParameters();
    return (parameters.Length == 1 && parameters[0].ParameterType == typeof(Error))
      || (parameters.Length == 2 && parameters[0].ParameterType == typeof(string)
        && parameters[1].IsOut);
  }

  private static List<Type> MapperTypes() =>
    [.. ApiAssemblies()
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.Name.EndsWith("ApiErrorMapper", StringComparison.Ordinal))];

  // ⚠ THE CONTROL AGAINST A FOURTH SHAPE APPEARING AND BEING SKIPPED IN SILENCE.
  //
  // Everything above depends on recognising how a mapper is called. A mapper written to a NEW shape would
  // simply not be invoked, every code it owns would look unmapped, and the failure would point at the
  // codes rather than at the omission. So each mapper must expose at least one entry point this test can
  // actually call.
  [Fact]
  public void Every_mapper_exposes_an_entry_point_this_test_can_invoke()
  {
    var types = MapperTypes();

    Assert.True(types.Count >= 9,
      $"only {types.Count} mapper types were discovered; the assembly walk has degraded.");

    var uncallable = types
      .Where(type => !type.GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Any(IsRecognisedEntryPoint))
      .Select(type => type.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(uncallable.Length == 0,
      "a mapper exposes no entry point this test recognises, so every code it owns would be reported as " +
      "unmapped and the real defect would be this omission rather than the codes:\n  " +
      string.Join("\n  ", uncallable) +
      "\n\nTeach `IsRecognisedEntryPoint` the new shape.");
  }

  private static Assembly[] ApiAssemblies() =>
    [.. Directory
      .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.API.dll")
      .Select(RepositoryPaths.ProjectName)
      .Distinct(StringComparer.Ordinal)
      .Select(Assembly.Load)];
}
