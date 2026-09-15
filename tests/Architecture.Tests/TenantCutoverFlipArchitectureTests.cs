using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.TenantStorage;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Infrastructure.TenantStorage;

namespace SSAS.Architecture.Tests;

// THE BOUNDARIES OF THE ROUTING FLIP (ADR-020, TS-Storage Phase E4).
//
// The flip is the one irreversible step in the whole cutover. These guards protect the two properties that
// make it safe: that its three facts move together inside one transaction, and that nothing downstream of
// the commit — an invalidation, a cache, a caller — can undo or reverse it.
public sealed class TenantCutoverFlipArchitectureTests
{
  private static readonly Assembly InfrastructureAssembly = typeof(TenantCutoverWriteFence).Assembly;

  private static readonly Type FlipService = InfrastructureAssembly.GetType(
    "SSAS.Platform.Infrastructure.TenantStorage.TenantCutoverRoutingFlipService")!;

  // ---- ONE TRANSACTION, AND THE THREE FACTS INSIDE IT. A committed state where the assignment moved but
  // the operation still said Frozen would tell an operator the tenant was mid-copy while its traffic had
  // already moved.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_flip_opens_exactly_one_platform_transaction_and_mutates_all_three_facts_inside_it()
  {
    var source = SourceOf("TenantCutoverRoutingFlipService.cs");

    // Exactly one transaction boundary in the whole service.
    Assert.Equal(1, Occurrences(source, "BeginTransactionAsync"));
    Assert.Equal(1, Occurrences(source, "CommitAsync"));

    var body = Between(source, "BeginTransactionAsync", "CommitAsync");

    // The assignment ends, the replacement is created, and the operation records the flip — all before the
    // single commit.
    Assert.Contains(".End(Actor", body, StringComparison.Ordinal);
    Assert.Contains("TenantDatabaseAssignment.Create(", body, StringComparison.Ordinal);
    Assert.Contains("RecordRoutingFlip(", body, StringComparison.Ordinal);

    // TWO FLUSHES, deliberately: the filtered unique index admits one active assignment and SQL Server has
    // no deferrable constraints, so the vacate must be flushed before the fill.
    Assert.Equal(2, Occurrences(body, "SaveChangesAsync"));
  }

  // ---- INVALIDATION IS AFTER THE COMMIT, AND CANNOT UNDO IT.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Local_invalidation_happens_after_commit_and_cannot_roll_the_flip_back()
  {
    var source = SourceOf("TenantCutoverRoutingFlipService.cs");

    var commitIndex = source.IndexOf("CommitAsync", StringComparison.Ordinal);
    var invalidateIndex = source.IndexOf("Invalidate(tracked.TenantId)", StringComparison.Ordinal);

    Assert.True(commitIndex > 0 && invalidateIndex > commitIndex,
      "invalidation must be reachable only after the transaction has committed");

    // The invalidation failure path produces an ERROR VALUE, never a rollback or a rethrow.
    var invalidate = Between(source, "private Error? Invalidate(", "private async Task<Result>");
    Assert.Contains("return TenantStorageErrors.CutoverInvalidationIncomplete", invalidate, StringComparison.Ordinal);
    Assert.DoesNotContain("Rollback", invalidate, StringComparison.Ordinal);
    Assert.DoesNotContain("throw", invalidate, StringComparison.Ordinal);

    // The flip holds the INVALIDATOR, not the cache: it may evict, and cannot write cache entries.
    Assert.Contains(typeof(ITenantRoutingCacheInvalidator), Dependencies(FlipService));
    Assert.DoesNotContain(typeof(ITenantRoutingCache), Dependencies(FlipService));
  }

  // ---- ⚠⚠⚠ THE STATUS MACHINE, WHICH IS THE MECHANISM A FLIPBACK WOULD HAVE TO USE (T-088).
  //
  // The test below this one checks NAMES — six reversal verbs and three more — and a flipback called
  // `SoftUnflip` or `Reopen2` passes it. **This one is name-independent**: whatever a new transition is
  // called, it has to assign `Status`, and every assignment in the aggregate is listed here with the member
  // that performs it. An added transition appears as a new pair and reddens regardless of its spelling.
  //
  // ⚠ AN EXACT LIST RATHER THAN A BAN, so the expected value is non-empty and a collapsed scan FAILS
  // instead of passing. It also states the machine positively — these six transitions and no others — which
  // is a thing a reader can check against ADR-020, where "no flipback exists" is only a thing they can hope.
  //
  // ⚠⚠ THE BOUND: this reads the aggregate's SOURCE, so a status written from outside the aggregate — a
  // repository setting the property directly, a migration updating the column — is invisible here. That is
  // a real hole and it is stated rather than closed; the property is `private set` today, which is what
  // makes the aggregate the only writer, and this test does not assert that.
  //
  // ⚠⚠ REACH PROBE, BOTH COLOURS FROM ONE PLANT (T-088). `SoftUnwind(actor, occurredUtc)` was planted on the
  // aggregate, setting `Status = Frozen` — a flipback whose name contains NONE of the nine verbs the test
  // below matches.
  //
  //   The name-based test -> GREEN. It cannot see a reversal that is not spelled like one.
  //   This test           -> RED, naming it: `"SoftUnwind -> Frozen"` appended to the transition list.
  //
  // Same plant, same run, opposite colours. That is the difference between checking the mechanism and
  // checking the vocabulary, and it is why both tests exist rather than one wider regex.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_cutover_aggregate_assigns_status_on_exactly_these_six_transitions()
  {
    var source = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Domain", "TenantStorage",
      "TenantCutoverOperation.cs"));

    var member = new Regex(
      @"^\s*(?:public|private|internal|protected)[\w\s<>,\[\]\?\.]*?\s(\w+)\s*\(", RegexOptions.Multiline);
    var assignment = new Regex(@"Status\s*=\s*TenantCutoverOperationStatus\.(\w+)\s*;");

    var members = member.Matches(source).Cast<Match>().ToArray();
    Assert.True(members.Length >= 8,
      // ⚠ PHRASED SO NUMBER AGREEMENT CANNOT ARISE (T-103). This read "only {n} member declarations were
      // parsed" and a plant that reduced the parse to ONE — rather than to zero — rendered *"only 1 member
      // declarations were parsed"*. The zero case reads correctly and the one case does not, which is why
      // the adversarial subject for a message is the value that most nearly looks right.
      $"the declaration pattern matched {members.Length} of the aggregate's members; at least eight were " +
      "expected. It has stopped matching, and every status assignment below would be attributed to " +
      "whichever member the parse last recognised rather than to the one that performs it.");

    var transitions = assignment.Matches(source).Cast<Match>()
      .Select(match =>
      {
        var owner = members.LastOrDefault(candidate => candidate.Index < match.Index);
        return $"{owner?.Groups[1].Value ?? "<none>"} -> {match.Groups[1].Value}";
      })
      .ToArray();

    Assert.Equal(
      [
        "TenantCutoverOperation -> Preparing",
        "Freeze -> Frozen",
        "ReleaseFreeze -> Abandoned",
        "RecordRoutingFlip -> RoutingFlipped",
        "Complete -> Completed",
        "FailFreeze -> Abandoned"
      ],
      transitions);
  }

  // ================================================================================================
  // ⚠⚠⚠ THE HINGE THE MECHANISM TEST HANGS ON, ASSERTED BY NOTHING UNTIL T-091
  // ================================================================================================
  //
  // `The_cutover_aggregate_assigns_status_on_exactly_these_six_transitions` reads the AGGREGATE'S SOURCE.
  // That is only a statement about the system while **the aggregate is the sole writer of `Status`** —
  // which is true today because the setter is private, and which that test does not assert.
  //
  // ⚠ WIDEN THE SETTER AND NOTHING BREAKS. The transition test keeps passing, having quietly narrowed
  // from *these are the only status transitions* to *these are the only status transitions THE AGGREGATE
  // PERFORMS*, while a repository or a handler writes the property directly. **A guard whose scope shrinks
  // without its colour changing is the failure mode this whole night has been about**, and here it is in
  // a test I wrote three hours ago.
  //
  // ⚠⚠ NAMED TRIGGER: whoever relaxes this setter will be doing it for something else entirely — an EF
  // mapping that wants a settable property, a serializer, a test convenience, an interface like
  // `IBranchOwnedEntity` that declares `{ get; set; }` and forces the shape. **The invalidation of the
  // transition guard is a pure side effect they have no reason to think about**, which is exactly the
  // case a tripwire is for: it pays where its author cannot see.
  //
  // ⚠ BY REFLECTION, NOT BY READING THE SOURCE. A source scan for `private set` is defeated by
  // reformatting, by a comment, and by an explicit backing field — and the property's accessibility is a
  // fact the runtime already knows.
  //
  // ⚠⚠⚠ PLANTED, AND THE PLANT MEASURED THE CLAIM RATHER THAN ILLUSTRATING IT. With the setter changed to
  // `internal set`, the flip suite ran **9 passed, 1 failed** — this test alone. ***THE TRANSITION TEST
  // STAYED GREEN.*** So the silent narrowing is not a hypothesis about what could happen: it is what does
  // happen, and this tripwire is the only thing between that change and a guard that has quietly stopped
  // bounding what its name says it bounds.
  [Fact]
  [Trait("Decision", "ADR-020")]
  [Trait("Tripwire", "cutover-status-private-setter")]
  public void The_cutover_status_setter_is_private_to_the_aggregate()
  {
    var status = typeof(TenantCutoverOperation).GetProperty(nameof(TenantCutoverOperation.Status));
    Assert.NotNull(status);

    var setter = status!.GetSetMethod(nonPublic: true);
    Assert.True(setter is not null,
      "TenantCutoverOperation.Status has no setter at all any more. That is not a failure of ADR-020, but " +
      "it does mean the transition list in " +
      "`The_cutover_aggregate_assigns_status_on_exactly_these_six_transitions` can no longer be assigned " +
      "the way that test parses, and that test is now asserting something about dead syntax.");

    Assert.True(setter!.IsPrivate,
      $"TenantCutoverOperation.Status now has a setter that is {Visibility(setter)}, so THE AGGREGATE IS " +
      "NO LONGER THE ONLY THING THAT CAN WRITE IT.\n" +
      "  This is not primarily a problem with this property. It is a problem with " +
      "`The_cutover_aggregate_assigns_status_on_exactly_these_six_transitions`, which parses this file's " +
      "source and derives the complete set of status transitions from it. THAT TEST HAS JUST STOPPED " +
      "BOUNDING WHO CAN WRITE STATUS and now proves only that the aggregate itself is well behaved — while " +
      "a repository, a handler or a mapping profile can set `RoutingFlipped` back to `Frozen` and neither " +
      "test will say a word.\n" +
      "  If the setter must widen, the transition test has to be replaced by something that reads every " +
      "WRITER of the property rather than every assignment inside one file. Do not simply delete this.");
  }

  private static string Visibility(MethodInfo method) =>
    method switch
    {
      { IsPublic: true } => "public",
      { IsFamily: true } => "protected",
      { IsAssembly: true } => "internal",
      { IsFamilyOrAssembly: true } => "protected internal",

      // ⚠ UNREACHABLE TODAY, AND IT PRINTS A FACT RATHER THAN A LABEL (T-103). The four arms above cover
      // every non-private accessibility C# can express, and this is only called once `IsPrivate` is false.
      // **The day this arm is reached is the day those four stopped covering the space** — which is exactly
      // when a reader needs the raw value, not `"non-private"`, which would tell them only that MY SWITCH
      // did not recognise it. A fallback branch describing the matcher carries no information about the
      // subject.
      // ⚠ DO NOT DELETE AS DEAD CODE and DO NOT THROW: a tripwire that throws on an unexpected
      // accessibility fails for the wrong reason, and this one exists to report, not to refuse.
      _ => $"unrecognised, raw access flags {method.Attributes & MethodAttributes.MemberAccessMask}"
    };

  // ---- NO FLIPBACK EXISTS, at any layer. ADR-020 forbids a simple reversal once the target may have been
  // written to, and an API offering one would be reached for during exactly the incident where it is least
  // safe.
  //
  // ⚠ THIS IS A VOCABULARY AND THE NAME NOW SAYS SO (T-088). It was `No_automatic_flipback_path_exists`,
  // which claims a mechanism; the predicate is nine literal verbs over two types. `SoftUnflip` passes it.
  // The mechanism is asserted by `The_cutover_aggregate_assigns_status_on_exactly_these_six_transitions`
  // above, and this remains because a reversal named for what it is should be caught at the CONTRACT — an
  // interface method that is declared and not yet implemented assigns no status and is invisible there.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void No_cutover_method_is_named_for_a_reversal()
  {
    foreach (var name in typeof(ITenantCutoverRoutingFlipService).GetMethods().Select(method => method.Name))
    {
      foreach (var reversal in ReversalVerbs)
      {
        Assert.DoesNotContain(reversal, name, StringComparison.OrdinalIgnoreCase);
      }
    }

    // The domain admits no transition out of RoutingFlipped except forward: release refuses it, and there
    // is no method that returns it to Frozen or Preparing.
    var operation = typeof(TenantCutoverOperation);
    var transitions = operation
      .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
      .Select(method => method.Name)
      .ToArray();

    Assert.DoesNotContain(transitions, name =>
      name.Contains("Unfreeze", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Reopen", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Revert", StringComparison.OrdinalIgnoreCase));

    var source = SourceOf("TenantCutoverRoutingFlipService.cs");
    Assert.DoesNotContain("ReleaseFreeze", source, StringComparison.Ordinal);
  }

  // ---- THE FENCE IS ROUTE-AWARE. Refusing by TenantId alone would freeze the tenant forever after the
  // flip; permitting by TenantId alone would let a stale context write to the database it just left.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_write_fence_decides_per_route_rather_than_per_tenant()
  {
    var admit = typeof(ITenantWriteFence).GetMethod(nameof(ITenantWriteFence.AdmitWriteAsync))!;
    var parameters = admit.GetParameters().Select(parameter => parameter.Name).ToArray();

    Assert.Contains("tenantId", parameters);
    Assert.Contains("tenantDatabaseId", parameters);

    // The routed context carries the database it was bound to, captured when routing was resolved.
    var context = InfrastructureAssembly.GetType(
      "SSAS.Platform.Infrastructure.Persistence.TenantErp.TenantDbContext")!;
    Assert.Contains(
      context.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
      parameter => parameter.Name == "tenantDatabaseId");

    // The gate itself distinguishes the two sides rather than answering yes/no for the tenant.
    var gate = typeof(TenantCutoverWriteGate);
    Assert.NotNull(gate.GetProperty(nameof(TenantCutoverWriteGate.RefusesEveryWrite)));
    Assert.NotNull(gate.GetMethod(nameof(TenantCutoverWriteGate.PermitsWriteTo)));
    Assert.NotNull(gate.GetProperty(nameof(TenantCutoverWriteGate.SourceTenantDatabaseId)));
    Assert.NotNull(gate.GetProperty(nameof(TenantCutoverWriteGate.TargetTenantDatabaseId)));
  }

  // ---- THE POST-CUTOVER OBSERVATION IS SET BY A GENUINE TARGET WRITE AND NOTHING ELSE.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void Post_cutover_write_observation_is_recorded_only_from_the_write_admission_path()
  {
    // Exactly one production caller: the write fence, on the admission path.
    var callers = CutoverSourceFiles
      .Where(file => SourceOf(file).Contains("RecordPostCutoverWriteAsync", StringComparison.Ordinal))
      .ToArray();

    Assert.Equal(["TenantCutoverWriteFence.cs"], callers);

    // THE FLIP DOES NOT SET IT. Routing changing is not an application write, and conflating the two would
    // destroy the only fact that distinguishes the rollback regimes.
    Assert.DoesNotContain(
      "PostCutoverWriteObservedUtc",
      SourceOf("TenantCutoverRoutingFlipService.cs"),
      StringComparison.Ordinal);

    // ...and it is write-once in the domain rather than by the caller remembering.
    var record = typeof(TenantCutoverOperation)
      .GetMethod(nameof(TenantCutoverOperation.RecordPostCutoverWrite))!;
    Assert.NotNull(record);
  }

  // ---- THE FLIP DOES NOT REIMPLEMENT THE COPY. It invokes E3's validation rather than carrying a second,
  // divergent notion of what "the target matches" means.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_flip_reuses_the_copy_services_validation_rather_than_duplicating_it()
  {
    Assert.Contains(typeof(ITenantCutoverCopyService), Dependencies(FlipService));

    var source = SourceOf("TenantCutoverRoutingFlipService.cs");
    foreach (var copyMechanism in new[] { "SqlBulkCopy", "ColumnMappings", "WriteToServerAsync" })
    {
      Assert.DoesNotContain(copyMechanism, source, StringComparison.Ordinal);
    }
  }

  // ---- NO BROKER, NO HTTP, NO CUSTOMER-MANAGED CUTOVER, NO DESTRUCTION.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void The_flip_slice_introduces_no_transport_endpoint_or_destructive_path()
  {
    var source = SourceOf("TenantCutoverRoutingFlipService.cs");

    foreach (var forbidden in new[]
    {
      "StackExchange", "ServiceBus", "RabbitMQ", "Kafka", "HttpClient",
      "TenantDatabaseHostingMode.CustomerManaged", "DROP ", "DELETE FROM", "TRUNCATE"
    })
    {
      Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }

    Assert.All(Dependencies(FlipService), parameter => Assert.DoesNotContain(
      "Microsoft.AspNetCore", parameter.Namespace ?? string.Empty, StringComparison.Ordinal));

    Assert.False(typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(FlipService));
  }

  // ---- THE DATABASE GUARD EXISTS, and is a guard rather than a generator.
  [Fact]
  [Trait("Decision", "ADR-020")]
  public void A_routing_version_guard_trigger_is_shipped_and_never_assigns_the_version()
  {
    // PINNED TO E4'S OWN MIGRATION BY NAME. Two migrations now create this trigger — E4's original and the
    // E5 one that strengthens it — and selecting "the first file containing the name" silently depended on
    // directory enumeration order to decide which slice this test was describing.
    var migration = MigrationSourceNamed("AddRoutingVersionGuard");
    Assert.NotNull(migration);

    // ASSERTED AGAINST THE SQL, NOT THE FILE. Scanning the whole migration would match ordinary English in
    // the comments explaining the design — "while", "insert" — and a guard that trips on its own rationale
    // gets deleted rather than heeded.
    var sql = Between(migration!, "CREATE TRIGGER", "DROP TRIGGER");

    // AFTER INSERT, UPDATE — what E4 shipped. NOT a claim about the trigger in the database today: E5 added
    // DELETE to it, and the assertion that the live guard covers deletion belongs to that slice's test
    // (TenantCutoverOrchestrationArchitectureTests). Repeating the old "and no DELETE" here would make this
    // file assert the opposite of what ships.
    Assert.Contains("AFTER INSERT, UPDATE", sql, StringComparison.Ordinal);

    // IT REJECTS; IT NEVER WRITES. A trigger that supplied the next version would hide a caller that forgot
    // to advance it.
    Assert.Contains("THROW 51020", sql, StringComparison.Ordinal);
    Assert.Contains("THROW 51021", sql, StringComparison.Ordinal);
    Assert.DoesNotContain("UPDATE [platform].[TenantDatabaseAssignments]", sql, StringComparison.Ordinal);
    Assert.DoesNotContain("INSERT INTO", sql, StringComparison.OrdinalIgnoreCase);

    // SET-BASED, so a multi-row statement cannot slip a violation past it, and no cursor or loop appears.
    Assert.DoesNotContain("CURSOR", sql, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("WHILE ", sql, StringComparison.OrdinalIgnoreCase);

    // No external reach: a trigger runs inside the caller's transaction, so anything beyond this database
    // would hold locks across a network call.
    foreach (var forbidden in ForbiddenInTriggerSql)
    {
      Assert.DoesNotContain(forbidden, sql, StringComparison.OrdinalIgnoreCase);
    }

    // Persisted message text is nvarchar.
    Assert.Contains("N'A new tenant database assignment", sql, StringComparison.Ordinal);
  }

  private static readonly string[] ReversalVerbs =
    ["Rollback", "Revert", "Unflip", "FlipBack", "Restore", "Undo"];

  // Every cutover component that could plausibly reach for the post-cutover observation.
  private static readonly string[] CutoverSourceFiles =
  [
    "TenantCutoverWriteFence.cs", "TenantCutoverRoutingFlipService.cs", "TenantCutoverCopyService.cs",
    "TenantCutoverFreezeService.cs", "TenantCutoverTableCopier.cs", "TenantCutoverCopyValidator.cs"
  ];

  private static readonly string[] ForbiddenInTriggerSql =
    ["xp_cmdshell", "OPENQUERY", "sp_send_dbmail", "OPENROWSET"];

  private static int Occurrences(string source, string token)
  {
    var count = 0;
    var cursor = 0;
    while ((cursor = source.IndexOf(token, cursor, StringComparison.Ordinal)) >= 0)
    {
      count++;
      cursor += token.Length;
    }

    return count;
  }

  private static string Between(string source, string start, string end)
  {
    var from = source.IndexOf(start, StringComparison.Ordinal);
    Assert.True(from >= 0, $"expected marker not found: {start}");
    var to = source.IndexOf(end, from, StringComparison.Ordinal);
    return to < 0 ? source[from..] : source[from..to];
  }

  private static Type[] Dependencies(Type type) =>
    type.GetConstructors()
      .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
      .Distinct()
      .ToArray();

  private static string SourceOf(string fileName)
  {
    var full = Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "TenantStorage", fileName);
    Assert.True(File.Exists(full), $"expected source file not found: {full}");
    return File.ReadAllText(full);
  }

  // Selected by FILE NAME, so a test that describes one migration cannot end up reading another.
  private static string? MigrationSourceNamed(string migrationName)
  {
    var directory = Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations");

    var file = Directory.EnumerateFiles(directory, $"*_{migrationName}.cs").SingleOrDefault();
    return file is null ? null : File.ReadAllText(file);
  }

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
    {
      directory = directory.Parent;
    }

    Assert.NotNull(directory);
    return directory!.FullName;
  }

  static TenantCutoverFlipArchitectureTests() => Assert.NotNull(FlipService);
}
