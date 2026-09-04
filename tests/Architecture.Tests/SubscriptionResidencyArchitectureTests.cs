using System.Reflection;
using System.Text.RegularExpressions;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Architecture.Tests;

// THE COMMERCIAL PLANE IS PLATFORM-RESIDENT AND DOES NOT TRAVEL AT CUTOVER (FP-014, `DEC-SUB-0011`).
//
// ---- WHY THIS NEEDS A TEST RATHER THAN A COMMENT.
//
// `TenantCutoverCopyPlan.Build` derives the Shared→Dedicated manifest by **reflecting over
// `ITenantOwnedEntity` within the model it is handed**. That is a good design — the manifest cannot drift
// from the model — and it has one consequence worth guarding: a commercial entity that acquired
// `ITenantOwnedEntity`, or was added to the tenant model by mistake, would be **swept into the copy
// automatically and silently**.
//
// The failure would not look like a failure. The cutover would succeed, and a tenant's new dedicated
// database would carry a copy of its subscription history — which by `ADR-017` and `DEC-SUB-0003` lives in
// the Platform database and nowhere else, and which the entitlement resolver would then read from the wrong
// place or not at all.
//
// So the absence is asserted rather than assumed, which is what the task asked for: an entity added to the
// Platform context could be swept in by a future reflection-based manifest, and nothing else would notice.
public sealed class SubscriptionResidencyArchitectureTests
{
  // The commercial types, named rather than discovered. A scan could pass vacuously; naming them means a
  // type renamed or moved fails here rather than dropping silently out of the check.
  private static readonly Type[] CommercialTypes =
  [
    typeof(SubscriptionPlan),
    typeof(PlanModuleGrant),
    typeof(PlanLimit),
    typeof(PlanPrice),
    typeof(ModuleDefinition),
    typeof(TenantSubscription),
    typeof(TenantEntitlementGrant),
  ];

  // ---- WHAT THIS FILE PROVES AND WHAT IT DOES NOT (T-074, T-075).
  //
  // The comment above says the interface is what keeps these types out of the manifest. **That is half
  // true and the missing half matters.** `TenantCutoverCopyPlan.Build` selects `ITenantOwnedEntity`
  // WITHIN THE MODEL IT IS HANDED, and it is handed `ITenantModelSource.Model`. So the interface decides
  // which of the tenant model's entities are copied; **membership of that model decides which entities are
  // candidates at all.**
  //
  // Both directions have a counter-example in this tree:
  //
  //   TenantUser  carries ITenantOwnedEntity and does NOT travel — it is not in the tenant model
  //   Branch      is a Platform.Domain type that DOES travel — TenantDbContext maps it deliberately
  //
  // **So these assertions are true, cheap, and not the property that decides residency.** They pass today
  // because the commercial types happen to satisfy both. A Platform type that entered the tenant model
  // WITHOUT the interface would be invisible here — proved by planting `SubscriptionPlanConfiguration`
  // into the tenant model, which left this file green and `TenantModelResidencyTests` red.
  //
  // Keep them. They assert something worth asserting: a commercial type that ACQUIRED the interface would
  // be swept in the moment anything put it in the model, and this is the cheaper of the two guards to
  // notice. The residency itself is asserted in `TenantModelResidencyTests`.

  // ---- NONE OF THEM IS TENANT-OWNED, WHICH IS ONE OF THE TWO THINGS KEEPING THEM OUT OF THE MANIFEST.
  //
  // `TenantSubscription` and `TenantEntitlementGrant` both carry a `TenantId`, and that is exactly why this
  // is worth asserting: they LOOK tenant-owned. The tenant is the **subject** of the agreement, never its
  // owner (`DEC-SUB-0002`) — the rows are platform-administered commercial records about a tenant, and a
  // tenant cannot read, still less write, its own.
  [Fact]
  [Trait("Criterion", "AC-SUB-0030")]
  [Trait("Criterion", "AC-SUB-0029")]
  // ==================================================================================================
  // `AC-SUB-0030`, pasted — *"Expiry writes nothing to `Tenant`. The tenant's `TenantStatus` remains
  // `Active`, and a suspended tenant that is paid up remains `Suspended`"*
  //
  // `AC-SUB-0029`, pasted — *"A tenant whose term has expired **authenticates successfully** and is refused
  // every gated module. A **suspended or archived** tenant is still refused at authentication, and the two
  // outcomes remain **distinct** — one is commercial and reversible by the customer, the other
  // administrative"*
  // ==================================================================================================
  //
  // ⚠⚠⚠ THE TWO CRITERIA ARE ONE STRUCTURAL PROPERTY WRITTEN FROM BOTH ENDS, AND THE PAIR OF GUARDS IS
  // WHAT MAKES *DISTINCT* MECHANICAL RATHER THAN OBSERVED:
  //
  //   `The_authentication_surface_cannot_see_entitlement`   authentication cannot refuse for a COMMERCIAL
  //   (`AuthenticationMilestoneArchitectureTests`)          reason, because it cannot see one
  //   this test                                             the commercial surface cannot change an
  //                                                         ADMINISTRATIVE outcome, because it cannot
  //                                                         reach `TenantStatus`
  //
  // ***TWO OUTCOMES ARE DISTINCT WHEN NEITHER MECHANISM CAN REACH THE OTHER'S INPUT.*** A behavioural pair
  // — an expired tenant logging in, a suspended one refused — shows they ARE distinct today. **These two
  // guards show they CANNOT CONVERGE**, which is the claim `AC-SUB-0029` actually makes about a
  // reversible-by-the-customer state versus an administrative one.
  //
  // ⚠ AND `AC-SUB-0030`'s FIRST SENTENCE IS AN ABSENCE OF A WRITE, WHICH HAS NO BEHAVIOURAL WITNESS AT
  // ALL. *"Expiry writes nothing to `Tenant`"* — there is no expiry EVENT: `HasExpiredAt` is a pure
  // function of the term against the clock, nothing is written when a term ends and no job runs
  // (`OD-SUB-0010`, quoted in `AC-SUB-0026`). **So there is no moment at which the write could be observed
  // not to happen, and the only assertable form is that the code which would do it does not exist.**
  //
  // ⚠⚠ THE BAN IS ON `TenantStatus` AND THE LIFECYCLE VERBS, NOT ON THE WORD `Tenant`. The commercial
  // surface names tenants constantly — `TenantSubscription`, `TenantEntitlementGrant`, `TenantId` — and a
  // ban on `Tenant` would be a false red on every file it is meant to protect. **A guard whose false
  // positives outnumber its true ones is one somebody switches off.**
  public void The_commercial_surface_cannot_reach_tenant_status()
  {
    var commercialFiles = CommercialSourceFiles();

    const string lifecycleVocabulary = @"(?:TenantStatus|\.Suspend\(|\.Archive\(|\.Activate\(|TenantStatusChangeReason)";
    // The matcher control: it must match the real forms and not match the commercial surface's own
    // tenant-shaped names, which is the false red this ban is designed to avoid.
    Assert.Matches(lifecycleVocabulary, "if (tenant.TenantStatus != TenantStatus.Active)");
    Assert.Matches(lifecycleVocabulary, "tenant.Suspend(reason, actor, eventId, now);");
    Assert.DoesNotMatch(lifecycleVocabulary, "public Guid TenantId { get; private set; }");
    Assert.DoesNotMatch(lifecycleVocabulary, "var grant = new TenantEntitlementGrant();");

    var offenders = commercialFiles
      .Where(path => Regex.IsMatch(CodeOnly(path), lifecycleVocabulary, RegexOptions.CultureInvariant))
      .Select(Path.GetFileName)
      .ToArray();

    Assert.Empty(offenders);
  }

  [Fact]
  // ⚠⚠⚠ DELIBERATELY NO `[Trait]`. `AC-SUB-0026` DECLARES ITS OWN VACUITY, so this GUARDS the vacuity
  // and does not WITNESS the criterion — a trait here would enter the census as coverage, which is what
  // the guard exists to prevent being recorded. Same precedent as the `AC-SUB-0008` guard in
  // `PlatformInfrastructureRegistrationTests`. *A bucket meaning "not cited" cannot be implemented with
  // the thing that means "cited".*
  //
  // ==================================================================================================
  // `AC-SUB-0026`, pasted — *"Losing entitlement to a module deletes **no row** in that module's tables —
  // counts before and after are identical — and every record is readable again on re-entitlement.
  // ⚠ **The guarantee holds and the TEST IT ASKS FOR CANNOT BE WRITTEN (2026-08-30).** There is no
  // entitlement-lapse event: `HasExpiredAt` is a pure function of the term against the clock, nothing is
  // written when a term ends and no job runs, **so there is no moment at which a deletion could occur and
  // no before-and-after to count** (`OD-SUB-0010`). **It is satisfied by the absence of the mechanism it
  // guards against, which is not the same as being implemented** — whoever builds a lapse path must
  // re-check this criterion, because that commit is the one that can violate it"*
  // ==================================================================================================
  //
  // ***THE CRITERION NAMES ITS OWN RE-CHECK CONDITION AND THEN LEAVES IT TO A HUMAN NOTICING.*** "Whoever
  // builds a lapse path must re-check this" is a rule with no event to hang on: the person building that
  // path is the one least likely to read a criterion filed under a guarantee that currently holds.
  // **This test is that event.** The day a deletion or a scheduled job appears in the commercial surface,
  // it reddens and the author has to open `AC-SUB-0026` and decide.
  //
  // ⚠⚠ THE BAN IS ON THE MECHANISM THE VACUITY RESTS ON, NOT ON THE CRITERION'S SUBJECT. The criterion is
  // about rows in a MODULE's tables; those tables are not FP-014's and a ban over them would be
  // unmaintainable. **What is assertable is that the commercial surface contains nothing that could delete
  // anything and nothing that runs on a timer** — which is precisely what `OD-SUB-0010` claims and what a
  // lapse path would have to introduce.
  //
  // ⚠ IT IS ALSO NOT A PROOF THAT NO LAPSE PATH COULD EXIST ELSEWHERE. A deletion written into a MODULE's
  // own code would not be seen here. **The guard covers the surface whose absence of a mechanism the
  // criterion cites**, which is the claim actually made — and saying so is the difference between a bound
  // and an overstatement.
  public void The_commercial_surface_contains_no_lapse_mechanism()
  {
    var commercialFiles = CommercialSourceFiles();

    const string lapseVocabulary =
      @"(?:\.Remove\(|\.RemoveRange\(|ExecuteDelete|BackgroundService|IHostedService|new Timer\()";
    // The matcher control: it must match the forms a lapse path would take, and not match the reads and
    // appends this surface is made of.
    Assert.Matches(lapseVocabulary, "context.TenantSubscriptions.Remove(row);");
    Assert.Matches(lapseVocabulary, "public sealed class LapseSweeper : BackgroundService");
    Assert.DoesNotMatch(lapseVocabulary, "context.TenantSubscriptions.Add(row);");
    Assert.DoesNotMatch(lapseVocabulary, "if (Term is null || Term.HasExpiredAt(instant))");

    var offenders = commercialFiles
      .Where(path => Regex.IsMatch(CodeOnly(path), lapseVocabulary, RegexOptions.CultureInvariant))
      .Select(Path.GetFileName)
      .ToArray();

    Assert.Empty(offenders);
  }

  // ---- SHARED BY THE TWO SURFACE SCANS ABOVE, because they walk the same population and a second copy of
  // ---- the walk is a second thing to keep in step.
  private static string[] CommercialSourceFiles()
  {
    var root = FindRepositoryRoot();
    var files = new[]
      {
        Path.Combine(root, "src", "Platform", "SSAS.Platform.Domain", "Subscriptions"),
        Path.Combine(root, "src", "Platform", "SSAS.Platform.Application", "Subscriptions")
      }
      .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
      .ToArray();

    // The floor is 10 against 13 files today. ⚠ It is deliberately BELOW the count rather than at it: its
    // job is to catch the FILTER COLLAPSING — a renamed namespace directory returning nothing — not to
    // pin the file count, and a floor set at the current number turns every legitimate file removal into
    // a red with a misleading message. **A first attempt guessed 15 and failed on a correct tree**, which
    // is the same defect one level down: a floor asserted from expectation rather than from the population.
    Assert.True(files.Length >= 10,
      $"only {files.Length} commercial files were scanned; the walk has stopped matching and every " +
      "absence asserted over it would mean nothing.");

    return files;
  }

  // ---- ⚠ A FOURTH PRIVATE COPY OF THESE TWO HELPERS, ADDED KNOWINGLY RATHER THAN SILENTLY.
  //
  // `AdminTransportArchitectureTests`, `AuthenticationMilestoneArchitectureTests` and
  // `AuthenticationSessionArchitectureTests` each carry their own. **Extracting them is a separate task —
  // it touches four green guards to change nothing observable**, which is the kind of edit the
  // `PlatformRouteInventory` header already stops for the same reason. *Recorded here so the count is
  // visible to whoever does extract them; four copies is the number, not "a few".*
  private static string FindRepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln"))) return directory.FullName;
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }

  // ⚠ THE BAN READS CODE, NOT PROSE. A commercial file explaining WHY it must not touch `TenantStatus`
  // would otherwise fail the rule for documenting the rule — the false red this suite has met before.
  private static string CodeOnly(string path) =>
    string.Join(
      "\n",
      File.ReadAllText(path).Split('\n').Select(line =>
      {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
      }));

  [Fact]
  public void No_commercial_type_is_tenant_owned()
  {
    var owned = CommercialTypes
      .Where(type => typeof(ITenantOwnedEntity).IsAssignableFrom(type))
      .Select(type => type.Name)
      .ToList();

    Assert.True(
      owned.Count == 0,
      "A commercial type marked ITenantOwnedEntity would be swept into the Shared→Dedicated copy manifest " +
      $"automatically, because TenantCutoverCopyPlan.Build reflects over that interface: {string.Join(", ", owned)}. " +
      "DEC-SUB-0011 places the commercial plane outside cutover, and DEC-SUB-0002 makes the tenant the " +
      "subject of the agreement rather than its owner.");
  }

  // ---- AND NONE OF THEM IS IN THE TENANT ASSEMBLY'S REACH.
  //
  // A second, independent handle on the same property. The first would pass if someone kept the interface
  // off but registered the type on `TenantDbContext` anyway; this one asserts the types live where the
  // Platform context can see them and the tenant model cannot claim them by accident.
  [Fact]
  public void Every_commercial_type_lives_in_the_platform_domain_assembly()
  {
    var platformDomain = typeof(SubscriptionPlan).Assembly;

    Assert.All(CommercialTypes, type => Assert.Equal(platformDomain, type.Assembly));
    Assert.Equal("SSAS.Platform.Domain", platformDomain.GetName().Name);
  }

  // ---- THE APPEND-ONLY PAIR CARRIES NO CONCURRENCY STATE, BECAUSE IT IS NEVER UPDATED.
  //
  // `EmployeePositionAssignment` established the reasoning: "a record that is never updated has no
  // concurrency state to protect". A `RowVersion` on an append-only type is not merely redundant — it is an
  // invitation to the update `PreventAppendOnlyMutation` refuses, and it would make the type look editable
  // to anyone reading the model rather than the guard.
  [Fact]
  [Trait("Criterion", "AC-SUB-0003")]
  // `AC-SUB-0003`'s OTHER HALF, pasted whole — *"An attempt to update or delete a subscription record is
  // **refused**, not silently ignored, and the refusal comes from the persistence guard rather than from a
  // handler that remembered to check"*
  //
  // ⚠ The first version of this line quoted as far as *"is refused…"* and stopped. **Caught by the sweep,
  // in the same commit that quoted this criterion IN FULL twelve lines away in another file** — so the
  // failure is not ignorance of the rule or of the text, it is that an ellipsis is what you reach for when
  // the rest of the sentence is not the part you are talking about.
  // **The subject is the SUBSCRIPTION RECORD, and this is the only test that says the record is inside the
  // guard's reach.** `PlatformAppendOnlyGuardTests` proves the guard refuses `IAppendOnlyEntity`, using a
  // test-only probe — **so strip `IAppendOnlyEntity` from `TenantSubscription` and all four of those tests
  // stay green while subscription records become mutable.** Measured: this test is the only one in seven
  // suites that reddens.
  //
  // ⚠ *A guard and the marker that admits a type to it are two claims, and a test of either alone reads as
  // a test of both.*
  public void An_append_only_commercial_record_declares_no_rowversion_and_no_modified_columns()
  {
    Type[] appendOnly = [typeof(TenantSubscription), typeof(TenantEntitlementGrant)];

    foreach (var type in appendOnly)
    {
      Assert.True(
        typeof(IAppendOnlyEntity).IsAssignableFrom(type),
        $"{type.Name} must be IAppendOnlyEntity — OD-SUB-0008 ruled the history append-only, and " +
        "PlatformDbContext.PreventAppendOnlyMutation is what makes that real rather than decorative.");

      foreach (var forbidden in new[] { "RowVersion", "ModifiedUtc", "ModifiedBy" })
      {
        Assert.True(
          type.GetProperty(forbidden, BindingFlags.Instance | BindingFlags.Public) is null,
          $"{type.Name} declares {forbidden}, which an append-only record must not have: the row is never " +
          "updated, so there is no concurrency state to protect and no modification to record.");
      }
    }
  }

  // ---- `ADR-029`: NO SSAS TYPE MAY BE CAPABLE OF HOLDING CARDHOLDER DATA.
  //
  // Cheap to assert on this slice, so asserted rather than reported as unchecked. The ADR's decision 4 is
  // about capability, not intent — a property that *could* hold a PAN is the finding, whatever it is named
  // for. This covers the seven types shipped here; it is **not** a repository-wide guard, and `BR-SUB-0020`
  // binds the whole product.
  [Fact]
  public void No_commercial_type_declares_a_property_capable_of_holding_cardholder_data()
  {
    string[] forbidden =
      ["pan", "cardnumber", "primaryaccountnumber", "cvv", "cvc", "cardholder", "expiry", "expirationdate"];

    var offenders = CommercialTypes
      .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(property => (Type: type, property.Name)))
      .Where(entry => forbidden.Any(term =>
        entry.Name.Replace("_", string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)))
      .Select(entry => $"{entry.Type.Name}.{entry.Name}")
      .ToList();

    Assert.True(
      offenders.Count == 0,
      "ADR-029 decision 4: no SSAS type may be CAPABLE of holding a primary account number, card " +
      $"verification value, cardholder name or expiry date. Offenders: {string.Join(", ", offenders)}.");
  }
}
