namespace SSAS.Architecture.Tests;

// ==================================================================================================
// NO `catch` IN `src/` HAS AN EMPTY BODY (T-242).
// ==================================================================================================
//
// ---- ⚠ THIS IS NOT THE GUARD THAT WAS ASKED FOR, BECAUSE THAT ONE WAS VACUOUS.
//
// The proposal was to assert zero **bare** `catch` with an empty body — `catch { }` — in `src/`, on the
// expectation that the count would fall to zero once the swallowed-exception pass finished. **It was
// already zero, and `git log -S` across every commit says it has never been anything else.** A guard over a
// pattern the codebase has never once written asserts nothing about the codebase; it only asserts that the
// scan still runs, and the floors below do that more honestly.
//
// So this asserts the property that is genuinely true and genuinely at risk: **no catch in `src/` has an
// empty body at all**, whatever exception it declares. That was 0 of 101 when it was written.
//
// ---- WHY THAT VERSION IS A REAL RULE RATHER THAN A RESTATEMENT.
//
// **`tests/` has 21 empty catch bodies.** The pattern is one this repository writes, fluently, in teardown
// and process-cleanup paths where it is correct — so a rule forbidding it in `src/` forbids something a
// contributor here plausibly would do. A rule nobody could break is not enforcement.
//
// ---- ⚠ THE RULE IS "SAID NOTHING AT ALL", NOT "HAS NO STATEMENT", AND THE DIFFERENCE IS DELIBERATE.
//
// A body holding only a comment PASSES. `TenantDatabaseRestoreVerificationExecutor` has one — a bare catch
// whose body explains that a projection failure must not strand the tenant in `Restoring` or replace a
// classified verification result with a less useful exception. **That catch does nothing on purpose and
// says so, which is the outcome this rule wants, not the one it should forbid.**
//
// The first draft required a STATEMENT and failed that catch. Requiring a statement would push correct code
// into writing `_ = 0;` to satisfy a test — and every such workaround makes the codebase worse in exchange
// for a number.
//
// What is left is decided by the GRAMMAR ALONE: is there anything between the braces? An earlier attempt at
// this class of question tried to judge whether a nearby comment counted as a *reason* and got it wrong
// twice in one hour in both directions — too narrow (a reason above the `try` was invisible) and then too
// wide (a comment inside the `try` body was credited as a reason for discarding an exception). **Whether
// prose is a good reason is not a question a test can answer. Whether anything was written is.**
//
// ---- WHAT THIS DELIBERATELY DOES NOT DO.
//
// It does not require a comment on every catch that discards its exception. That rule needs exactly the
// classifier that proved unreliable, and a guard enforcing a parser's idea of where reasons live is worse
// than no guard — it fails on correct code and passes on incorrect code, and both outcomes teach people to
// ignore it.
public sealed class EmptyCatchArchitectureTests
{
  [Fact]
  public void No_catch_in_source_has_an_empty_body()
  {
    var offenders = new List<string>();
    var files = 0;
    var clauses = 0;

    foreach (var file in SourceFiles())
    {
      var text = File.ReadAllText(file);
      if (!text.Contains("catch", StringComparison.Ordinal))
      {
        continue;
      }

      files++;

      // ⚠ SCANNED WITH COMMENTS AND STRING LITERALS BLANKED, BECAUSE THE WORD `catch` APPEARS IN BOTH.
      //
      // This file's own header says "catch" repeatedly, and so do explanatory comments across `src/`. A
      // raw scan treats each as a clause, finds the next `{` somewhere below, and measures a block that is
      // not a catch body at all. It produced no false red here only by luck — the block that follows a
      // sentence about catching is usually non-empty — and it inflated the clause count by about a quarter,
      // which quietly weakens the floor that count is supposed to support.
      // ⚠ THE BLANKED TEXT LOCATES CATCHES; THE ORIGINAL MEASURES THEIR BODIES. Using the blanked copy for
      // both was wrong and briefly reported five extra offenders: a comment-only body IS blank once its
      // comment is blanked, so exactly the catches this rule means to allow became the ones it flagged.
      // Blanking preserves length and newlines, so the two texts share indices and can be mixed safely.
      var scan = Blanked(text);

      foreach (var start in CatchBodies(scan))
      {
        clauses++;
        var body = BodyAt(text, start);
        if (body.Trim().Length > 0)
        {
          continue;
        }

        var line = text.AsSpan(0, start).Count('\n') + 1;
        offenders.Add($"{Path.GetFileName(file)}:{line}");
      }
    }

    // ⚠ ANTI-VACUITY, AND ITS LIMIT IS WORTH STATING RATHER THAN TRUSTING.
    //
    // These floors catch the walk COLLAPSING — a changed layout, a brace matcher that stopped matching —
    // because zero offenders out of zero clauses reads exactly like success. **They do not catch selective
    // invisibility**: if one file stopped being scanned, 100 clauses still clear a floor of 70 while the
    // empty catch inside it hides completely. A floor is protection against total failure of an instrument
    // and nothing else, and it is the cheaper half of the problem.
    Assert.True(files >= 40,
      $"only {files} source files containing `catch` were scanned; the walk has degraded and every count " +
      "below it is meaningless rather than reassuring.");
    Assert.True(clauses >= 70,
      $"only {clauses} catch clauses were found across {files} files; the matcher has stopped matching.");

    Assert.True(offenders.Count == 0,
      "a catch in src/ has an empty body, so a failure there is discarded without anything happening and " +
      "without any trace that it occurred:\n  " + string.Join("\n  ", offenders) +
      "\n\nHandle it, translate it into a result, or log it. `tests/` may do this in teardown; `src/` may " +
      "not.");
  }

  // Start index of each catch clause's opening brace. Written by hand rather than with a regex because the
  // optional declaration and optional `when` filter make the header shape awkward and the brace is what
  // matters.
  private static IEnumerable<int> CatchBodies(string text)
  {
    var index = 0;
    while (true)
    {
      index = text.IndexOf("catch", index, StringComparison.Ordinal);
      if (index < 0)
      {
        yield break;
      }

      var after = index + "catch".Length;

      // `catches` and `Recatch` are not `catch`.
      var before = index == 0 ? ' ' : text[index - 1];
      if (char.IsLetterOrDigit(before) || before == '_' ||
        (after < text.Length && (char.IsLetterOrDigit(text[after]) || text[after] == '_')))
      {
        index = after;
        continue;
      }

      // ⚠ THE FIRST `{` AFTER `catch` IS NOT NECESSARILY THE BODY, AND THIS COST A FALSE RED.
      //
      // `catch (DbUpdateException e) when (UniqueViolation(e) is { } index)` puts a PROPERTY PATTERN inside
      // the filter. Taking the first brace made `{ }` the body, so ten catches with real handling in them
      // were reported as empty. The declaration and the filter are skipped by counting parentheses — which
      // a regex could not do either, because the filter nests them.
      after = SkipParenthesised(text, after);
      after = SkipFilter(text, after);

      var brace = text.IndexOf('{', after);
      if (brace < 0)
      {
        yield break;
      }

      yield return brace;
      index = brace + 1;
    }
  }

  // Replaces the CONTENT of comments and string literals with spaces, preserving length and newlines so
  // every reported line number still points at the real line. Not a C# parser and does not need to be: the
  // only question is whether a `catch` token is code, and blanking is enough to answer it.
  private static string Blanked(string text)
  {
    var buffer = text.ToCharArray();
    var i = 0;

    while (i < buffer.Length)
    {
      var c = buffer[i];

      if (c == '/' && i + 1 < buffer.Length && buffer[i + 1] == '/')
      {
        while (i < buffer.Length && buffer[i] != '\n')
        {
          buffer[i++] = ' ';
        }

        continue;
      }

      if (c == '/' && i + 1 < buffer.Length && buffer[i + 1] == '*')
      {
        while (i < buffer.Length && !(buffer[i] == '*' && i + 1 < buffer.Length && buffer[i + 1] == '/'))
        {
          if (buffer[i] != '\n')
          {
            buffer[i] = ' ';
          }

          i++;
        }

        for (var j = i; j < Math.Min(i + 2, buffer.Length); j++)
        {
          buffer[j] = ' ';
        }

        i += 2;
        continue;
      }

      // `@"..."` and `"""..."""` both end at a quote that is not doubled; treating every quoted run the
      // same way is sufficient, because a `catch` inside any of them is not code either.
      if (c == '"')
      {
        buffer[i++] = ' ';
        while (i < buffer.Length && buffer[i] != '"')
        {
          if (buffer[i] == '\\' && i + 1 < buffer.Length && buffer[i + 1] != '\n')
          {
            buffer[i++] = ' ';
          }

          if (i < buffer.Length && buffer[i] != '\n')
          {
            buffer[i] = ' ';
          }

          i++;
        }

        if (i < buffer.Length)
        {
          buffer[i++] = ' ';
        }

        continue;
      }

      i++;
    }

    return new string(buffer);
  }

  // Steps over a balanced `( ... )` if one starts here, otherwise leaves the position alone.
  private static int SkipParenthesised(string text, int index)
  {
    while (index < text.Length && char.IsWhiteSpace(text[index]))
    {
      index++;
    }

    if (index >= text.Length || text[index] != '(')
    {
      return index;
    }

    var depth = 0;
    for (var i = index; i < text.Length; i++)
    {
      if (text[i] == '(')
      {
        depth++;
      }
      else if (text[i] == ')')
      {
        depth--;
        if (depth == 0)
        {
          return i + 1;
        }
      }
    }

    return index;
  }

  private static int SkipFilter(string text, int index)
  {
    var probe = index;
    while (probe < text.Length && char.IsWhiteSpace(text[probe]))
    {
      probe++;
    }

    if (!text.AsSpan(probe).StartsWith("when"))
    {
      return index;
    }

    return SkipParenthesised(text, probe + "when".Length);
  }

  private static string BodyAt(string text, int brace)
  {
    var depth = 0;
    for (var i = brace; i < text.Length; i++)
    {
      if (text[i] == '{')
      {
        depth++;
      }
      else if (text[i] == '}')
      {
        depth--;
        if (depth == 0)
        {
          return text[(brace + 1)..i];
        }
      }
    }

    return text[(brace + 1)..];
  }

  private static IEnumerable<string> SourceFiles()
  {
    var root = RepositoryRoot();
    return Directory
      .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
        && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
  }

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);
    return directory!.FullName;
  }
}
