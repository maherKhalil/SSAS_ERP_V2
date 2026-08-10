using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SSAS.Platform.API.Transport;

// Neutral strict-request parsing for admin route groups (and future Company API).
// Mirrors the established localization strict-parsing convention: JSON bodies must be an
// object with only the declared members (no unknown/duplicate members, allowed value-kinds,
// required members enforced); query strings reject unknown or multi-valued keys.
// Strict binding remains contract/route-group specific — global JsonSerializerOptions are not changed.
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
