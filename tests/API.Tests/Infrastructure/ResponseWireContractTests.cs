using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// NO RESPONSE TYPE CAN SERIALIZE A TENANT IDENTIFIER (252, class 3).
// ==================================================================================================
//
// Several endpoint tests assert tenant non-disclosure by reading ONE response body and searching it for
// the string `tenantId`. That probe passes when the predicate matches nothing, and worse, IT ONLY EVER
// COVERS THE ONE RESPONSE THE TEST HAPPENED TO MAKE. A leak on any path no test exercised is invisible to
// it — and untested and wrong are correlated, so the unexercised path is exactly where a leak will be.
//
// This asserts the property structurally instead: NO RESPONSE TYPE IN THE PRODUCT CAN PRODUCE THE FIELD AT
// ALL. That holds for every response those types can ever produce, including the ones nobody tested.
//
// ---- ⚠⚠ THE FORBIDDEN NAME IS DERIVED, NEVER TYPED, AND THAT IS THE WHOLE POINT.
//
// Writing `"tenantId"` here would reproduce the very defect this file exists to close: a literal that
// nothing proves can match. Both halves come from the product instead —
//
//   the SYMBOL   `nameof(ITenantOwnedEntity.TenantId)`, so a rename is a compile error, and
//   the CASING   the serializer's OWN naming policy, so it cannot drift from what the wire actually does.
//
// ⚠ The WEB DEFAULTS are the right source because the product does not override it: there is no
// `ConfigureHttpJsonOptions` and no `PropertyNamingPolicy` anywhere in `src/`, which `StrictRequestReader`
// states as a deliberate convention — global JsonSerializerOptions are not changed. If that ever stops
// being true, this guard follows the change rather than contradicting it.
//
// ---- ⚠⚠⚠ WHAT THIS DOES NOT COVER, AND IT IS A REAL GAP RATHER THAN A FORMALITY.
//
// IT ASSERTS THE TYPE CANNOT PRODUCE THE FIELD. IT NEVER ASSERTS THAT A HANDLER DID NOT WRITE IT BY SOME
// OTHER ROUTE — an anonymous object, a hand-built JSON node, a raw string, a problem-details extension.
// Those bypass the response types entirely and this guard is blind to every one of them. The per-response
// probes remain the only cover for that, which is why this SUPPLEMENTS them rather than replacing them.
public sealed class ResponseWireContractTests
{
  // The one identifier that must never reach a caller. `CompanyId` is deliberately NOT here: it is
  // legitimately serialized, and forbidding it would be a false alarm on every company response — and a
  // guard whose false positives outnumber its true ones is one somebody switches off.
  private static string ForbiddenTenantField => NamingPolicy.ConvertName(nameof(ITenantOwnedEntity.TenantId));

  // ⚠ `JsonSerializerOptions.Web` is .NET 9; this targets .NET 8, so the web defaults are constructed
  // rather than read off a static. Same source of truth either way — the SERIALIZER's policy, not mine.
  private static JsonNamingPolicy NamingPolicy =>
    new JsonSerializerOptions(JsonSerializerDefaults.Web).PropertyNamingPolicy ?? JsonNamingPolicy.CamelCase;

  [Fact]
  [Trait("Decision", "DEC-L-079")]
  public void No_response_type_can_serialize_a_tenant_identifier()
  {
    var responses = ResponseTypes()
      .Where(type => !TenantIsTheSubject(type))
      .ToArray();

    // ANTI-VACUITY. A discovery that found nothing would report success over an empty product — the exact
    // failure `TenantModelEntityCountArchitectureTests` records for this same scanning mechanism.
    Assert.True(
      responses.Length >= 40,
      $"only {responses.Length} response types were discovered; the scan has degraded and every assertion " +
      "below would hold trivially rather than because the product is clean");

    var offenders = new List<string>();

    foreach (var response in responses)
    {
      Walk(response, response.Name, new HashSet<Type>(), offenders);
    }

    Assert.True(
      offenders.Count == 0,
      $"these response types can serialize a tenant identifier as '{ForbiddenTenantField}': " +
      $"{string.Join(", ", offenders.OrderBy(name => name, StringComparer.Ordinal))}. A caller would learn " +
      "the tenant id of the records it reads, which is the disclosure every read scope exists to prevent.");
  }

  // ---- THE CONTROL, AND IT IS NOT OPTIONAL HERE.
  //
  // The assertion above is a NEGATIVE one over a walker. If the walker could not see a forbidden field at
  // all, it would report zero offenders forever — the same unfalsifiable shape this file was written to
  // close, one level down. These probes exist so that cannot happen silently.
  private sealed record LeakingProbe(Guid TenantId, string Name);

  private sealed record NestedProbe(string Name, LeakingProbe Inner);

  private sealed record CollectionProbe(string Name, IReadOnlyList<LeakingProbe> Items);

  [Fact]
  [Trait("Decision", "DEC-L-079")]
  public void The_walker_detects_a_tenant_identifier_directly_and_through_a_nested_payload()
  {
    var direct = new List<string>();
    Walk(typeof(LeakingProbe), nameof(LeakingProbe), new HashSet<Type>(), direct);
    Assert.NotEmpty(direct);

    // ⚠ NESTED, because a walker that only inspected top-level properties would satisfy the assertion
    // above and still miss every real leak: responses carry their payload in nested records.
    var nested = new List<string>();
    Walk(typeof(NestedProbe), nameof(NestedProbe), new HashSet<Type>(), nested);
    Assert.NotEmpty(nested);

    // ⚠⚠ AND THROUGH A COLLECTION, which is how every page response carries its rows. A walker that
    // stopped at `IReadOnlyList<T>` would be blind to precisely the shape `CompanyPageResponse` uses.
    var collection = new List<string>();
    Walk(typeof(CollectionProbe), nameof(CollectionProbe), new HashSet<Type>(), collection);
    Assert.NotEmpty(collection);
  }

  // ---- ⚠⚠ THE ONE EXEMPTION, AND IT ASSERTS ITS OWN GROUNDS BELOW RATHER THAN BEING A HOLE.
  //
  // Authentication responses NAME THE TENANT ON PURPOSE: `AuthenticatedResponse` says which tenant you are
  // now in, and `TenantSelectionRequiredResponse` lists the ones you may choose between. THE TENANT IS THE
  // SUBJECT OF THOSE ANSWERS, not an attribute leaking out of a record you asked about. Forbidding it there
  // would be a false alarm on the one place the field is the entire point — and a guard whose false
  // positives outnumber its true ones is one somebody switches off.
  //
  // ⚠ The exemption is a NAMESPACE, not a list of names, so a new authentication response is covered
  // without an edit and a business response cannot be quietly moved into the exemption by renaming it.
  private const string AuthenticationSurface = "SSAS.Platform.API.Authentication";

  private static bool TenantIsTheSubject(Type type) =>
    string.Equals(type.Namespace, AuthenticationSurface, StringComparison.Ordinal);

  [Fact]
  [Trait("Decision", "DEC-L-079")]
  public void The_authentication_exemption_is_load_bearing_and_does_not_swallow_the_population()
  {
    var all = ResponseTypes();
    var exempted = all.Where(TenantIsTheSubject).ToArray();

    // ⚠ LOAD-BEARING. If nothing in the exempted namespace actually carries the field, this exemption is
    // stale decoration and should be deleted — and leaving it would mean a real leak could later appear
    // there unchallenged.
    var carrying = exempted
      .Where(type =>
      {
        var found = new List<string>();
        Walk(type, type.Name, new HashSet<Type>(), found);
        return found.Count > 0;
      })
      .ToArray();

    Assert.True(
      carrying.Length >= 1,
      "no exempted authentication response carries a tenant identifier at all, so this exemption is " +
      "protecting nothing and should be removed rather than left to cover a future leak");

    // ⚠⚠ AND NARROW. An exemption that grew to cover most of the surface would silently retire the guard
    // while leaving it green — the shape where a rule survives as a passing test that governs nothing.
    Assert.True(
      exempted.Length * 4 < all.Length,
      $"the authentication exemption covers {exempted.Length} of {all.Length} response types, which is no " +
      "longer a narrow exception — the guard is being retired by growth rather than by decision");
  }

  private static void Walk(Type type, string path, HashSet<Type> seen, List<string> offenders)
  {
    if (!seen.Add(type))
    {
      return;
    }

    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      var wireName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? NamingPolicy.ConvertName(property.Name);

      if (string.Equals(wireName, ForbiddenTenantField, StringComparison.OrdinalIgnoreCase))
      {
        offenders.Add($"{path}.{property.Name}");
      }

      foreach (var next in Payloads(property.PropertyType))
      {
        Walk(next, $"{path}.{property.Name}", seen, offenders);
      }
    }
  }

  // The types a property can carry a payload in: itself, or the element of a collection.
  private static IEnumerable<Type> Payloads(Type type)
  {
    var candidate = Nullable.GetUnderlyingType(type) ?? type;

    if (candidate.IsArray && candidate.GetElementType() is { } element)
    {
      candidate = element;
    }
    else if (candidate.IsGenericType && typeof(IEnumerable).IsAssignableFrom(candidate))
    {
      candidate = candidate.GetGenericArguments()[0];
    }

    if (candidate.IsPrimitive || candidate.IsEnum || candidate == typeof(string) ||
        candidate.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
    {
      yield break;
    }

    yield return candidate;
  }

  // Population by MECHANISM: every public response type the API assemblies declare. ⚠ It deliberately does
  // NOT read "response types that carry no tenant id", which would exclude by construction exactly the
  // defect being asserted — the self-selecting shape caught in 248.
  private static Type[] ResponseTypes() => Directory
    .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.API.dll")
    .Select(LoadOrNull)
    .Where(assembly => assembly is not null)
    .SelectMany(assembly => SafeTypes(assembly!))
    .Where(type => type.IsPublic && !type.IsAbstract && !type.IsInterface &&
      type.Name.EndsWith("Response", StringComparison.Ordinal))
    .OrderBy(type => type.FullName, StringComparer.Ordinal)
    .ToArray();

  private static Assembly? LoadOrNull(string path)
  {
    try
    {
      return Assembly.LoadFrom(path);
    }
    catch (BadImageFormatException)
    {
      return null;
    }
    catch (FileLoadException)
    {
      return null;
    }
  }

  private static IEnumerable<Type> SafeTypes(Assembly assembly)
  {
    try
    {
      return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException loaded)
    {
      return loaded.Types.Where(type => type is not null)!;
    }
  }
}
