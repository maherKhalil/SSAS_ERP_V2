using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// EVERY TYPE THE STRICT READER BINDS CARRIES ITS WIRE NAMES (276). TIER 1: TWO RECORDED OUTAGES.
// ==================================================================================================
//
// `StrictRequestReader.ReadStrictJsonAsync` deserializes with `JsonSerializerOptions.Default`, which is
// **case-sensitive** and reads enums **from numbers only**. This has shipped as a total, silent defect
// TWICE, in two different features:
//
//   * **FP-011 (GL)** omitted `[property: JsonPropertyName]`. `{"code":"4100"}` never bound to `Code`, the
//     reader returned null, and EVERY GL write route answered `400 request.invalid` while routes, handlers,
//     domain and mapper were all correct.
//   * **FP-012 (Payroll)** omitted `[property: JsonConverter(typeof(JsonStringEnumConverter))]`. `"Earning"`
//     could not become a `PayElementKind`, so no pay element could be created and therefore no payroll
//     could ever be calculated.
//
// ---- ⚠ WHY THIS EXISTS WHEN `AttendanceTransportContractTests` ALREADY DOES IT.
//
// That guard is correct and covers ONE assembly — its own comment says so: *every request record that will
// ever exist IN THIS MODULE*. **Attendance suffered NEITHER outage.** GL, which suffered the first, has no
// structural guard at all; Payroll, which suffered the second, has a behavioural one that checks the routes
// somebody remembered to enumerate. **THE PROTECTION WAS WHERE THE DAMAGE WASN'T.**
//
// Copying Attendance's shape per assembly was rejected: its ASSEMBLY-plus-NAME filter is load-bearing on an
// assumption true of Attendance and false elsewhere — that every `*Request` type there goes through the
// strict reader. Four more copies would carry that assumption into four assemblies where it does not hold.
//
// ---- ⚠⚠ THE POPULATION IS THE CLOSURE, NOT THE CALL SITES, AND THAT IS THE WHOLE DESIGN.
//
// `CreateJournalDraftRequest` contains `JournalLineRequest[]`; `DefineFiscalYearRequest` contains
// `FiscalPeriodRequest[]`. **Neither nested type appears at any call site**, both bind through the same
// options, and both need the same attributes. **A call-site-derived population would look complete and miss
// the shape most likely to fail undetected** — the outer type binds, the nested array comes back empty or
// wrong, and every route answers plausibly.
//
// ⚠ AND THE CANDIDATE POPULATIONS DIFFER IN BOTH DIRECTIONS, so neither is a proxy for the other: there are
// ~66 types named `*Request` and 59 call-site arguments, and the gap is nested types MISSING from the call
// sites in one direction and Authentication's contracts that must be EXCLUDED in the other.
//
// ---- WHAT IS EXCLUDED, BY CONSTRUCTION RATHER THAN BY A FILTER.
//
// `AuthenticationLoginRequest` and `AuthenticationTenantSelectionRequest` carry NO `JsonPropertyName` and
// that is CORRECT: the Authentication surface does not use this reader. It has its own `ReadJsonAsync<T>`
// with its own allowlist and `JsonSerializerDefaults.Web`, which is camelCase and CASE-INSENSITIVE, so
// unattributed properties bind correctly there. **An attribute added to those types would be inert today
// and load-bearing the moment somebody changed that reader.**
//
// They are excluded because they are not reachable from a `ReadStrictJsonAsync` call site — not by a name
// filter. A name-based population would have flagged them on day one, and the "fix" a reader would apply is
// invisible and permanent.
//
// ---- ⚠⚠⚠ A REJECTED ALTERNATIVE, RECORDED BECAUSE IT WILL BE PROPOSED AGAIN.
//
// **Constrain the reader to `where T : IStrictJsonRequest` and let the compiler enforce membership.** That
// is the strongest population guarantee available and cannot drift. It was rejected here for two reasons:
// it is a 59-type change to PRODUCTION contracts to fix a TEST's population predicate; and **it does not
// reach the nested half** — nothing would force `JournalLineRequest` to implement the marker, so the
// closure walk below is needed either way. It buys drift-resistance on the ROOTS ONLY, and the roots are
// already protected by the count cross-check in `Every_strict_reader_call_site_is_parsed`.
public sealed class StrictRequestBindingArchitectureTests
{
  // The type argument, on the same line as the method name. Measured: all 59 call sites are written this
  // way today. The cross-check below is what makes that measurement safe to depend on.
  private static readonly Regex TypedCall = new(
    @"ReadStrictJsonAsync<(\w+)>", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private static readonly Regex AnyCall = new(
    @"ReadStrictJsonAsync\s*<", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  // ---- ⚠ THE DERIVATION'S OWN ANTI-VACUITY LEG, AND IT IS THE ONE THAT MAKES A SOURCE SCAN SAFE.
  //
  // A source scan reads what the source SAYS. A `using` alias, a fully-qualified type argument or a line
  // break produces a call site the loose pattern sees and the extracting pattern does not — and the guard
  // below would then hold over a SMALLER population while looking identical.
  //
  // **Counting what the mechanism emitted against what was parsed converts that from silent to loud.**
  [Fact]
  public void Every_strict_reader_call_site_is_parsed()
  {
    var sources = EndpointSources();

    Assert.NotEmpty(sources);

    var loose = sources.Sum(source => AnyCall.Matches(source).Count);
    var typed = sources.Sum(source => TypedCall.Matches(source).Count);

    Assert.True(
      loose == typed,
      $"{loose} `ReadStrictJsonAsync<` call sites exist and only {typed} yielded a type argument. The " +
      "unparsed ones are invisible to every assertion in this file, which would then hold over a subset " +
      "while reading as complete.");
  }

  [Fact]
  [Trait("Decision", "DEC-L-080")]
  public void Every_bound_request_property_carries_an_explicit_json_property_name()
  {
    var missing = BoundTypes()
      .SelectMany(type => Properties(type)
        .Where(property => property.GetCustomAttribute<JsonPropertyNameAttribute>() is null)
        .Select(property => $"{type.Name}.{property.Name}"))
      .OrderBy(entry => entry, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      missing.Length == 0,
      "These properties bind through StrictRequestReader and carry no [property: JsonPropertyName], so a " +
      "correctly-cased body will not bind and the route will answer 400 request.invalid — the FP-011 " +
      $"defect:\n  {string.Join("\n  ", missing)}");
  }

  [Fact]
  [Trait("Decision", "DEC-L-080")]
  public void Every_bound_enum_property_reads_from_a_string()
  {
    var missing = BoundTypes()
      .SelectMany(type => Properties(type)
        .Where(property => IsEnum(property.PropertyType))
        .Where(property =>
          property.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType
            != typeof(JsonStringEnumConverter))
        .Select(property => $"{type.Name}.{property.Name}"))
      .OrderBy(entry => entry, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      missing.Length == 0,
      "These enum-valued properties bind through StrictRequestReader without " +
      "[property: JsonConverter(typeof(JsonStringEnumConverter))]. JsonSerializerOptions.Default reads " +
      $"enums from NUMBERS ONLY, so the whole record fails to bind — the FP-012 defect:\n  " +
      string.Join("\n  ", missing));
  }

  // ---- ⚠⚠ THE CLOSURE IS PROVEN TO HAVE WALKED, TWO WAYS, BECAUSE IT HAS TWO WAYS TO COLLAPSE.
  //
  // A walk that stopped at depth one would return exactly the roots and every assertion above would still
  // pass — over the population that is already the best covered. **The count comparison catches a walk that
  // never descended; the named type catches one that descended into noise instead.** Neither alone does.
  [Fact]
  public void The_closure_reaches_types_that_appear_at_no_call_site()
  {
    var roots = RootTypes();
    var closure = BoundTypes();

    Assert.NotEmpty(roots);

    Assert.True(
      closure.Length > roots.Length,
      $"the closure returned {closure.Length} types from {roots.Length} roots, so it descended into " +
      "nothing. Nested request records — the shape whose defect is silent — would be unguarded.");

    // `JournalLineRequest` is nested inside `CreateJournalDraftRequest` and appears at NO call site. It is
    // named because it is the concrete case this guard exists to reach, and because a rename should break
    // this loudly rather than quietly shrink the population.
    Assert.Contains(closure, type => type.Name == "JournalLineRequest");
  }

  // The roots: every type argument at a `ReadStrictJsonAsync` call site, resolved to a real Type.
  private static Type[] RootTypes() =>
    EndpointSources()
      .SelectMany(source => TypedCall.Matches(source).Select(match => match.Groups[1].Value))
      .Distinct(StringComparer.Ordinal)
      .Where(name => name != "T")
      .Select(Resolve)
      .OrderBy(type => type.Name, StringComparer.Ordinal)
      .ToArray();

  // The population: the roots plus every SSAS-declared type reachable through their properties.
  private static Type[] BoundTypes()
  {
    var seen = new HashSet<Type>();
    var frontier = new Queue<Type>(RootTypes());

    while (frontier.Count > 0)
    {
      var type = frontier.Dequeue();
      if (!seen.Add(type))
      {
        continue;
      }

      foreach (var property in Properties(type))
      {
        var candidate = Unwrap(property.PropertyType);

        if (IsOwnDeclared(candidate) && !candidate.IsEnum && !seen.Contains(candidate))
        {
          frontier.Enqueue(candidate);
        }
      }
    }

    return [.. seen.OrderBy(type => type.Name, StringComparer.Ordinal)];
  }

  private static PropertyInfo[] Properties(Type type) =>
    type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

  // ⚠ THROWS RATHER THAN SKIPPING. A name that resolves to nothing would silently leave a whole root — and
  // its entire subtree — out of the population, and every assertion here is an emptiness over that
  // population. A guard on the input is not a guard on the output; this is the input guard, and the
  // count cross-check above is the output one.
  private static Type Resolve(string name)
  {
    var matches = ApiAssemblies()
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.Name == name)
      .Distinct()
      .ToArray();

    return matches.Length == 1
      ? matches[0]
      : throw new InvalidOperationException(
        $"the type argument `{name}` resolved to {matches.Length} types across the API assemblies. Every " +
        "assertion in this file is an emptiness over the population this name enters, so an unresolved " +
        "root removes itself and its whole subtree from the guard.");
  }

  // Arrays, collections and Nullable<> all wrap the type that actually binds.
  private static Type Unwrap(Type type)
  {
    if (type.IsArray)
    {
      return Unwrap(type.GetElementType()!);
    }

    if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
    {
      return Unwrap(type.GetGenericArguments()[0]);
    }

    return Nullable.GetUnderlyingType(type) ?? type;
  }

  private static bool IsEnum(Type type) => Unwrap(type).IsEnum;

  private static bool IsOwnDeclared(Type type) =>
    type.Assembly.GetName().Name?.StartsWith("SSAS.", StringComparison.Ordinal) == true;

  private static Assembly[] ApiAssemblies() =>
  [
    Assembly.Load("SSAS.GL.API"),
    Assembly.Load("SSAS.Payroll.API"),
    Assembly.Load("SSAS.Attendance.API"),
    Assembly.Load("SSAS.HR.API"),
    Assembly.Load("SSAS.Platform.API")
  ];

  private static string[] EndpointSources() =>
    Directory
      .EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
      .Where(path =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Select(File.ReadAllText)
      .Where(source => source.Contains("ReadStrictJsonAsync", StringComparison.Ordinal))
      .ToArray();

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
