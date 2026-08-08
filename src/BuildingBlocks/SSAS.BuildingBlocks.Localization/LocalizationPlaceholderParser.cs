using System.Text;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Localization;

public static class LocalizationPlaceholderParser
{
  public static Result<PlaceholderSet> Parse(string? text)
  {
    if (text is null)
    {
      return Result.Failure<PlaceholderSet>(LocalizationErrors.InvalidPlaceholder);
    }

    var names = new List<string>();
    for (var index = 0; index < text.Length;)
    {
      if (text[index] == '{')
      {
        if (index + 1 < text.Length && text[index + 1] == '{')
        {
          index += 2;
          continue;
        }

        var close = text.IndexOf('}', index + 1);
        if (close < 0 || text.AsSpan(index + 1, close - index - 1).Contains('{'))
        {
          return Result.Failure<PlaceholderSet>(LocalizationErrors.InvalidPlaceholder);
        }

        var name = PlaceholderName.Create(text[(index + 1)..close]);
        if (name.IsFailure)
        {
          return Result.Failure<PlaceholderSet>(name.Error);
        }

        names.Add(name.Value.Value);
        index = close + 1;
        continue;
      }

      if (text[index] == '}')
      {
        if (index + 1 < text.Length && text[index + 1] == '}')
        {
          index += 2;
          continue;
        }

        return Result.Failure<PlaceholderSet>(LocalizationErrors.InvalidPlaceholder);
      }

      index++;
    }

    return Result.Success(new PlaceholderSet(names));
  }

  public static Result<string> Format(
    string text,
    PlaceholderSet expected,
    IReadOnlyDictionary<string, string> values)
  {
    ArgumentNullException.ThrowIfNull(text);
    ArgumentNullException.ThrowIfNull(expected);
    ArgumentNullException.ThrowIfNull(values);

    var supplied = PlaceholderSet.Create(values.Keys);
    if (supplied.IsFailure || !expected.Matches(supplied.Value) || values.Values.Any(value => value is null))
    {
      return Result.Failure<string>(LocalizationErrors.PlaceholderMismatch);
    }

    var output = new StringBuilder(text.Length);
    for (var index = 0; index < text.Length;)
    {
      if (text[index] == '{')
      {
        if (index + 1 < text.Length && text[index + 1] == '{')
        {
          output.Append('{');
          index += 2;
          continue;
        }

        var close = text.IndexOf('}', index + 1);
        if (close < 0)
        {
          return Result.Failure<string>(LocalizationErrors.InvalidPlaceholder);
        }

        var name = text[(index + 1)..close];
        if (!values.TryGetValue(name, out var value))
        {
          return Result.Failure<string>(LocalizationErrors.PlaceholderMismatch);
        }

        output.Append(value);
        index = close + 1;
        continue;
      }

      if (text[index] == '}' && index + 1 < text.Length && text[index + 1] == '}')
      {
        output.Append('}');
        index += 2;
        continue;
      }

      output.Append(text[index]);
      index++;
    }

    return Result.Success(output.ToString());
  }
}
