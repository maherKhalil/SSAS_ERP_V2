namespace SSAS.HR.Infrastructure.Persistence;

// TURNING A USER'S SEARCH TEXT INTO A `LIKE` PATTERN, SAFELY (DEC-POS-0030).
//
// ================================================================================================
// WILDCARDS IN USER INPUT ARE LITERAL CHARACTERS, NOT OPERATORS.
// ================================================================================================
//
// Someone searching for a department called "50% Owned" means the character `%`. Pasted into a `LIKE`
// pattern unescaped it becomes "match anything", and the search silently returns the entire scope — a
// result that looks like a working search returning a lot of rows rather than like a bug. `_` matches any
// single character and `[` opens a character class, with the same consequence at smaller scale.
//
// So every character SQL Server treats as special is escaped, and the pattern is issued with an explicit
// `ESCAPE` clause. This is not injection defence — the pattern travels as a parameter and always did — it is
// about the search meaning what the user typed.
//
// ---- THE ESCAPE CHARACTER IS ESCAPED FIRST, AND THE ORDER IS LOAD-BEARING.
//
// Replacing `%` before `\` would turn a typed backslash into `\` + an already-escaped `%`, doubling the
// escape and matching nothing. Backslash goes first, always.
//
// ---- `]` IS NOT ESCAPED, AND THAT IS CORRECT RATHER THAN AN OVERSIGHT.
//
// `]` is only special INSIDE a character class, and escaping `[` means no class can ever open. A lone `]`
// in a pattern is a literal to SQL Server.
internal static class SearchPattern
{
  // Backslash: conventional, and not a character that appears in the identifiers and labels this searches.
  internal const char EscapeCharacter = '\\';

  // A CONTAINS pattern: `%text%`. The leading wildcard is why the search column carries no index.
  internal static string Contains(string searchText) =>
    $"%{Escape(searchText)}%";

  // A PREFIX pattern: `text%`. Used where the existing behaviour was a prefix match and stays one — a code
  // search, where a leading wildcard would make every code in the scope a candidate.
  internal static string StartsWith(string searchText) =>
    $"{Escape(searchText)}%";

  // The caller's text as the domain would have normalized it, so it compares equal to the stored column
  // under the binary collation. Normalizing here and on write is what makes the search case-insensitive
  // WITHOUT a case-insensitive collation, which the ordinal columns deliberately do not have.
  internal static string Normalize(string searchText) =>
    searchText.Trim().ToUpperInvariant();

  private static string Escape(string searchText) =>
    Normalize(searchText)
      .Replace("\\", "\\\\", StringComparison.Ordinal)
      .Replace("%", "\\%", StringComparison.Ordinal)
      .Replace("_", "\\_", StringComparison.Ordinal)
      .Replace("[", "\\[", StringComparison.Ordinal);
}
