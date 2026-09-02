using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SSAS.BuildingBlocks.Api.Transport;

// Neutral strict-request parsing. The convention: JSON bodies must be an
// object with only the declared members (no unknown/duplicate members, allowed value-kinds,
// required members enforced); query strings reject unknown or multi-valued keys.
// Strict binding remains contract/route-group specific — global JsonSerializerOptions are not changed.
// Localization converted its private, token-identical copy under T-127.
//
// ⚠ THIS IS NOT UNIVERSAL, AND AN EARLIER VERSION OF THIS HEADER SAID IT WAS. It claimed every route
// group in the product binds through this reader and that Localization was "the last holdout". Both
// were false. As at 2026-09-02 two private body readers remain, and neither is named "Strict" anything:
//   AuthenticationEndpointRouteBuilderExtensions.ReadJsonAsync<T>
//   PlatformSupportAuthenticationEndpointRouteBuilderExtensions.ReadLoginAsync
// Enumerated by mechanism — JsonDocument.ParseAsync over Request.Body — because a name search cannot
// find them. StrictRequestBindingArchitectureTests excludes their contracts from its closure on purpose.
//
// ⚠⚠ DO NOT CONVERT THEM TO THIS READER. Both construct their options as new(JsonSerializerDefaults.Web),
// which matches property names case-INSENSITIVELY; this reader uses JsonSerializerOptions.Default, which
// is case-SENSITIVE. Neither login contract carries a single [JsonPropertyName], so their camelCase wire
// bodies would stop binding to their PascalCase members on conversion — every field null on the login
// routes. That makes the architecture guard's exclusion silently load-bearing on an option nothing
// asserts. The deleted sentence was not merely inaccurate: it was an instruction to perform that
// conversion, on the file a converter reads first.
public static class StrictRequestReader
{
  public static async Task<T?> ReadStrictJsonAsync<T>(
    HttpContext context,
    IReadOnlyDictionary<string, JsonValueKind[]> fields,
    CancellationToken cancellationToken,
    IReadOnlyCollection<string>? requiredFields = null,
    Func<JsonElement, bool>? additionalValidation = null) where T : class
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(fields);
    if (!context.Request.HasJsonContentType())
    {
      return null;
    }

    try
    {
      using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
      if (document.RootElement.ValueKind != JsonValueKind.Object)
      {
        return null;
      }

      var seen = new HashSet<string>(StringComparer.Ordinal);
      foreach (var property in document.RootElement.EnumerateObject())
      {
        if (!fields.TryGetValue(property.Name, out var kinds) || !seen.Add(property.Name) || !kinds.Contains(property.Value.ValueKind))
        {
          return null;
        }
      }

      var required = requiredFields ?? fields.Keys;
      if (required.Any(propertyName => !seen.Contains(propertyName)) ||
        additionalValidation is not null && !additionalValidation(document.RootElement))
      {
        return null;
      }

      return document.RootElement.Deserialize<T>();
    }
    catch (JsonException)
    {
      return null;
    }
    catch (BadHttpRequestException)
    {
      return null;
    }
  }

  // Every supplied query key must be expected and single-valued.
  public static bool HasOnly(IQueryCollection values, IReadOnlyCollection<string> names) =>
    values.All(pair => names.Contains(pair.Key, StringComparer.Ordinal) && pair.Value.Count == 1);

  public static bool TryRequired(IQueryCollection values, string name, out string value)
  {
    value = string.Empty;
    return values.TryGetValue(name, out var source) && source.Count == 1 && !string.IsNullOrWhiteSpace(value = source[0]!);
  }

  public static bool TryOptional(IQueryCollection values, string name, out string? value)
  {
    value = null;
    return !values.TryGetValue(name, out var source) ||
      (source.Count == 1 && !string.IsNullOrWhiteSpace(value = source[0]!));
  }

  public static bool TryInt(IQueryCollection values, string name, int defaultValue, out int value)
  {
    value = defaultValue;
    return !values.TryGetValue(name, out var source) ||
      (source.Count == 1 && int.TryParse(source[0], NumberStyles.None, CultureInfo.InvariantCulture, out value));
  }

  public static bool TryBool(IQueryCollection values, string name, out bool value)
  {
    value = false;
    if (!values.TryGetValue(name, out var source))
    {
      return true;
    }

    return source.Count == 1 && (source[0] == "true" ? (value = true) : source[0] == "false");
  }

  public static bool IsOneOf(string? value, IReadOnlyCollection<string> allowed) =>
    value is null || allowed.Contains(value, StringComparer.Ordinal);
}
