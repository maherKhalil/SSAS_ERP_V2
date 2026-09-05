using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Infrastructure.Persistence.Seeding;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.Subscriptions;

// THE TWO INVARIANTS THAT MAKE THE COMMERCIAL MODEL SAFE (FP-014, T-035).
//
// Monotonic append and additive-only grants. Neither is expressible as a database constraint — the first
// spans rows, the second spans two aggregates and varies with time — so both are domain rules, and a domain
// rule with no test is a comment.
//
// ==================================================================================================
// ⚠⚠⚠ WHERE THE `SUB` CITATION PASS STOPPED, AND WHY IT IS A FINDING RATHER THAN A PLACE IT RAN OUT.
// ==================================================================================================
//
// ⚠⚠⚠ ***CORRECTED. THIS NOTE SAID "TEN CONSECUTIVE CRITERIA" AND THE ARGUMENT BELOW IS TRUE OF FIVE.***
//
// It read: *"`AC-SUB-0034` through `AC-SUB-0043` — ten consecutive criteria — describe a product that does
// not exist yet. Reading them one at a time would produce ten restatements of one fact, so the fact is
// recorded once, here, and the pass stopped."* **The economy was the point and the range was wrong.**
//
// ***THREE OF THE TEN ARE NOT WHAT IT CLAIMS, AND ONE OF THEM WAS ALREADY WRONG THE DAY THIS WAS WRITTEN:***
//
//   `AC-SUB-0037`   ***CITED TODAY.*** A criterion this note says describes a non-existent product has a
//                   witness. *The run was over-extended before anything changed in the tree.*
//   `AC-SUB-0036`   ***UNBUILT FOR A DIFFERENT REASON.*** *"Assigning a plan to a tenant whose billing
//                   currency has no price row is refused"* — **`SubscriptionPlanPrices` is one of the seven
//                   tables**; the price row exists. What is absent is the ASSIGNMENT PATH: this module's
//                   whole application layer is four files and holds no assign-plan or change-plan command.
//                   *Still unbuilt; the table-set argument is not why.*
//   `AC-SUB-0043`   ***BUILT.*** `TenantEntitlementSnapshot` carries `IsModuleEnabledAt` and `LimitAt`,
//                   which is the criterion exactly. **Cited below.**
//
// ⚠⚠ SO THE ARGUMENT BELOW COVERS `0038`–`0042` AND NOTHING ELSE, AND THAT IS THE RANGE A READER SHOULD
// TAKE FROM IT. **A collective predicate is a set and distributes over its subjects with holes** — and
// this one had a property the misdescriptions we have catalogued do not: ***it was the recorded REASON
// nine criteria were never examined, so it suppressed the reads that would have found its own holes.***
//
// ⚠ AND THE DECAY MODE MATTERS FOR THE REMEDY. A tripwire answers *the world changed and this became
// false*. **It cannot answer *this was wider than its evidence when written***, which is what happened
// here — only re-derivation finds that, and re-running the ARGUMENT is what found it rather than
// re-reading the criteria.
//
//   *Reading and disclosure* (`0034`, `0035`) — a platform caller with `Platform.Subscriptions.View`
//   reading across tenants, and a tenant caller refused from every commercial read route. **No such
//   permission name exists on either plane** (`AC-SUB-0008` says so itself, over all 28 platform names),
//   and there are no commercial read routes to be refused from.
//
//   *The commercial record* (`0038`–`0042`, corrected from `0036`–`0043`) — invoice immutability and
//   number reuse, one line per
//   subscription record in a billed period, seat usage stamped with the record in force, overage judged
//   against the plan in force then, mid-term proration. **There is no invoice, invoice line, seat usage
//   sample, payment attempt or proration anywhere in `src/`.**
//
// ⚠ ESTABLISHED BY MECHANISM, NOT BY NAME, BECAUSE EVERY ONE OF THOSE CRITERIA NEEDS A PERSISTED RECORD.
// `20260826031515_AddSubscriptionCommercialPlane` — the migration that builds this plane — creates exactly
// **seven tables**: `ModuleDefinitions`, `SubscriptionPlans`, `TenantEntitlementGrants`,
// `SubscriptionPlanLimits`, `SubscriptionPlanModules`, `SubscriptionPlanPrices`, `TenantSubscriptions`.
// **Nothing billing-shaped is among them, and no later migration adds one.**
//
// ⚠⚠ THE HONEST READING IS THE UNALARMING ONE AND IT IS ALSO THE POINT. This is a package built in
// dependency order — entitlement resolution before billing — and **an unbuilt feature is not a defect.**
// What is worth recording is that **these ten read exactly like the twelve that ARE built**: same table,
// same voice, same specificity about boundary cases. *Nothing in the criteria document distinguishes a
// criterion describing shipped behaviour from one describing intended behaviour*, which is the same
// property that made `AC-SUB-0019`'s silence persuasive — **a document that declares some of its gaps and
// not others teaches a reader to trust the ones it does not mention.**
//
// The `SUB` pass therefore covers `AC-SUB-0002` through `AC-SUB-0032` and stops there deliberately —
// **plus `AC-SUB-0043`, cited below, which this note wrongly placed outside it.**
public sealed class SubscriptionInvariantTests
{
  private static readonly DateTimeOffset Noon = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
  private static readonly Guid Tenant = Guid.NewGuid();
  private static readonly Guid Plan = Guid.NewGuid();

  private static SubscriptionTerm Perpetual => SubscriptionTerm.Perpetual(Noon);

  // ================================================================================================
  // ⚠⚠⚠ THE TRIPWIRE UNDER THE DISPOSITION ABOVE (AC-SUB-0038 to AC-SUB-0042).
  // ================================================================================================
  //
  // **Five criteria are recorded above as unbuilt on one piece of evidence: *there is no invoice, invoice
  // line, seat usage sample, payment attempt or proration anywhere in `src/`.* That evidence is a fact
  // about the schema, and it will stop being true.**
  //
  // ***UNBUILT-BY-ENUMERATION DECAYS IN SILENCE. UNBUILT-BY-ENFORCEMENT ANNOUNCES ITSELF.*** The day a
  // billing table ships, five dispositions become false and nothing would otherwise say so — the note
  // above would still read as current, and a reader would still take it as the reason not to look.
  //
  // ---- ⚠ THIS FIRES ON LEGITIMATE WORK, AND THE QUESTION IT ASKS IS THE POINT.
  //
  // **A new platform table is ordinary. When this goes red the question is NOT *"is this table fine?"* —
  // it is *"do `AC-SUB-0038` through `AC-SUB-0042` now have a subject, and is the disposition above still
  // true?"*** *Answer that, update the list, and if the answer is yes, the five criteria return to the
  // queue.* **The list is data, not a rule; the sentence above it is the rule.**
  //
  // ---- ⚠⚠ TWO ASSERTIONS AT DIFFERENT GRAIN, AND THE SECOND IS THE WEAK ONE.
  //
  // The **exact set** catches ANY new table, including one nobody would call billing-shaped. *It is the
  // one that cannot be evaded by naming.* The **name ban** below it catches the shapes the disposition
  // actually names, and exists so that a table called `Invoices` fails with a message about these five
  // criteria rather than as an anonymous inventory diff. ***A ban names only the shapes its author thought
  // of — `Charges`, `Statements` or `BillingRuns` would pass it — which is why it is second and why the
  // exact set is the assertion that carries the tripwire.***
  //
  // ⚠⚠⚠ AND A TRIPWIRE CANNOT ANSWER THE OTHER DECAY MODE. This guards *the world changed*. It does
  // nothing about *the claim was wider than its evidence when written* — which is what the correction at
  // the top of this file was: `0037` cited, `0036` unbuilt for another reason, `0043` built, none of which
  // a schema guard would ever have caught. **Re-derivation is the only remedy for that one, and this
  // tripwire must not be read as making it unnecessary.**
  //
  // ⚠⚠⚠ AND THE TRAITS BELOW ARE DELIBERATELY *NOT* `Criterion`, WHICH I LEARNED BY GETTING IT WRONG.
  //
  // I first tagged this `[Trait("Criterion", "AC-SUB-0038")]` and so on for all five. ***THE FEATURE COUNT
  // IMMEDIATELY REPORTED THEM AS CITED — 29 to 34 — AND FIVE UNBUILT CRITERIA READ AS COVERED.***
  //
  // **A tripwire asserting that a criterion's subject DOES NOT EXIST is the exact opposite of a witness for
  // it.** *The trait key is what a counting instrument reads, and `Criterion`, `Decision`, `Acceptance` and
  // `AcceptanceCriteria` are all read the same way* — so a guard about a disposition has to sit outside
  // that vocabulary or it silently converts a refusal into a citation. **`Tripwire` is read by nothing and
  // says what this is.**
  [Fact]
  [Trait("Tripwire", "AC-SUB-0038")]
  [Trait("Tripwire", "AC-SUB-0039")]
  [Trait("Tripwire", "AC-SUB-0040")]
  [Trait("Tripwire", "AC-SUB-0041")]
  [Trait("Tripwire", "AC-SUB-0042")]
  public void No_billing_table_exists_yet_and_five_dispositions_depend_on_that()
  {
    var created = PlatformTablesCreatedByMigrations();

    // KNOWN-POSITIVE FROM INSIDE THE ARTEFACT: the commercial plane's own tables are present, so a walk
    // that read nothing — a moved folder, a renamed migration — fails here rather than passing empty.
    Assert.Contains("TenantSubscriptions", created);
    Assert.Contains("SubscriptionPlanPrices", created);

    Assert.Equal(
      [
        "AccountActionTokens", "AuthenticationAccounts", "AuthenticationSessions", "Companies",
        "Identities", "LocalizationCatalogStates", "ModuleDefinitions", "PlatformAuthenticationSessions",
        "PlatformPermissionAssignments", "PlatformRefreshTokenRecords", "PlatformSupportPrincipals",
        "RefreshTokenRecords", "RolePermissionAssignments", "Roles", "SubscriptionPlanLimits",
        "SubscriptionPlanModules", "SubscriptionPlanPrices", "SubscriptionPlans",
        "TenantCutoverOperations", "TenantDatabaseAssignments", "TenantDatabaseBackupPolicies",
        "TenantDatabaseBackupRuns", "TenantDatabaseRestoreVerificationRuns", "TenantDatabases",
        "TenantEntitlementGrants", "TenantLocalizationOverrideVersions", "TenantLocalizationOverrides",
        "TenantLocalizationSettings", "TenantSelectionTransactions", "TenantSubscriptions",
        "TenantUserRoleAssignments", "TenantUsers", "Tenants", "UserBranchAccess", "UserCompanyAccess",
        "UserEmployeeLink"
      ],
      created);

    // ---- THE WEAK HALF, KEPT FOR ITS MESSAGE RATHER THAN ITS REACH.
    Assert.DoesNotContain(created, name =>
      name.Contains("Invoice", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Payment", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Usage", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Overage", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("Proration", StringComparison.OrdinalIgnoreCase));
  }

  // Every table any platform migration CREATES. Designer and snapshot files are excluded: they restate the
  // model rather than declaring an operation, so counting them would double every table.
  private static string[] PlatformTablesCreatedByMigrations()
  {
    var directory = Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure",
      "Persistence", "Migrations");

    var names = new SortedSet<string>(StringComparer.Ordinal);

    foreach (var file in Directory.EnumerateFiles(directory, "*.cs")
      .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
      .Where(path => !path.Contains("Snapshot", StringComparison.Ordinal)))
    {
      var source = File.ReadAllText(file);

      for (var i = source.IndexOf("migrationBuilder.CreateTable(", StringComparison.Ordinal); i >= 0;
        i = source.IndexOf("migrationBuilder.CreateTable(", i + 1, StringComparison.Ordinal))
      {
        var marker = source.IndexOf("name: \"", i, StringComparison.Ordinal);
        if (marker < 0)
        {
          continue;
        }

        var start = marker + "name: \"".Length;
        names.Add(source[start..source.IndexOf('"', start)]);
      }
    }

    return [.. names];
  }


  // ---- ⚠⚠⚠ ONE RESOLUTION ANSWERS BOTH QUESTIONS (AC-SUB-0043).
  //
  // *"The resolved cap for a limit key is available at the enforcement point in the same call that
  // resolves module entitlement — one resolution, not two."*
  //
  // **The note at the top of this file placed this criterion among ten describing a product that does not
  // exist. It does exist**, and it is the only one of that range that does — `TenantEntitlementSnapshot`
  // carries `IsModuleEnabledAt` and `LimitAt`, and `ITenantEntitlementReader` hands back one snapshot from
  // one call. *The record was wrong about the product, in our own hand.*
  //
  // ⚠⚠⚠ WHAT THIS ADDS IS THE CONJUNCTION, NOT EITHER HALF — AND A PLANT IS HOW I LEARNED THAT.
  //
  // Reddening `LimitAt` failed TWO tests: this one and
  // `TenantEntitlementSnapshotTests.A_grant_above_the_plan_cap_raises_it`, **which I did not know existed.**
  // *That file covers cap resolution and module entitlement thoroughly — grants raising a cap, an
  // undefined cap answering null, an expired term resolving none — and carries **zero criterion traits**.*
  //
  // ***SO THE TWO HALVES WERE ALREADY PROVEN SEPARATELY. THE CRITERION IS ABOUT THEM NOT BEING SEPARATE.***
  // *"...in the same call that resolves module entitlement — one resolution, not two"* is violated by a
  // product where both answers are correct and arrive from two lookups. **Each half working is what those
  // tests establish; that one object carries both, and that no second path exists, is what this does.**
  //
  // ⚠ The two numbers still differ deliberately — the plan grants **100** seats and a `LimitRaise` lifts
  // it to **250** — so the conjunction is asserted over a RESOLVED cap rather than a passthrough. *That is
  // borrowed rigour from the file above rather than new rigour here, and it is worth having on the object
  // an enforcement point actually holds.*
  //
  // ⚠⚠ AND "ONE RESOLUTION, NOT TWO" IS THE STRUCTURAL HALF, ASSERTED ON THE READER. A second
  // resolution path would be a second method — a `ReadLimitsAsync` beside `ReadAsync` — and the exact
  // member set is what notices one being added. **The behavioural half shows both answers coming from one
  // object; this shows there is nowhere else they could come from.**
  [Fact]
  [Trait("Criterion", "AC-SUB-0043")]
  public void One_snapshot_answers_module_entitlement_and_the_resolved_cap()
  {
    var snapshot = new TenantEntitlementSnapshot(
      Tenant, Plan, Perpetual,
      new HashSet<string>(StringComparer.Ordinal) { "HR" },
      new Dictionary<string, long>(StringComparer.Ordinal) { ["Seats"] = 100 },
      [new EntitlementGrantFact(
        EntitlementGrantKind.LimitRaise, null, "Seats", 250, Noon.AddDays(-1), null)]);

    // ---- BOTH ANSWERS, FROM THE ONE OBJECT AN ENFORCEMENT POINT HOLDS.
    Assert.True(snapshot.IsModuleEnabledAt("HR", Noon));
    Assert.Equal(250, snapshot.LimitAt("Seats", Noon));

    // ---- AND THE CAP WAS RESOLVED RATHER THAN READ. The plan says 100; the grant raises it.
    Assert.NotEqual(100, snapshot.LimitAt("Seats", Noon));

    // ---- THERE IS NO SECOND RESOLUTION PATH.
    Assert.Equal(
      ["ReadAsync"],
      typeof(ITenantEntitlementReader).GetMethods().Select(method => method.Name));
  }

  private static Result<TenantSubscription> Append(
    DateTimeOffset effectiveFrom, DateTimeOffset? currentMaximum) =>
    TenantSubscription.Append(
      Tenant, Plan, effectiveFrom, currentMaximum, Perpetual, "USD", "operator", null, null, Noon);

  // ==================================================================================================
  // MONOTONIC APPEND.
  // ==================================================================================================

  [Fact]
  [Trait("Criterion", "AC-SUB-0052")]
  [Trait("Criterion", "AC-SUB-0054")]
  // ==================================================================================================
  // `AC-SUB-0052`, pasted — *"Every tenant existing when the seed migration runs holds the **all-module
  // plan on a `Fixed` 14-day term**. **No status filter** — suspended and archived tenants are seeded like
  // any other, because `OD-SUB-0010` made subscription state and `TenantStatus` orthogonal and a filter
  // here is that coupling. **No history is reconstructed**: `EffectiveFromUtc` is the instant the seed ran,
  // never the tenant's creation date"*
  // ==================================================================================================
  //
  // ⚠⚠ THE SUBJECT IS A **SQL STRING**, WHICH IS WHY THIS IS EXEC-SCOPE AT ALL. `TrialSubscriptionSeed.Sql`
  // is a constant in the Infrastructure assembly; the migration merely hands it to `migrationBuilder.Sql`.
  // **So the seed's content is readable without a database**, and the two clauses that are about its
  // CONTENT — no status filter, and the instant it uses — are assertable here rather than only against
  // SQL Server.
  //
  // ⚠ What is NOT assertable here is the EFFECT: that every tenant ends up holding the plan. That needs
  // rows, and `TrialSubscriptionSeedSqlServerTests` is where they are. **Named rather than counted:
  // this test carries the two content clauses and neither of the outcome ones.**
  //
  // ⚠⚠⚠ THE STATUS ASSERTION IS SCOPED TO THE TENANT SELECT, AND THAT SCOPING IS THE WHOLE TEST. The seed
  // writes `[Status]` into the PLANS insert, legitimately — **a blanket ban on the word would be a false
  // red on a correct seed**, which is the failure mode that gets a guard deleted. So the assertion reads
  // only the statement that selects tenants, from `FROM [platform].[Tenants]` onward.
  //
  // `AC-SUB-0054`'s SQL SIDE: the same statement carries `WHERE NOT EXISTS (… TenantSubscriptions …)`,
  // which is the *already holds **any** subscription record* guard — **the same rule the C# issuer applies
  // in `TrialSubscriptionIssuer`, asserted here on the OTHER writer.** Two paths, one rule; a divergence
  // between them is what `DEC-L-034` forbids and what neither test alone would catch.
  public void The_trial_seed_selects_every_tenant_regardless_of_status_and_stamps_the_seed_instant()
  {
    var sql = TrialSubscriptionSeed.Sql;
    Assert.True(sql.Length > 1000, $"the seed SQL is {sql.Length} characters; it has not been loaded.");

    // The floor's companion: the statement this test is about must actually be present, or every
    // assertion below reads an empty string and passes.
    var tenantSelect = sql[sql.IndexOf("FROM [platform].[Tenants]", StringComparison.Ordinal)..];
    Assert.True(tenantSelect.Length > 100, "the tenant-selection statement was not found in the seed SQL.");

    // ---- NO STATUS FILTER, scoped to the statement that chooses tenants.
    Assert.DoesNotContain("Status", tenantSelect, StringComparison.OrdinalIgnoreCase);

    // ---- AND THE CONTROL FOR THAT SCOPING: the seed DOES write a status elsewhere, so an assertion that
    // ---- passed over the whole string would be passing for the wrong reason.
    Assert.Contains("[Status]", sql, StringComparison.Ordinal);

    // ---- THE INSTANT IS THE SEED'S OWN, NOT A TENANT COLUMN.
    Assert.Contains("TODATETIMEOFFSET(SYSUTCDATETIME(), 0)", sql, StringComparison.Ordinal);
    Assert.DoesNotContain("tenant.[CreatedUtc]", sql, StringComparison.Ordinal);

    // ---- `AC-SUB-0054` ON THE SQL WRITER: any existing record, not any existing TRIAL.
    Assert.Contains("NOT EXISTS", tenantSelect, StringComparison.Ordinal);
    Assert.Contains("TenantSubscriptions", tenantSelect, StringComparison.Ordinal);
    Assert.DoesNotContain("SubscriptionPlanId] = @planId", tenantSelect, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Criterion", "AC-SUB-0046")]
  // ==================================================================================================
  // `AC-SUB-0046`, pasted — *"The migration that creates these tables **inserts no subscription row and
  // seeds no plan.** Immediately after it runs every existing tenant is unentitled, which is correct under
  // `CON-0001` and is exactly why `AC-SUB-0047` exists"*
  // ==================================================================================================
  //
  // ⚠⚠⚠ THE PRODUCT ALREADY ENFORCES THIS AT MIGRATION TIME, AND THAT IS THE INTERESTING PART. The
  // table-creating migration ends by COUNTING plans, subscriptions and grants and **throwing 51014 if any
  // is non-zero** — *"this migration must create the commercial plane EMPTY"*. So the criterion is not
  // merely true; it is self-enforcing on every database the migration touches.
  //
  // ⚠⚠ WHICH MEANS THERE ARE TWO SEPARABLE CLAIMS AND THIS TEST MAKES BOTH, BECAUSE THEY FAIL
  // DIFFERENTLY:
  //
  //   THE MECHANISM   the migration contains no insert of a plan or subscription. **This is what makes the
  //                   criterion true.** Delete the guard below and it stays true.
  //   THE SAFETY NET  the emptiness assertion is present. **This is what makes it STAY true** when someone
  //                   adds a convenient backfill — and deleting it is invisible to the mechanism claim.
  //
  // *A test asserting only the mechanism would go green on the day the net was removed, and a test
  // asserting only the net would go green on the day an insert was added beside it.*
  public void The_commercial_plane_migration_inserts_nothing_and_asserts_its_own_emptiness()
  {
    var migration = File.ReadAllText(Path.Combine(
      RepositoryRoot(), "src", "Platform", "SSAS.Platform.Infrastructure", "Persistence", "Migrations",
      "20260826031515_AddSubscriptionCommercialPlane.cs"));
    Assert.True(migration.Length > 5000, $"the migration is {migration.Length} characters; it was not read.");
    // The walk found A file; this proves it found THE file, so the absences below are about the migration
    // that creates these tables rather than about whatever else that path might one day hold.
    Assert.Contains("CreateTable(", migration, StringComparison.Ordinal);
    Assert.Contains("TenantSubscriptions", migration, StringComparison.Ordinal);

    // ---- THE MECHANISM.
    Assert.DoesNotContain("InsertData(", migration, StringComparison.Ordinal);
    Assert.DoesNotContain("INSERT INTO [platform].[SubscriptionPlans]", migration, StringComparison.Ordinal);
    Assert.DoesNotContain("INSERT INTO [platform].[TenantSubscriptions]", migration, StringComparison.Ordinal);

    // ---- THE SAFETY NET.
    Assert.Contains("THROW 51014", migration, StringComparison.Ordinal);
    Assert.Contains("must create the commercial plane EMPTY", migration, StringComparison.Ordinal);
  }

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln"))) return directory.FullName;
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }

  [Fact]
  public void The_first_record_for_a_tenant_appends_with_no_current_maximum()
  {
    var result = Append(Noon, currentMaximum: null);

    Assert.True(result.IsSuccess);
    Assert.Equal(Noon, result.Value.EffectiveFromUtc);
  }

  [Fact]
  public void An_append_strictly_after_the_current_maximum_is_accepted()
  {
    var result = Append(Noon.AddTicks(1), currentMaximum: Noon);

    Assert.True(result.IsSuccess);
  }

  // ---- THE TWO REFUSALS, AND WHY *EQUAL* IS AS WRONG AS *BEHIND*.
  //
  // Two records at the same instant make "the greatest `EffectiveFromUtc <= T`" ambiguous — the derived
  // invariant "exactly one in force" stops being derivable, and which plan a tenant holds depends on row
  // order. That is why the rule is strictly-greater rather than not-less-than.
  // ⚠ CITES `AC-SUB-0004`'s FIRST CLAUSE — *"Appending a record whose `EffectiveFromUtc` is EQUAL TO OR
  // EARLIER THAN the tenant's current maximum is refused."* The three rows are that clause exactly: `0` is
  // *equal to*, `-1` and `-864000000000` are *earlier than* at two magnitudes.
  //
  // ⚠⚠ THE `0` ROW IS THE ONE THE CRITERION EXISTS FOR AND THE ONE AN IMPLEMENTER WOULD OMIT. A
  // not-less-than rule passes the two negative rows and fails only this one — and the comment above already
  // says why: two records at the same instant make *the greatest `EffectiveFromUtc <= T`* ambiguous, so
  // *exactly one in force* stops being derivable and the answer depends on row order. **Delete the `0` row
  // and the test still reads as a monotonicity test while permitting the tie it exists to forbid.**
  //
  // ⚠⚠⚠ AND CLAUSE TWO IS NOT HERE: *"Two CONCURRENT appends produce one record and one refusal, never two
  // records."* That is a race over a shared maximum and needs real SQL; this passes `currentMaximum` as an
  // argument, so **the value it is compared against is supplied by the caller and never read under
  // contention.** Same shape as `Role.Retire` being TOLD whether a role is assigned — the domain honours
  // the input and says nothing about who computes it. Cited for clause 1 only.
  [Theory]
  [InlineData(0)]      // the same instant
  [InlineData(-1)]     // one tick behind
  [InlineData(-864000000000L)] // a day behind
  [Trait("Criterion", "AC-SUB-0004")]
  public void An_append_at_or_behind_the_current_maximum_is_refused(long offsetTicks)
  {
    var result = Append(Noon.AddTicks(offsetTicks), currentMaximum: Noon);

    Assert.True(result.IsFailure);
    Assert.Equal(SubscriptionErrors.NonMonotonicAppend, result.Error);
  }

  // The reason the rule exists, asserted as behaviour rather than left in a comment: an append behind the
  // present would change what was in force at an instant already used to judge a metered overage.
  [Fact]
  public void A_backdated_append_cannot_rewrite_what_was_in_force_at_a_past_instant()
  {
    var first = Append(Noon, currentMaximum: null).Value;
    var backdated = Append(Noon.AddDays(-30), currentMaximum: Noon);

    Assert.True(backdated.IsFailure);

    // Fifteen days ago nothing was in force, and it still is nothing — because the record that would have
    // retroactively covered that instant was never created. Had the append succeeded, this same query would
    // now answer with a plan the tenant did not hold at the time, and any overage judged then would change
    // verdict.
    Assert.Null(TenantEntitlement.InForceAt([first], Noon.AddDays(-15)));
    Assert.Equal(first, TenantEntitlement.InForceAt([first], Noon));
  }

  // ==================================================================================================
  // ADDITIVE GRANTS — THE WRITE-TIME REFUSAL.
  // ==================================================================================================

  // ⚠ CITES `AC-SUB-0016` — *"A grant whose `LimitValue` is AT OR BELOW the plan's current cap for that key
  // is REFUSED AT WRITE, with an error naming the plan's value."* The two rows are *below* and *at*, and
  // `A_limit_grant_above_the_plan_cap_is_accepted` below is the anti-vacuity control: without it, a
  // `RaiseLimit` that refused everything satisfies both rows.
  //
  // ⚠⚠⚠ THE CLAUSE THIS TEST DOES **NOT** CARRY IS NOT A TEST GAP. IT IS A PRODUCT GAP, AND IT IS
  // STRUCTURAL. *"…refused at write, WITH AN ERROR NAMING THE PLAN'S VALUE."*
  //
  // `TenantEntitlementGrant.RaiseLimit` returns `SubscriptionErrors.GrantWouldNotRaise`, which is a
  // `static readonly Error` built once at type load:
  //
  //   "An entitlement grant may only raise a limit above the plan's value; it may never lower one."
  //
  // **It names the plan's value the way a sentence names a variable — it does not CARRY it.** The instance
  // is shared by every refusal and takes no parameters, so **it cannot vary with `planLimitValue` at all**.
  // An operator submitting 100 against a cap of 100 is told the rule and not the number.
  //
  // ⚠ SO THE CLAUSE IS UNSATISFIABLE WITHOUT A `src/` CHANGE — a parameterised error, or detail added at
  // the transport boundary — **and that is a decision for the owner, not a test to write.** Recorded here
  // rather than asserted, because a test demanding `100` in the message would fail on correct-as-built code
  // and would be a proposal wearing a test's clothes.
  //
  // ⚠⚠⚠ AND *"REFUSED AT WRITE"* HAS A SECOND PROBLEM THAT SUBSUMES THE FIRST: **THERE IS NO WRITE.**
  // `TenantEntitlementGrant` has exactly two factories — `GrantModule` and `RaiseLimit` — and searching
  // `src/` for both names plus `new TenantEntitlementGrant` finds **only their own declarations and three
  // comments about them.** No command handler, no endpoint, nothing in Application or API. **The only
  // callers in the repository are four test files**, this one among them.
  //
  // The type IS persisted and IS read — `TenantEntitlementGrantConfiguration`, the migrations, and
  // `TenantEntitlementReader` are all real, so a grant row that existed would be resolved correctly.
  // **Nothing that ships can create one**; see the SQL and migration checks below for how far that goes.
  //
  // ⚠ MECHANISM, NOT NAMES: two factories and a private constructor are the complete set of ways this
  // aggregate comes into being in C#, so the search is exhaustive over source. **The name search is only
  // corroboration, and it OVER-INCLUDES as well as under-includes** — `SubscriptionPlan.GrantModule` is a
  // different method sharing the name, in `src/`, in the same namespace. The mechanism argument is what
  // carries this, not the grep.
  //
  // ⚠⚠ AND SQL WAS CHECKED SEPARATELY, BECAUSE A RAW INSERT NAMES NO C# SYMBOL. Searching `src/` for the
  // TABLE name finds create, constrain, index, map and read — **and no writer of any kind.** The trial seed
  // (`TrialSubscriptionSeed.Sql`) inserts into `SubscriptionPlans`, `SubscriptionPlanModules`,
  // `SubscriptionPlanPrices`, `ModuleDefinitions` and `TenantSubscriptions`: five tables, not this one.
  //
  // ⚠⚠⚠ AND THE MIGRATION THAT CREATES THE TABLE ASSERTS IT IS **EMPTY**.
  // `20260826031515_AddSubscriptionCommercialPlane.cs:289-304` counts plans, subscriptions and grants after
  // creating them and FAILS THE MIGRATION if any is non-zero, quoting `CON-0001` and `OD-SUB-0004` —
  // *"entitlement is recorded, never assumed."* **So the table is guaranteed empty at creation, has no
  // writer, and its only production consumer is a reader.** `AC-SUB-0017`'s *"however it came to exist"* is
  // not merely the only remaining path; the schema refuses at migration time the very row that would
  // exercise it.
  //
  // ---- WHAT THIS IS AND IS NOT, WITH THE BENIGN READING FIRST BECAUSE IT IS PROBABLY THE TRUE ONE.
  //
  // **`FP-014` is a young package under construction, and a domain and schema built ahead of the
  // application layer is ordinary rather than a defect.** Nothing here says anyone did anything wrong.
  // **The finding is about what the DOCUMENTATION CLAIMS, not about the missing handler.**
  //
  // ⚠⚠⚠ AND THE RISK IS NOT CURRENT — IT IS **ARMED**. Today the exposure is nil: no writer, and the table
  // asserted empty at creation, so the resolution guard protects a set that cannot be non-empty. **The
  // exposure arrives the day someone wires up grant creation** — and on that day the guard that catches a
  // lowering grant is documented as *the redundant half of a pair* whose loud half has never run. **A
  // reader tidying away belt-and-braces removes the only enforcement there is, and every test stays green,
  // because no grant row can exist to fail one.**
  //
  // So the shape is not *something is broken*. It is ***a correct-looking redundancy claim that is false in
  // the direction which makes removal look safe, and that becomes load-bearing later.***
  //
  // ⚠ THIS IS THE THIRD INSTANCE OF ONE CLASS AND THE CLASS IS WRITTEN UP ONCE, IN
  // `API.Tests/IdentityAccess/TenantUserRouteInventoryTests.cs` — *a criterion's subject exists, is
  // correct, and is on no executed path*. The other two are `AC-IAM-0001`'s user listing
  // (defended-but-unwitnessed) and three unrouted tenant-user handlers (unrouted-and-untested). **This one
  // is the worst of the three precisely because it is the least broken**: the other two announce their
  // gaps, and this one is described in its own source as safe by redundancy.
  //
  // ⚠⚠ THAT INVERTS THE BELT-AND-BRACES READING. `SubscriptionErrors.cs` calls the write refusal *"the loud
  // half"* of a deliberate pair. **The loud half is unreachable in production, so the resolution-side
  // `max(plan, grants)` is not redundancy — it is the whole enforcement**, and `AC-SUB-0017`'s tests are
  // carrying a load their own criterion describes as secondary.
  //
  // ⚠⚠ AND THE ERROR'S OWN DECLARATION CONFIRMS `AC-SUB-0017`'s READING: *"Resolution ALSO takes
  // `max(plan, grants)`, so a grant that somehow named a lower value could not lower anything — the two are
  // deliberate belt and braces, and this error is the loud half."* **The redundant enforcement is stated in
  // the source as well as split across two criteria**, which is the opposite of the undocumented double
  // guard found earlier tonight in the localization batch validator.
  //
  // ⚠⚠⚠ AND `AC-SUB-0017` IS A SEPARATE CRITERION, DELIBERATELY, WHICH THIS FILE'S OWN HEADING ANTICIPATES
  // — *ADDITIVE GRANTS: THE WRITE-TIME REFUSAL*. `0017` says the resolved cap is `max(plan, grants)` and
  // that a lower grant row **"however it came to exist"** does not lower it. **That phrase is the criterion
  // authors saying the write guard may be bypassed** — by a migration, a seed, a direct write — so the
  // resolution side must hold independently. **It is not cited here and this test cannot carry it**:
  // nothing below resolves a cap. `TenantEntitlementResolutionTests` is where that half must live, and a
  // reader who takes this citation as covering *additive grants* would have the write half and none of the
  // defence behind it.
  [Theory]
  [InlineData(50)]   // below the plan's cap
  [InlineData(100)]  // equal to it — a no-op the caller would believe did something
  [Trait("Criterion", "AC-SUB-0016")]
  public void A_limit_grant_at_or_below_the_plan_cap_is_refused(long grantValue)
  {
    var result = TenantEntitlementGrant.RaiseLimit(
      Tenant, PlanLimit.Seats, grantValue, planLimitValue: 100, Noon, null, "operator", null, null, Noon);

    Assert.True(result.IsFailure);
    Assert.Equal(SubscriptionErrors.GrantWouldNotRaise, result.Error);
  }

  [Fact]
  public void A_limit_grant_above_the_plan_cap_is_accepted()
  {
    var result = TenantEntitlementGrant.RaiseLimit(
      Tenant, PlanLimit.Seats, 250, planLimitValue: 100, Noon, null, "operator", null, null, Noon);

    Assert.True(result.IsSuccess);
    Assert.Equal(EntitlementGrantKind.LimitRaise, result.Value.GrantKind);
    Assert.Equal(250, result.Value.LimitValue);
  }

  // A plan that carries no such limit has nothing to exceed, so establishing one is additive. Distinguished
  // from a plan cap of zero, which is a real cap a grant must exceed.
  [Fact]
  public void A_limit_grant_is_accepted_when_the_plan_carries_no_such_limit()
  {
    var result = TenantEntitlementGrant.RaiseLimit(
      Tenant, PlanLimit.Seats, 1, planLimitValue: null, Noon, null, "operator", null, null, Noon);

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public void A_limit_grant_of_zero_against_a_plan_cap_of_zero_is_refused()
  {
    var result = TenantEntitlementGrant.RaiseLimit(
      Tenant, PlanLimit.Seats, 0, planLimitValue: 0, Noon, null, "operator", null, null, Noon);

    Assert.True(result.IsFailure);
    Assert.Equal(SubscriptionErrors.GrantWouldNotRaise, result.Error);
  }

  // ==================================================================================================
  // THE TERM, AND THE EXPLICIT PERPETUAL MARKER.
  // ==================================================================================================

  // ⚠ CITES `AC-SUB-0027`'s FIRST CLAUSE — *"A `Fixed` term requires an end AFTER its start."* The guard is
  // `endUtc <= startUtc`, so `A_fixed_term_ending_AT_its_start` is the boundary row and the one that
  // separates *after* from *not before*; the other is the ordinary case.
  [Fact]
  [Trait("Criterion", "AC-SUB-0027")]
  public void A_fixed_term_ending_before_it_starts_is_refused() =>
    Assert.True(SubscriptionTerm.Fixed(Noon, Noon.AddDays(-1)).IsFailure);

  [Fact]
  [Trait("Criterion", "AC-SUB-0027")]
  public void A_fixed_term_ending_at_its_start_is_refused() =>
    Assert.True(SubscriptionTerm.Fixed(Noon, Noon).IsFailure);

  [Fact]
  public void A_perpetual_term_never_expires()
  {
    var term = SubscriptionTerm.Perpetual(Noon);

    Assert.Equal(SubscriptionTermKind.Perpetual, term.Kind);
    Assert.Null(term.EndUtc);
    Assert.False(term.HasExpiredAt(Noon.AddYears(50)));
  }

  [Fact]
  public void A_fixed_term_expires_after_its_end()
  {
    var term = SubscriptionTerm.Fixed(Noon, Noon.AddDays(30)).Value;

    Assert.False(term.HasExpiredAt(Noon.AddDays(30)));
    Assert.True(term.HasExpiredAt(Noon.AddDays(30).AddTicks(1)));
  }

  // ---- REHYDRATION REFUSES THE COMBINATIONS THE FACTORIES REFUSE.
  //
  // EF materialises through `Rehydrate`, so a row written before the `CHECK` existed — or by any path that
  // bypassed the domain — must not become an object the rest of the model believes is valid.
  //
  // ⚠⚠⚠ CITES `AC-SUB-0027`'s SECOND CLAUSE **WITH A QUALIFIER THAT CHANGES WHERE THE GUARANTEE LIVES** —
  // *"`Fixed` with a NULL END and `Perpetual` with AN END are both refused AT CONSTRUCTION."*
  //
  // **They are not refused at construction. They are unconstructible.** The factory signatures are
  // `Fixed(DateTimeOffset startUtc, DateTimeOffset endUtc)` — a NON-NULLABLE end — and
  // `Perpetual(DateTimeOffset startUtc)`, which takes no end at all and returns a bare `SubscriptionTerm`
  // rather than a `Result`, because **it has nothing it could fail on.** Neither bad combination can be
  // expressed through the construction path the criterion names.
  //
  // ⚠ THAT IS STRONGER THAN THE CRITERION ASKS AND IT IS NOT WHAT THE CRITERION SAYS, so the distinction is
  // recorded rather than smoothed over. **The two tests below are on `Rehydrate`, a DIFFERENT path** — the
  // one EF materialises through — and that is the only path where the states are representable at all. A
  // reader citing `AC-SUB-0027` for "refused at construction" and landing here would find a refusal on the
  // rehydration path and conclude the constructors validate. They do not; **they make the question
  // impossible to ask**, which is why no test exists for a refusal that cannot happen.
  //
  // **The guarantee is real, complete, and located one layer from where the criterion puts it.**
  [Fact]
  [Trait("Criterion", "AC-SUB-0027")]
  public void Rehydrating_a_perpetual_term_that_carries_an_end_is_refused() =>
    Assert.True(SubscriptionTerm
      .Rehydrate(SubscriptionTermKind.Perpetual, Noon, Noon.AddDays(1)).IsFailure);

  [Fact]
  [Trait("Criterion", "AC-SUB-0027")]
  public void Rehydrating_a_fixed_term_with_no_end_is_refused() =>
    Assert.True(SubscriptionTerm.Rehydrate(SubscriptionTermKind.Fixed, Noon, null).IsFailure);

  // ==================================================================================================
  // NO PLAN ATTRIBUTE IS COPIED ONTO A SUBSCRIPTION ROW (`AC-SUB-0005`).
  // ==================================================================================================
  //
  // *"A plan is referenced by many tenants; amending it changes no subscription record, and no plan
  // attribute is copied into a subscription row at assignment."*
  //
  // ---- ⚠ WHAT THE CLAUSE IS FOR, WHICH DECIDES WHAT COUNTS AS A COPY.
  //
  // **Divergence.** Copy the plan's name or price onto the row and a plan amendment stops propagating —
  // which is the failure `AC-SUB-0015` names from the other side. **A DUPLICATED SINGLE-VALUED ATTRIBUTE
  // CAN DRIFT FROM ITS SOURCE; A SELECTION FROM A SET CANNOT, because there is no source value to drift
  // from.**
  //
  // ---- ⚠⚠ `BillingCurrencyCode` IS A SELECTION, AND ITS GROUNDS ARE ASSERTED BELOW RATHER THAN CLAIMED.
  //
  // The row carries which of the plan's currencies this tenant is billed in — **a fact about the
  // subscription that exists nowhere on the plan.** That reading depends entirely on the plan being
  // genuinely multi-currency, so the test asserts it: `SubscriptionPlan.Prices` is a COLLECTION of
  // `PlanPrice`, each with its own `CurrencyCode`. ***IF A PLAN EVER BECOMES SINGLE-CURRENCY, THAT
  // ASSERTION FAILS AND THIS EXEMPTION IS WITHDRAWN AUTOMATICALLY*** — the grounds are checked, not
  // recorded, which is what stops it becoming a name in an exclusion list nobody re-examines.
  //
  // ⚠⚠⚠ AND THE HONEST WEIGHT OF THAT ASSERTION, MEASURED: **IT COULD NOT BE PLANTED.** Making `Prices`
  // single-valued does not compile — `SubscriptionPlanConfiguration` owns it as a collection and the EF
  // model refuses. **So the compiler, not this line, is what actually prevents a single-currency plan.**
  // The assertion stays because it states the dependency at the place that depends on it and costs
  // nothing; it is belt-and-braces over a guarantee the build already gives, and reporting it as the
  // guard would overstate it.
  //
  // ---- THE AUDIT NAMES ARE EXCLUDED, AND READ FROM THE INTERFACE RATHER THAN TYPED HERE.
  //
  // Both types declare `CreatedUtc` and a `ModifiedBy`-shaped actor. Those are each row's own provenance,
  // not the plan's attributes, and a name match on them is a false positive. **The exclusion is the member
  // list of `IAuditableEntity` plus `RowVersion`, read reflectively**, so it cannot drift from the
  // interface and is not a hand-written list of convenient names.
  [Fact]
  [Trait("Criterion", "AC-SUB-0005")]
  public void No_single_valued_plan_attribute_is_duplicated_onto_a_subscription_record()
  {
    var provenance = typeof(IAuditableEntity).GetProperties()
      .Select(property => property.Name)
      .Append("RowVersion")
      .Append("ChangedBy")
      .ToHashSet(StringComparer.Ordinal);

    // THE GROUNDS FOR THE ONE SELECTION, ASSERTED FIRST. A plan offers many currencies, so holding one is
    // a choice and not a duplicate. If this stops being true the exemption below stops with it.
    var prices = typeof(SubscriptionPlan).GetProperty(nameof(SubscriptionPlan.Prices));
    Assert.NotNull(prices);
    Assert.True(
      typeof(System.Collections.IEnumerable).IsAssignableFrom(prices!.PropertyType)
        && prices.PropertyType != typeof(string),
      "SubscriptionPlan.Prices is no longer a collection, so a plan may be single-currency and " +
      "TenantSubscription.BillingCurrencyCode would be a COPY rather than a selection. AC-SUB-0005's " +
      "reading depends on this.");

    // ⚠ THE IDENTIFIER IS NOT AN ATTRIBUTE, AND THE GROUNDS ARE THE CRITERION'S OWN FIRST CLAUSE:
    // *"A plan is REFERENCED by many tenants."* `TenantSubscription.SubscriptionPlanId` is that reference —
    // **it is what makes copying unnecessary, so counting it as a copy inverts the rule.** The name is
    // derived from the type rather than written as a literal, so renaming `SubscriptionPlan` carries the
    // exclusion with it instead of silently reopening this as a false positive.
    var identifier = typeof(SubscriptionPlan).Name + "Id";

    var planAttributes = Declared(typeof(SubscriptionPlan))
      .Where(property => property.Name != identifier)
      .Where(property => !provenance.Contains(property.Name))
      .Where(property => !typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType)
        || property.PropertyType == typeof(string))
      .ToArray();

    var subscriptionProperties = Declared(typeof(TenantSubscription))
      .Where(property => !provenance.Contains(property.Name))
      .ToArray();

    // ⚠ TWO FLOORS, BECAUSE EITHER SIDE COLLAPSING MAKES THE COMPARISON VACUOUS AND GREEN. A filter that
    // matched nothing on the plan side would report no copies of nothing.
    Assert.True(planAttributes.Length >= 3,
      $"only {planAttributes.Length} single-valued plan attributes were found; the reflection has stopped " +
      "matching and no copy could be detected.");
    Assert.True(subscriptionProperties.Length >= 5,
      $"only {subscriptionProperties.Length} subscription properties were found; same problem.");

    // A copy is a NAME match — the shape the criterion forbids, and the one that drifts. Type matching is
    // deliberately not used: `Guid`, `string` and `DateTimeOffset` recur for unrelated reasons on both
    // types and would report every row as a copy of every other.
    var copies = subscriptionProperties
      .Where(subscription => planAttributes.Any(plan => plan.Name == subscription.Name))
      .Select(property => property.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(copies.Length == 0,
      "these TenantSubscription properties duplicate a single-valued SubscriptionPlan attribute, so a plan " +
      "amendment would stop propagating to subscriptions that name it and the two would silently " +
      $"diverge: {string.Join(", ", copies)}. A value CHOSEN from a plan-offered set is not a copy — if " +
      "one of these is such a selection, assert the grounds as Prices is asserted above.");
  }

  private static System.Reflection.PropertyInfo[] Declared(Type type) =>
    type.GetProperties(System.Reflection.BindingFlags.Public
      | System.Reflection.BindingFlags.Instance
      | System.Reflection.BindingFlags.DeclaredOnly);
}
