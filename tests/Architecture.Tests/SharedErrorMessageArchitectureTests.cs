using System.Reflection;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// AN ERROR RAISED BY SHARED CODE MUST NOT NAME A DOMAIN IN ITS MESSAGE (T-262).
// ==================================================================================================
//
// ---- WHAT THIS WAS WRITTEN FOR.
//
// `Persistence.UniqueConstraint` read *"The requested identity or access value already exists."* It is
// returned by BOTH unit-of-work classes for EVERY aggregate on SQL 2601/2627, and ten mappers translate it
// into a correct per-module wire code. **The code was right in all ten. The message was wrong in nine.**
//
// It cost nothing for as long as nobody could read it. T-261 put `Error.Message` on the wire as RFC 7807
// `detail`, and an Attendance caller posting a duplicate holiday began receiving
//
//     409  attendance.unique_conflict
//     detail: "The requested identity or access value already exists."
//
// **Making a latent artefact visible converts its defects into live ones.** Every one of these messages was
// written under the assumption that only a developer reading the constant would ever see it, and that
// assumption was retracted for all of them in a single commit.
//
// ---- WHY IT IS A DEFECT AND NOT A DESIGN, AND WHY THAT MATTERS FOR THE TEST.
//
// `Persistence.ConcurrencyConflict` — same origin, same 409, same breadth — reads *"The record was changed
// by another operation."* Domain-neutral and correct in all eight of its mappers. The two sat on adjacent
// lines of the same file, and one was written carefully. **So this guard has a genuine passing example
// rather than only the cases it was written to catch**, which is what makes it a rule about the codebase
// instead of a restatement of one bug.
public sealed class SharedErrorMessageArchitectureTests
{
  // The vocabulary of a specific bounded context. An error that shared infrastructure hands to every module
  // may not use these, because it is going to be read by a caller who was doing something else entirely.
  private static readonly string[] DomainNouns =
  [
    "identity", "access", "employee", "department", "position", "tenant", "company", "branch",
    "payroll", "attendance", "leave", "journal", "salary", "role", "permission", "invitation",
  ];

  [Fact]
  public void No_shared_persistence_error_names_a_domain_in_its_message()
  {
    var shared = SharedPersistenceErrors();

    // ⚠ THE FLOOR READS THE QUANTITY THE ASSERTION READS -- shared errors discovered, which is exactly what
    // `offenders` filters. A floor on assemblies or on types would survive the day the `Persistence.` prefix
    // stops matching, and that is the day this test must fail rather than pass over an empty set.
    Assert.True(shared.Length >= 3,
      $"only {shared.Length} shared persistence errors were discovered; three exist, so the walk has " +
      "degraded and 'no domain nouns' would be a statement about nothing.");

    var offenders = shared
      .Select(error => (error.Code, error.Message, Found: DomainNouns
        .Where(noun => error.Message.Contains(noun, StringComparison.OrdinalIgnoreCase))
        .ToArray()))
      .Where(row => row.Found.Length > 0)
      .ToArray();

    Assert.True(offenders.Length == 0,
      "a `Persistence.*` error is raised by shared code for every aggregate, so its message reaches callers " +
      "in every module — but these name a specific domain:\n  " +
      string.Join("\n  ", offenders.Select(row =>
        $"{row.Code}: \"{row.Message}\"  names: {string.Join(", ", row.Found)}")) +
      "\n\nWrite the message for a caller who was doing something else entirely.");
  }

  // The control on the control. If `ConcurrencyConflict` ever stops being found, the test above is passing
  // over a set that no longer contains its own worked example.
  [Fact]
  public void The_neutral_example_this_rule_was_derived_from_is_still_in_the_set()
  {
    var codes = SharedPersistenceErrors().Select(error => error.Code).ToArray();

    Assert.Contains("Persistence.ConcurrencyConflict", codes);
    Assert.Contains("Persistence.UniqueConstraint", codes);
    Assert.Contains("Persistence.WriteFailure", codes);
  }

  private static Error[] SharedPersistenceErrors() =>
    [.. Directory
      .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.dll")
      // `RepositoryPaths.ProjectName`, not `Path.GetFileNameWithoutExtension`, which
      // `RepositoryPathPortabilityTests` bans outright in this project — it caught this file on its first
      // gate. The ban is blanket by design: the helper is right for a filesystem path and wrong for an
      // MSBuild `Include`, and a rule that only fired on the unsafe kind would need a reader to classify
      // it correctly every time. Mine was the safe kind and the ban is still the right shape.
      .Select(RepositoryPaths.ProjectName)
      .Distinct(StringComparer.Ordinal)
      .Select(name => Assembly.Load(name!))
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.Name.EndsWith("Errors", StringComparison.Ordinal))
      .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
      .Where(field => field.FieldType == typeof(Error))
      .Select(field => (Error)field.GetValue(null)!)
      .Where(error => error.Code.StartsWith("Persistence.", StringComparison.Ordinal))
      .DistinctBy(error => error.Code, StringComparer.Ordinal)];
}
