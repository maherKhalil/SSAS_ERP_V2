using System.Text;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public static class LocalizationPlaceholderFormatter
{
  public static Result<string> Format(
    string template,
    PlaceholderSet placeholders,
    IReadOnlyDictionary<string, string>? values)
  {
    ArgumentNullException.ThrowIfNull(template);
    ArgumentNullException.ThrowIfNull(placeholders);
    values ??= new Dictionary<string, string>(StringComparer.Ordinal);
    if (values.Count != placeholders.Names.Count ||
      values.Keys.Any(key => !placeholders.Names.Any(name => string.Equals(name, key, StringComparison.Ordinal))) ||
      placeholders.Names.Any(name => !values.ContainsKey(name)))
    {
      return Result.Failure<string>(LocalizationErrors.PlaceholderMismatch);
    }

    var output = new StringBuilder(template.Length);
    for (var index = 0; index < template.Length; index++)
    {
      if (template[index] == '{')
      {
        if (index + 1 < template.Length && template[index + 1] == '{')
        {
          output.Append('{');
          index++;
          continue;
        }

        var closing = template.IndexOf('}', index + 1);
        if (closing < 0)
        {
          return Result.Failure<string>(LocalizationErrors.InvalidPlaceholder);
        }

        var name = template[(index + 1)..closing];
        if (!values.TryGetValue(name, out var value))
        {
          return Result.Failure<string>(LocalizationErrors.PlaceholderMismatch);
        }

        output.Append(value);
        index = closing;
        continue;
      }

      if (template[index] == '}' && index + 1 < template.Length && template[index + 1] == '}')
      {
        output.Append('}');
        index++;
        continue;
      }

      output.Append(template[index]);
    }

    return Result.Success(output.ToString());
  }
}
