using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// TS-SS-0003, GENERALISED — A SELF-SERVICE ROUTE MAY BIND FILTERS, NEVER A SUBJECT (T-089).
// ==================================================================================================
//
// ---- WHAT THE RULE USED TO BE, AND WHY IT COULD NOT SURVIVE THE SECOND SELF ROUTE.
//
// T-088 wrote it as `bound.Count == 0`: the self payslip route binds nothing at all, so "nothing bound"
// and "no subject bound" were the same assertion, and the stricter one was cheaper to write.
//
// **They are not the same assertion, and Attendance is where they part.** `/api/attendance/me/records`
// legitimately binds `fromDate` and `toDate`. A date range NARROWS a set the caller is already authorized
// to see; it cannot widen one. Under the old rule the only way to ship a date filter would have been to
// relax the guard for that route — a per-route exception, which is how a guard stops meaning anything.
//
// **The property worth protecting was never "no parameters". It is that the SUBJECT of a self-service read
// is resolved from the caller's identity and can never arrive from the request** (`AC-SS-0007`).
//
// ---- HOW A SUBJECT IS RECOGNISED, STATED HERE RATHER THAN INFERRED PER CALLER.
//
// Two independent tests, because either alone has a hole:
//
//   * BY TYPE — an identifier-shaped bound parameter (`Guid`, `long`, and their nullable forms) is refused
//     outright. This catches `Guid? employeeId` renamed to `Guid? subject`, `Guid? forWhom`, or `Guid? id`.
//   * BY NAME — a bound parameter naming a person is refused whatever its type, which catches
//     `string employeeCode` and `string nationalId`.
//
// A route that genuinely needs a bound identifier is not a self-service route, and admitting one here is a
// decision that should cost a person a conversation rather than a parameter.
//
// ---- AND THE SERVICE / BOUND SPLIT, WHICH IS THE PART A FUTURE READER MAY WANT TO DISPUTE.
//
// A minimal-API handler's parameters mix values BOUND FROM THE REQUEST with SERVICES from the container.
// Only the first is a contract member; an injected `IAttendanceReadService` is not something a caller can
// set. **An unrecognised type is classified as BOUND** — the conservative direction — and both lists are
// printed on failure so the classification can be argued with rather than reverse-engineered.
internal static class SelfServiceContractRule
{
  private static readonly string[] SubjectWords =
    ["employee", "person", "staff", "subject", "user", "worker", "member"];

  // The route pattern is the whole path surface: no path parameter may name a person either.
  public static void AssertNoSubjectOnAnySurface(RouteEndpoint endpoint)
  {
    ArgumentNullException.ThrowIfNull(endpoint);

    Assert.DoesNotContain(SubjectWords, word =>
      endpoint.RoutePattern.RawText!.Contains(word, StringComparison.OrdinalIgnoreCase));

    var handler = endpoint.Metadata.GetMetadata<MethodInfo>();
    Assert.NotNull(handler);

    var bound = new List<string>();
    var injected = new List<string>();

    foreach (var parameter in handler!.GetParameters())
    {
      var type = parameter.ParameterType;

      var isService = type.IsInterface ||
        type == typeof(CancellationToken) ||
        type == typeof(HttpContext);

      (isService ? injected : bound).Add($"{type.Name} {parameter.Name}");
    }

    // NOT VACUOUS. A handler whose parameters could not be read at all would leave both lists empty and
    // every assertion below would pass over nothing.
    Assert.NotEmpty(injected);

    var identifierTypes = new[] { typeof(Guid), typeof(Guid?), typeof(long), typeof(long?) };

    var offending = handler.GetParameters()
      .Where(parameter => !(parameter.ParameterType.IsInterface ||
        parameter.ParameterType == typeof(CancellationToken) ||
        parameter.ParameterType == typeof(HttpContext)))
      .Where(parameter =>
        identifierTypes.Contains(parameter.ParameterType) ||
        SubjectWords.Any(word => parameter.Name!.Contains(word, StringComparison.OrdinalIgnoreCase)))
      .Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}")
      .ToArray();

    Assert.True(
      offending.Length == 0,
      $"A self-service route must not bind the SUBJECT of the read from the request. " +
      $"Offending: [{string.Join(", ", offending)}]. " +
      $"All bound: [{string.Join(", ", bound)}]. " +
      $"Injected (not part of the contract): [{string.Join(", ", injected)}]. " +
      "A bound parameter that merely NARROWS the caller's own data — a date range, a page size — is " +
      "allowed; one shaped like an identifier or named for a person is not.");

    Assert.DoesNotContain(
      injected,
      parameter => SubjectWords.Any(word => parameter.Contains(word, StringComparison.OrdinalIgnoreCase)));
  }
}
