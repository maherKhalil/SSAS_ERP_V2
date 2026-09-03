namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE PLATFORM READ PATH'S SCOPE GUARD — THE ONE THE OTHER PACKAGES ALL HAVE AND THIS ONE DID NOT.
// ==================================================================================================
//
// `Architecture-Principles.md` states the rule: *a complete scope guard has exactly two halves — the
// composed predicate for the unfiltered dimensions, and the ABSENCE of `IgnoreQueryFilters` for the
// filtered one.* HR, Position, Department, GL, Company and Attendance each carry their half in
// `EmployeeReadScopeArchitectureTests`, `PositionReadScopeArchitectureTests`,
// `DepartmentReadScopeArchitectureTests`, `GlReadScopeArchitectureTests` and `CompanyApiArchitectureTests`.
// **The Platform read path had neither half, and it is the path `AC-IAM-0001` depends on.**
//
// ---- ⚠⚠⚠ WHY THIS IS NEEDED HERE, IN ONE SENTENCE FROM THE CODE.
//
// `ListTenantUsersQueryHandler` passes NO tenant id to `TenantUserReadService`, which queries
// `dbContext.TenantUsers` with NO tenant predicate. The isolation is entirely
// `PersistenceDbContext.ConfigureTenantFilter` — `CurrentTenantId.HasValue && entity.TenantId ==
// CurrentTenantId.Value`, which fails closed. **So one `IgnoreQueryFilters()` in that file returns every
// tenant's users, and before this test nothing in the repository would have failed.**
//
// ---- ⚠⚠ THE RULE IS NOT A BAN, BECAUSE FOUR CALLERS ARE LEGITIMATE — AND THEY HAVE TWO DIFFERENT GROUNDS.
//
// A blanket `DoesNotContain` would redden correct code. The callers divide, and the division is the whole
// design of this test:
//
//   HAND-WRITTEN PREDICATE   `AccessTokenClaimsProvider`, `TenantAdministratorAuthority` ignore the filter
//                            and supply `TenantId == tenantId` THEMSELVES. The scoping moved, it did not
//                            vanish. **Mechanically checkable: the file contains `TenantId ==`.**
//
//   CROSS-TENANT BY DESIGN   `IdentityTenantMembershipReadService` answers *which tenants does this
//                            identity belong to* — a question with NO tenant to scope to, asked before a
//                            tenant is selected. It has no `TenantId ==` and must not have one.
//                            **Not mechanically derivable, so it must SAY SO.**
//
// **Each exemption asserts its own grounds; neither is listed by name in this test.** A file that starts
// ignoring filters must acquire a predicate or a declaration, and adding either is a deliberate act that
// shows up in review — which is the property a name list would destroy, since a name list is edited by the
// same person making the change.
//
// ---- ⚠ PLANT EVIDENCE, RECORDED BECAUSE A GREEN SCOPE GUARD PROVES NOTHING BY ITSELF.
//
//   `IgnoreQueryFilters()` added to `TenantUserReadService.ListAsync` — the exact leak this exists to
//   catch — reddened BOTH tests, naming the file and the two admissible remedies.
//
//   The `CROSS-TENANT BY DESIGN` marker removed from `IdentityTenantMembershipReadService` — **PASSED on
//   the first version of this test, which was a defect in the guard, not in the code.** See `Code(...)`.
//
// Both planted, both read as TEXT rather than as a colour, both reverted. **The leak plant was re-run
// after `Code(...)` was replaced by the repository's own `StripComments`, because a STRONGER stripper can
// over-strip: adopting it without re-planting would have been trusting a change to the instrument on the
// strength of a plant run against the previous one.**
//
// ---- ⚠ AND THE SIBLINGS DO NOT SHARE THE DEFECT. Checked rather than assumed, because *my fix was easy*
// is the tell that says look for the same class elsewhere.
//
//   `EmployeeReadScopeArchitectureTests`    `ReadHrCode` IS `StripComments(ReadHrSource(...))`
//   `PositionReadScopeArchitectureTests`    strips, same implementation
//   `DepartmentReadScopeArchitectureTests`  strips, same implementation
//
// **All three were already immune, and `EmployeeReadService.cs:25` proves it is load-bearing rather than
// incidental: that file DISCUSSES `IgnoreQueryFilters` in a comment, and its guard asserts
// `DoesNotContain` over the source. Without the strip, that guard would be permanently red.** So the
// convention exists because someone already hit this — the answer was *mine was the outlier*, not *fix
// them all*.
public sealed class PlatformReadScopeArchitectureTests
{
  private const string CrossTenantMarker = "CROSS-TENANT BY DESIGN";

  // ⚠⚠⚠ EVERY CODE SEARCH BELOW RUNS OVER `Code(...)`, NOT OVER THE RAW SOURCE, AND A PLANT IS WHY.
  //
  // The first version matched the raw file. Documenting this very rule at the call site — a comment saying
  // *"a `TenantId ==` predicate would be wrong here"* — MADE THE FILE SATISFY THE RULE. The guard was
  // defeated by prose ABOUT the guard, and the plant that should have reddened passed instead.
  //
  // **That is the failure mode in its purest form: a text matcher cannot tell an assertion from a
  // description of one, so writing down why a file is exempt is indistinguishable from being exempt.** It
  // runs both ways — `IgnoreQueryFilters` is also discussed in comments in sibling read services, which
  // would have put files into the *ignoring* set for mentioning the hazard they avoid.
  //
  // ⚠⚠ AND THIS IS THE REPOSITORY'S OWN `StripComments`, COPIED RATHER THAN INVENTED — `EmployeeReadScope`,
  // `PositionReadScope` and `DepartmentReadScope` each carry it verbatim, and `ReadHrCode` is defined as
  // `StripComments(ReadHrSource(...))`. **The convention already solved this and mine was the outlier.**
  //
  // My first version dropped only FULL-LINE comments. Theirs cuts at the first `//` on ANY line, so a
  // TRAILING comment after code is stripped too — strictly stronger, and the difference is exactly the
  // case my version would have missed: `.AsNoTracking() // never IgnoreQueryFilters here` would have put a
  // clean file into the ignoring set. **Adopting theirs rather than keeping mine, so a reader meets one
  // idiom in four files instead of two.**
  //
  // ⚠ THE SHARED BOUND: a `//` inside a string literal truncates that line. None occurs in this directory
  // and the same is true of the three siblings, which have run this way for longer than tonight. The marker
  // check deliberately still reads the RAW source — the marker IS a comment, and requiring it in code would
  // be nonsense.
  private static string Code(string source) => string.Join(
    Environment.NewLine,
    source
      .Split('\n')
      .Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
      }));

  [Fact]
  [Trait("Criterion", "AC-IAM-0001")]
  public void Every_platform_read_service_that_ignores_query_filters_supplies_its_own_scope()
  {
    var files = Directory
      .EnumerateFiles(
        Path.Combine(
          RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Queries"),
        "*.cs",
        SearchOption.TopDirectoryOnly)
      .Select(path => (Name: Path.GetFileName(path), Source: File.ReadAllText(path)))
      .Select(file => (file.Name, file.Source, Code: Code(file.Source)))
      .ToArray();

    // ---- ANTI-VACUITY, THREE WAYS, BECAUSE THIS TEST HAS THREE DIFFERENT WAYS TO PASS WHILE ASSERTING
    // ---- NOTHING.
    //
    // The population could empty (directory renamed, layout moved); the rule's antecedent could go
    // unsatisfied everywhere (nobody ignores filters, so the implication is vacuous); or EVERY file could
    // ignore filters, which would mean the global filter had stopped being the mechanism and this guard
    // would be measuring a codebase it no longer describes.
    Assert.True(files.Length >= 10,
      $"the Platform Queries directory yielded {files.Length} files; the walk has collapsed and every "
      + "assertion below is over an empty or truncated population.");

    var ignoring = files.Where(file => file.Code.Contains("IgnoreQueryFilters", StringComparison.Ordinal)).ToArray();
    var relying = files.Length - ignoring.Length;

    Assert.True(ignoring.Length >= 1,
      "no Platform read service calls IgnoreQueryFilters, so the rule below is vacuously true. Either the "
      + "call sites moved — in which case this guard now watches nothing — or the search string is wrong.");

    Assert.True(relying >= 1,
      "every Platform read service ignores query filters, so none relies on the global tenant filter and "
      + "the mechanism this guard protects is no longer in use.");

    // ---- THE RULE. Each ignoring file must carry ITS OWN grounds, of one of the two admissible kinds.
    foreach (var (name, source, code) in ignoring)
    {
      Assert.True(
        code.Contains("TenantId ==", StringComparison.Ordinal) ||
        source.Contains(CrossTenantMarker, StringComparison.Ordinal),
        $"{name} calls IgnoreQueryFilters and neither supplies a hand-written `TenantId ==` predicate nor "
        + $"declares `{CrossTenantMarker}`. Ignoring the global filter removes the ONLY tenant scoping on "
        + "that query — supply the predicate yourself, or state in the file why this read is legitimately "
        + "cross-tenant. Do not add the marker to silence this: it is read by a human, and a read that "
        + "should be tenant-scoped needs the predicate instead.");
    }
  }

  // ---- AND THE CONVERSE, WHICH IS THE HALF THAT ACTUALLY PROTECTS `TenantUserReadService`.
  //
  // The rule above constrains files that ALREADY ignore filters. It says nothing about a file that starts
  // to — which is the change that would leak the user list. This pins the read services that currently rely
  // on the global filter, by COUNT rather than by name: if one of them acquires an `IgnoreQueryFilters`
  // call, `relying` falls and this fails, and whoever made the change has to come here and say which
  // grounds apply.
  //
  // ⚠ A COUNT rather than a name list, deliberately: a name list is edited by the same hand that makes the
  // change and would be updated in the same commit without a thought. **A number forces the question to be
  // asked out loud in the diff.** The number is derived below, never typed — the assertion is that it has
  // not FALLEN, so adding a new read service that relies on the filter is free and taking one away is not.
  [Fact]
  [Trait("Criterion", "AC-IAM-0001")]
  public void The_platform_read_services_relying_on_the_global_tenant_filter_do_not_shrink()
  {
    var files = Directory
      .EnumerateFiles(
        Path.Combine(
          RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Queries"),
        "*.cs",
        SearchOption.TopDirectoryOnly)
      .Select(File.ReadAllText)
      .Select(Code)
      .ToArray();

    var relying = files.Count(code => !code.Contains("IgnoreQueryFilters", StringComparison.Ordinal));

    // Measured 2026-09-03: 15 files, 3 ignoring, 12 relying. The floor is the measurement, not a guess.
    Assert.True(relying >= 12,
      $"{relying} Platform read services rely on the global tenant filter, down from 12. A read service "
      + "that stopped relying on it either supplied its own predicate — fine, and the sibling test checks "
      + "that — or lost its tenant scoping entirely. Confirm which, then move this floor deliberately.");
  }

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }
}
