using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Permissions;
using SSAS.GL.Contracts.Posting;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Application.Reads;
using SSAS.Payroll.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// PAYROLL'S STRUCTURAL GUARDS (FP-012).
//
// Each pins a property that is invisible at the point where it would be broken. Two are unique to this
// module and carry more weight than their GL equivalents:
//
//   * **Isolation is asserted in BOTH directions.** Payroll reaches GL only through `SSAS.GL.Contracts` and
//     HR only through `SSAS.HR.Contracts` — and neither GL nor HR may learn about Payroll, because the
//     "one-directional coupling" argument in `DEC-PAY-0018` stops being true the moment they do.
//   * **The scope guards protect personal data.** Elsewhere a forgeable scope is an authorization defect;
//     for compensation it is a personal-data breach.
public sealed class PayrollArchitectureTests
{
  // ================================================================================================
  // MODULE ISOLATION, BOTH DIRECTIONS (ADR-012, DEC-PAY-0017, DEC-PAY-0018)
  // ================================================================================================

  // ---- ⚠ CITES `AC-ATT-0026` — AND THE CRITERION IS WHY ATTENDANCE IS IN THE LIST BELOW AT ALL.
  //
  // *"Payroll consumes the contract **without an assembly reference** to any Attendance implementation
  // project — asserted by an architecture test, not by inspection."* **This is that architecture test, and
  // the last clause is the criterion telling you it must not be a review item.** Until `AC-ATT-0026` was
  // worked, Attendance was absent from the forbidden list and this test asserted nothing about it.
  //
  // **Planted: removing the `SSAS.Attendance.Contracts` exemption reddens the `SSAS.Payroll.Application`
  // row**, which is the assembly that legitimately holds the contract reference — so the clause is live and
  // is evaluated against a real reference rather than passing over an empty set.
  //
  // ⚠⚠ THE BOUND OF *THIS* TEST: `GetReferencedAssemblies` reads EMITTED metadata, so **it catches
  // CONSUMPTION, not DECLARATION.** A `ProjectReference` to `SSAS.Attendance.Domain` that no Payroll type
  // ever uses is pruned by the compiler and would not appear here.
  //
  // ⚠⚠⚠ **BUT THAT IS NOT A BOUND ON THE CRITERION, AND I FIRST RECORDED IT AS ONE.**
  // `AttendanceArchitectureTests.Payroll_references_the_attendance_contracts_and_no_attendance_implementation`
  // already existed, already asserted this criterion, and **closes the declared half** — its plant record
  // shows an unused `SSAS.Attendance.Domain` reference passing the emitted assertion and reddening the
  // declared one. *I cited here before finding it, which is the same search failure as the permission-plane
  // duplicate: I searched the Payroll files and not the Attendance one, while reading the Attendance one.*
  //
  // **Both keep the citation because neither subsumes the other:** that test is EXHAUSTIVE and covers
  // declared + emitted but only `SSAS.Payroll.Application`; this one covers **all four Payroll assemblies**
  // and is emitted-only. A reference from `SSAS.Payroll.API` is caught only here.
  [Theory]
  [Trait("Decision", "ADR-012")]
  [Trait("Criterion", "AC-ATT-0026")]
  [InlineData("SSAS.Payroll.Domain")]
  [InlineData("SSAS.Payroll.Application")]
  [InlineData("SSAS.Payroll.Infrastructure")]
  [InlineData("SSAS.Payroll.API")]
  public void Payroll_assemblies_reach_other_modules_only_through_contracts(string assemblyName)
  {
    var assembly = Assembly.Load(assemblyName);

    var forbidden = assembly.GetReferencedAssemblies()
      .Select(reference => reference.Name)
      .Where(name => name is not null)
      .Where(name =>
        // Another module's IMPLEMENTATION assemblies are out of reach. The two `.Contracts` assemblies are
        // the sanctioned doors and are deliberately not in this list.
        name!.StartsWith("SSAS.GL.", StringComparison.Ordinal) && name != "SSAS.GL.Contracts" ||
        name.StartsWith("SSAS.HR.", StringComparison.Ordinal) && name != "SSAS.HR.Contracts" ||
        // ⚠⚠ ATTENDANCE WAS MISSING FROM THIS LIST UNTIL `AC-ATT-0026` WAS WORKED, AND THE MODULE HAD
        // SHIPPED. GL and HR were named when this test was written; FP-013 added a fourth module that
        // Payroll genuinely consumes — `SSAS.Payroll.Application` references `SSAS.Attendance.Contracts` —
        // and the forbidden list was not extended with it.
        //
        // **The property was never violated: measured, no Payroll assembly references any Attendance
        // IMPLEMENTATION project.** So this closes a hole in the GUARD rather than fixing a defect in the
        // product. ***A LIST-SHAPED GUARD SILENTLY EXCLUDES EVERYTHING ADDED AFTER IT WAS WRITTEN, and its
        // green is indistinguishable from a green that covers the new member.***
        name.StartsWith("SSAS.Attendance.", StringComparison.Ordinal)
          && name != "SSAS.Attendance.Contracts")
      .ToArray();

    Assert.Empty(forbidden);
  }

  // ---- ⚠⚠⚠ PAYROLL CANNOT REOPEN A FISCAL PERIOD, BECAUSE IT CANNOT REACH ANYTHING THAT COULD (AC-PAY-0023).
  //
  // *"Closing a fiscal period is not reversed by any payroll operation."* **That is true today by the shape
  // of the door rather than by any check inside Payroll**, and the two tests below pin the door.
  //
  // `SSAS.GL.Contracts` is the ONLY GL assembly Payroll may reference — `Payroll_assemblies_reach_other_
  // modules_only_through_contracts` above enforces that, and deliberately does NOT constrain what lives
  // inside the sanctioned door. *This is the surface within it.* `IJournalPoster` posts, reverses and
  // inspects; none of the three closes or reopens a period, and `PeriodClosed` is an OUTCOME Payroll
  // RECEIVES, never an action it can take.
  //
  // ⚠⚠ WHY THIS IS ASSERTED BY REFLECTION AND NOT BY SCANNING `using` LINES. A source scan finds only what
  // an import declares, and **a fully-qualified `SSAS.GL.Contracts.Posting.IJournalPoster` needs no import
  // at all** — so the scan's green would be silent about exactly the reference that avoided it. The
  // exported-type set cannot be avoided that way: a type Payroll could name is a type this assertion sees.
  //
  // ⚠⚠⚠ AND BOTH ARE EXACT-SET EQUALITIES RATHER THAN BAN LISTS, WHICH IS THIS FILE'S OWN LESSON APPLIED.
  // The Attendance note thirty lines above records a list-shaped guard that silently excluded a module
  // added after it was written, *"and its green is indistinguishable from a green that covers the new
  // member."* **A ban list cannot fail on an ADDITION, and an addition is the entire hazard here** — the
  // change this criterion fears is somebody adding a period-administration capability, not removing one.
  // An equality against a literal list also cannot pass over an empty derived set, so it is self-
  // controlling by shape and needs no separate anti-vacuity floor.
  //
  // ⚠ WHAT WOULD BREAK THESE, AND WHY THAT IS THE POINT. *"Payroll should be able to reopen the period to
  // fix a run"* is a reasonable thing for a competent person to want, and adding a member to
  // `IJournalPoster` is exactly how they would do it. The compiler DOES object to that — every implementer
  // gains an obligation — **but whoever adds the member implements it in the same edit, so the objection is
  // raised and satisfied inside one change and nothing survives to be noticed later.** That is the same
  // "noticed, then routinely silenced" pattern as a constructor parameter, and it is why this earns a guard
  // rather than a note.
  // ---- ⚠⚠⚠ A PAY ELEMENT'S CODE COMES FROM THE CALLER, AND FROM NOWHERE ELSE (AC-PAY-0006).
  //
  // *"A pay element is created with a caller-supplied code; no code is generated."* Two clauses, and **the
  // CONSTRUCTION-PATH clause leads, because it is the one that does not depend on anybody's choice of
  // words.** A generator named `Mint`, `Next` or `Allocate` defeats a search for the word "generate"; it
  // does not defeat *"every public way to build a `PayElement` takes the code as an argument."*
  //
  // ⚠ THE TREE ALREADY STATES THIS DOCTRINE FOR THE SIBLING CRITERION, and the wording is theirs rather
  // than mine. `PayElementDomainTests.An_element_code_cannot_be_changed_after_creation` closes `AC-PAY-0007`
  // and says of it: *"There is no method to change it, and the wire shape has no field for it — the absence
  // IS the rule."* **That criterion governs the code after creation; this one governs where it came from.**
  //
  // ⚠⚠ THE NAME BAN IS THE WEAKER HALF AND IS PLACED SECOND DELIBERATELY. It is a search over spellings,
  // so it can only ever catch a generator that announces itself. It earns its place because HR's
  // `No_employee_number_generator_exists` is the established instrument for exactly this shape — and it is
  // the control the vacuity sweep uses, so its floor-and-matcher form is known good — but the assertion
  // that actually carries this criterion is the one above it.
  [Fact]
  [Trait("Criterion", "AC-PAY-0006")]
  public void A_pay_element_code_is_supplied_by_its_caller_and_generated_by_nothing()
  {
    // ---- CLAUSE 1, THE CONSTRUCTION PATH. No public constructor at all: one private ctor for the factory
    // and one for EF materialization. So `Create` is the only door into this type.
    Assert.Empty(typeof(PayElement).GetConstructors());

    var factories = typeof(PayElement)
      .GetMethods(BindingFlags.Public | BindingFlags.Static)
      .Where(method => method.ReturnType == typeof(Result<PayElement>))
      .ToArray();

    // THE FLOOR, because `Assert.All` over an empty set is the same green as compliance — and this walk
    // is keyed on a return type, which a refactor could silently change.
    Assert.NotEmpty(factories);

    // ***AND EVERY ONE OF THEM DEMANDS A CODE.*** Stated as a universal over the construction paths rather
    // than as a fact about one method, so a SECOND factory that generated its own code would fail here.
    Assert.All(factories, factory =>
      Assert.Contains(
        factory.GetParameters(),
        parameter => parameter.Name == "code" && !parameter.IsOptional));

    // ---- AND AN ABSENT CODE IS REFUSED RATHER THAN FILLED IN, which is what makes "supplied" mean
    // supplied. Without this the parameter could be accepted as null and quietly replaced downstream.
    Assert.True(PayElementCode.Create(null).IsFailure);
    Assert.True(PayElementCode.Create("   ").IsFailure);

    // ---- CLAUSE 2, THE NAME BAN. HR's shape, including its floor and its matcher control: an empty type
    // set satisfies a ban identically to compliance, and a comparison that can never match is
    // indistinguishable from one that is satisfied.
    var types = typeof(PayElement).Assembly.GetTypes()
      .Concat(Assembly.Load("SSAS.Payroll.Infrastructure").GetTypes())
      .Select(type => type.Name)
      .ToArray();

    Assert.NotEmpty(types);
    Assert.Contains(types, name => name.Contains("PayElement", StringComparison.OrdinalIgnoreCase));

    Assert.DoesNotContain(types, name =>
      name.Contains("CodeGenerator", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("CodeSequence", StringComparison.OrdinalIgnoreCase) ||
      name.Contains("CodeAllocator", StringComparison.OrdinalIgnoreCase));
  }

  // ---- ⚠⚠⚠ THE LIMIT OF THIS ASSERTION, AND WHERE IT IS COVERED.
  //
  // ***A MEMBER-LIST GUARD WATCHES WHO YOU CAN REACH. IT IS BLIND TO WHAT ARRIVES THROUGH WHAT YOU
  // ALREADY REACH.*** This list catches a new `ReopenPeriodAsync`; it would not move for a
  // `FiscalPeriodId` plus a `Reopen` flag added to `JournalPostingRequest`, which reopens a period through
  // `PostAsync` — an existing member — instead.
  //
  // **That half is closed by the payload member-set assertion in the test below**, and the two are a pair:
  // this one pins the doors, that one pins what can be carried through them. *Neither is sufficient alone
  // and the criterion needs both.*
  [Fact]
  [Trait("Criterion", "AC-PAY-0023")]
  public void The_only_ledger_capabilities_payroll_can_reach_are_posting_reversing_and_inspecting()
  {
    Assert.Equal(
      ["InspectPostingWindowAsync", "PostAsync", "ReverseAsync"],
      typeof(IJournalPoster).GetMethods()
        .Select(method => method.Name)
        .OrderBy(name => name, StringComparer.Ordinal));
  }

  [Fact]
  [Trait("Criterion", "AC-PAY-0023")]
  public void The_ledger_contract_assembly_exposes_no_calendar_administration_at_all()
  {
    var exported = typeof(IJournalPoster).Assembly.GetExportedTypes();

    // Every exported type sits in the posting namespace. A `SSAS.GL.Contracts.Calendar` added beside it
    // would be reachable by Payroll the moment it existed, without touching a single Payroll file.
    Assert.All(exported, type => Assert.Equal("SSAS.GL.Contracts.Posting", type.Namespace));

    Assert.Equal(
      [
        "IJournalPoster",
        "JournalPostingLine",
        "JournalPostingOutcome",
        "JournalPostingRequest",
        "JournalPostingStatus",
        "JournalReversalRequest",
        "PostingWindow",
        "PostingWindowStatus"
      ],
      exported.Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal));

    // ---- ⚠⚠⚠ AND EVERY MEMBER OF EVERY PAYLOAD, WHICH IS THE HALF THE TYPE SET CANNOT SEE.
    //
    // ***A TYPE-SET AND A MEMBER-LIST GUARD BOTH WATCH WHO YOU CAN REACH. NEITHER SEES WHAT ARRIVES
    // THROUGH WHAT YOU ALREADY REACH.*** The set above catches a new contract type and the assertion
    // above that catches a new interface method — **and a `FiscalPeriodId` plus a `Reopen` flag added to
    // `JournalPostingRequest` would reopen a period through `PostAsync`, an EXISTING member of an EXISTING
    // type, without moving either.**
    //
    // ⚠ THIS IS A TRIPWIRE AND ITS PURPOSE IS WRITTEN ON IT, because it WILL redden on legitimate work.
    // **A new field on a cross-module contract is exactly the change that should be reviewed rather than
    // absorbed** — the reviewer's question is not *"is this field fine?"* but *"can Payroll now do
    // something to the ledger that `AC-PAY-0023` says it cannot?"* Update the list once that is answered.
    //
    // ⚠⚠ SET RATHER THAN SHAPE, and the distinction is why this exists at all. *"Every property is a
    // `Guid`"* works on a small homogeneous contract and says nothing about a five-member, four-type
    // record — anything it could assert there would restate the type declaration, which is an assertion
    // that cannot fail occupying the slot of one that could. **An exact member set works on any type
    // however heterogeneous, and cannot fail to notice an addition.**
    Assert.Equal(
      [
        "JournalPostingLine.AccountId",
        "JournalPostingLine.Credit",
        "JournalPostingLine.Debit",
        "JournalPostingLine.Description",
        "JournalPostingOutcome.Detail",
        "JournalPostingOutcome.IsPosted",
        "JournalPostingOutcome.JournalEntryId",
        "JournalPostingOutcome.PeriodName",
        "JournalPostingOutcome.Status",
        "JournalPostingRequest.CompanyId",
        "JournalPostingRequest.Description",
        "JournalPostingRequest.EntryDateUtc",
        "JournalPostingRequest.Lines",
        "JournalPostingRequest.Reference",
        "JournalReversalRequest.Description",
        "JournalReversalRequest.JournalEntryId",
        "JournalReversalRequest.ReversalDateUtc",
        "PostingWindow.EndUtc",
        "PostingWindow.FiscalPeriodId",
        "PostingWindow.IsOpen",
        "PostingWindow.PeriodName",
        "PostingWindow.StartUtc",
        "PostingWindow.Status"
      ],
      exported
        .Where(type => !type.IsEnum && !type.IsInterface)
        .SelectMany(type => type.GetProperties().Select(property => $"{type.Name}.{property.Name}"))
        .OrderBy(name => name, StringComparer.Ordinal));
  }

  // ---- THE POPULATION IS DERIVED, AND UNTIL T-083 IT WAS SIX TYPED `InlineData` ROWS.
  //
  // ⚠⚠⚠ THE TYPED LIST NAMED GL AND HR AT THREE LAYERS EACH AND MISSED NINE OF THE FIFTEEN NON-PAYROLL
  // MODULE ASSEMBLIES: every `SSAS.Attendance.*` project, plus `SSAS.GL.API`, `SSAS.HR.API`,
  // `SSAS.GL.Contracts` and `SSAS.HR.Contracts`. **Attendance is not hypothetical** — it is a registered
  // contributor in `CutoverTenantModel.Contributors`, and a Payroll reference from it is exactly the thing
  // this guard's name forbids, passing silently.
  //
  // ⚠ ADDING THE MISSING NAMES WOULD NOT HAVE BEEN THE FIX. It leaves the NEXT module to be forgotten by
  // the same mechanism that forgot this one, and a hand-written list is a guard that stops covering the
  // product as it grows without ever failing — `DeployedProductAssemblies` says so in its own header, which
  // is where the derivation now comes from.
  //
  // ⚠⚠⚠ REACH PROBE, BOTH COLOURS MEASURED — this is why the change is trustworthy rather than merely
  // larger. Pointing the ban's needle at `SSAS.Attendance` instead of `SSAS.Payroll`, changing nothing else:
  //
  //   DERIVED population  -> RED on three cases, `SSAS.Attendance.API`, `.Application` and `.Infrastructure`
  //   TYPED six-row list  -> GREEN, all six passing, over the identical assertions
  //
  // **The old guard could not have reddened for an Attendance violation under any circumstances.** It was
  // aimed at a change it was structurally incapable of seeing, and its green said the same thing on a clean
  // tree as it would have said on a breached one.
  [Theory]
  [Trait("Decision", "ADR-012")]
  [MemberData(nameof(OtherModuleAssemblies))]
  // ⚠ CITED BY B18, body-confirmed: ⚠ SUPERSET. The criterion names Payroll and GL specifically; this
  // asserts that no OTHER MODULE ASSEMBLY references Payroll — every project under `src/Modules` that is
  // not `SSAS.Payroll.*`, derived rather than listed.
  //
  // ⚠⚠ THAT SENTENCE USED TO READ *"this asserts NO module references Payroll at all"*, AND IT WAS FALSE
  // (T-083): the walk was six typed rows. **The over-claim was in the COMMENT, not only in the test name**,
  // so a reader doing the right thing — distrusting the name and reading the prose — was told the superset
  // a second time with a warning glyph on it. A rename would not have touched it.
  [Trait("Criterion", "AC-PAY-0025")]
  // ⚠ CITED BY B18 pass 15: ⚠ PARTLY PINNED, clause 1 only, and by SUPERSET.
  //
  // `AC-PAY-0003` clause 1 is *no EMPLOYEE compensation value is readable through any HR endpoint*. The
  // three HR assemblies are among the six this theory walks, and an assembly that cannot reference
  // `SSAS.Payroll.*` cannot expose a compensation type through any endpoint at all.
  //
  // ⚠ Clause 2 -- *or stored on any HR table* -- is NOT this test. It is an assembly-reference ban, and a
  // pay column on an HR table would need no reference to Payroll whatever. See
  // `No_employee_compensation_value_is_declared_in_hr` for that half.
  [Trait("Criterion", "AC-PAY-0003")]
  public void No_other_module_learns_about_payroll(string assemblyName)
  {
    // ---- THE OTHER DIRECTION, AND IT IS NOT SYMMETRY FOR ITS OWN SAKE.
    //
    // `DEC-PAY-0018` permits the ledger poster to skip a GL permission check partly because the coupling is
    // ONE-DIRECTIONAL: Payroll depends on GL, and GL knows nothing of payroll. If GL ever referenced
    // Payroll, that argument would silently stop holding while the decision still read as ratified.
    // ⚠ DECLARED AND EMITTED, BECAUSE THEY FAIL ON DIFFERENT DAYS (272). `GetReferencedAssemblies()` reads
    // emitted metadata and the compiler omits a reference no type is taken from, so GL could declare a
    // Payroll reference and pass here until the first use — by which point `DEC-PAY-0018`'s one-directional
    // argument has already stopped holding. Declared catches it at merge time; emitted catches consumption
    // including through a transitive path no `.csproj` of ours names.
    // The control proves the predicate fires where a Payroll reference legitimately exists.
    Assert.Contains(
      DeclaredDependencies.Of("SSAS.Host.API"),
      name => name.StartsWith("SSAS.Payroll", StringComparison.Ordinal));

    var referenced = Assembly.Load(assemblyName)
      .GetReferencedAssemblies()
      .Select(reference => reference.Name)
      .Where(name => name is not null && name.StartsWith("SSAS.Payroll", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(referenced);

    Assert.DoesNotContain(
      DeclaredDependencies.Of(assemblyName),
      name => name.StartsWith("SSAS.Payroll", StringComparison.Ordinal));
  }

  // Every module project that is not Payroll's own, read from the repository layout. `MemberData` is
  // evaluated at DISCOVERY, so a derivation that silently returned two would run two cases and report
  // success — which is why the floor below exists and why it calls THIS method rather than repeating it.
  public static TheoryData<string> OtherModuleAssemblies()
  {
    var data = new TheoryData<string>();

    foreach (var name in NonPayrollModuleProjectNames())
    {
      data.Add(name);
    }

    return data;
  }

  private static string[] NonPayrollModuleProjectNames() =>
    DeployedProductAssemblies.ModuleProjectNames()
      .Where(name => !name.StartsWith("SSAS.Payroll.", StringComparison.Ordinal))
      .ToArray();

  // ---- THE FLOOR ON THE DERIVATION, BECAUSE A THEORY OVER AN EMPTY SET IS NOT A FAILING THEORY.
  //
  // ⚠ A COUNT ALONE WOULD NOT HAVE CAUGHT THE DEFECT THIS REPLACES. Six is a perfectly healthy-looking
  // number, so the floor is paired with MEMBERSHIP of the two shapes the typed list actually dropped: a
  // whole module, and a layer present in some modules and not others.
  [Fact]
  public void The_other_module_walk_covers_every_non_payroll_module_assembly()
  {
    var derived = NonPayrollModuleProjectNames();

    // FIFTEEN measured 2026-09-07 — Attendance, GL and HR at five projects each. The floor sits below that
    // with room for a project to be retired, and far above the SIX the typed list reached.
    Assert.True(derived.Length >= 12,
      $"only {derived.Length} non-Payroll module projects were derived from src/Modules; fifteen were " +
      "measured at T-083. The layout walk has stopped matching, and the theory above is now inspecting a " +
      "subset of the modules while still reporting success for each one it does inspect.");

    // THE MEMBERS THE TYPED LIST FORGOT, named so this cannot regress to a healthy-looking count.
    Assert.Contains("SSAS.Attendance.Application", derived);
    Assert.Contains("SSAS.HR.Contracts", derived);
    Assert.Contains("SSAS.GL.API", derived);

    // THE EXCLUSION AS ITS OWN NEGATIVE: a filter that removed everything, or nothing, would otherwise be
    // invisible here — the assertions above pass either way.
    Assert.DoesNotContain(derived, name => name.StartsWith("SSAS.Payroll.", StringComparison.Ordinal));
    Assert.Contains("SSAS.Payroll.Application", DeployedProductAssemblies.ModuleProjectNames());
  }

  [Fact]
  [Trait("Decision", "ADR-012")]
  public void The_payroll_api_layer_references_no_platform_assembly()
  {
    // Declared and emitted, for the reason stated above: an unused `ProjectReference` is invisible to the
    // emitted reading. The control proves the predicate fires where Platform is legitimately referenced.
    Assert.Contains(
      DeclaredDependencies.Of("SSAS.Host.API"),
      name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));

    var referenced = Assembly.Load("SSAS.Payroll.API")
      .GetReferencedAssemblies()
      .Select(reference => reference.Name)
      .Where(name => name is not null && name.StartsWith("SSAS.Platform", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(referenced);

    Assert.DoesNotContain(
      DeclaredDependencies.Of("SSAS.Payroll.API"),
      name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));
  }

  // ================================================================================================
  // THE UNFORGEABLE READ SCOPE — AND HERE IT GUARDS PERSONAL DATA
  // ================================================================================================

  // ⚠⚠⚠ THIS ASSERTS THE SCOPE IS A PARAMETER. IT DOES NOT ASSERT THE QUERY USES IT (measured 2026-09-06).
  //
  // The comment below says an omitted scope predicate "is not something a caller can express". That is true
  // of the SIGNATURE and false of the BODY: a method can accept `PayrollReadScope scope`, satisfy this
  // guard forever, and never reference it in the `Where`.
  //
  // *Measured, subtractive, site determined:* deleting `.Where(record => record.CompanyId == companyId)`
  // from `PayrollRepositories.GetHistoryForCompanyAsync` left the whole gate GREEN. The same plant in
  // `AttendanceRepositories.GetCoveringAsync` was also GREEN. **No test in the tree fails when a Payroll or
  // Attendance company predicate is deleted** — and these two modules carry the most hand-written company
  // predicates in the product.
  //
  // ⚠ THE GUARD IS NOT VACUOUS AND IS NOT WRONG. Its population is every method on the interface, and it
  // proves what it claims to prove: the CALLER holds authority. Nothing proves the QUERY applies it.
  // `A_read_scope_cannot_be_constructed_from_outside_its_assembly` and `An_empty_company_set_cannot_produce
  // _a_scope` are about the same half.
  //
  // Recorded rather than remedied: asserting the body composes a predicate is a source-walk guard of the
  // kind `GlReadScopeArchitectureTests` and `PositionReadScopeArchitectureTests` already carry, and adding
  // one here is a scope decision rather than a test edit.
  [Fact]
  public void Every_read_service_method_requires_a_scope()
  {
    // A read that omitted its scope predicate is not something a reviewer has to catch, because it is not
    // something a caller can express. There is no overload without one, and no default.
    // ⚠ See the correction above: this is true of the signature and does not extend to the query body.
    var withoutScope = typeof(IPayrollReadService)
      .GetMethods()
      .Where(method => !method.GetParameters().Any(p => p.ParameterType == typeof(PayrollReadScope)))
      .Select(method => method.Name)
      .ToArray();

    Assert.Empty(withoutScope);
  }

  [Fact]
  // ⚠ CITED BY B18, body-confirmed: ⚠ PARTIAL. `AC-PAY-0028` has two halves: *a read scope cannot be SUPPLIED BY THE CALLER*, and *a
  // request attempting to WIDEN ITS OWN SCOPE is refused*. This asserts the first structurally -- no
  // public constructors, no factories outside the assembly. The runtime refusal half is pinned by
  // nothing here, and is recorded rather than implied (B18).
  [Trait("Criterion", "AC-PAY-0028")]
  public void A_read_scope_cannot_be_constructed_from_outside_its_assembly()
  {
    // The credential stays per-module (`ADR-027` d4 promotion boundary): the VALUE moved to
    // `AuthorizedCompanySet`, the proof did not. Holding a `PayrollReadScope` means Payroll's resolver ran.
    Assert.Empty(typeof(PayrollReadScope).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    var factories = typeof(PayrollReadScope)
      .GetMethods(BindingFlags.Public | BindingFlags.Static)
      .Where(method => method.ReturnType == typeof(PayrollReadScope) ||
        method.ReturnType == typeof(PayrollReadScope).MakeByRefType())
      .ToArray();

    Assert.Empty(factories);
  }

  [Fact]
  public void An_empty_company_set_cannot_produce_a_scope()
  {
    // An empty authorized set REFUSES the read rather than returning an empty page. Enforcing it at
    // construction makes `WHERE CompanyId IN ()` unrepresentable rather than merely guarded against.
    Assert.Null(SSAS.BuildingBlocks.Application.Authorization.AuthorizedCompanySet.Create([]));
    Assert.Null(SSAS.BuildingBlocks.Application.Authorization.AuthorizedCompanySet.Create(null));
  }

  [Fact]
  public void The_scope_carries_materialized_identifiers_and_no_mode_flag()
  {
    // "All companies" is a LIST, never the absence of a condition. A boolean or enum here would let a query
    // branch on intent instead of filtering on values — predicate omission wearing a scope's clothes.
    foreach (var property in typeof(PayrollReadScope).GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      Assert.False(
        property.PropertyType == typeof(bool) || property.PropertyType.IsEnum,
        $"PayrollReadScope.{property.Name} would let a query branch on intent.");
    }
  }

  // ================================================================================================
  // THE PERMISSION CATALOG (FP-006P)
  // ================================================================================================

  [Fact]
  public void Every_named_permission_is_defined_by_the_catalog_contributor()
  {
    // FP-006P's incident: HR's constants existed, no catalog defined them, no role could hold one, and every
    // Employee endpoint refused every caller. Naming a permission is not registering it.
    var named = typeof(PayrollPermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral)
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToArray();

    var defined = new PayrollPermissionCatalogContributor().Permissions
      .Select(permission => permission.Name)
      .ToArray();

    Assert.Equal(named.OrderBy(n => n, StringComparer.Ordinal), defined.OrderBy(n => n, StringComparer.Ordinal));
  }

  [Fact]
  public void Every_permission_name_has_three_segments_and_the_payroll_plane()
  {
    foreach (var definition in new PayrollPermissionCatalogContributor().Permissions)
    {
      var segments = definition.Name.Split('.');

      Assert.Equal(3, segments.Length);
      Assert.Equal("Payroll", segments[0]);
    }
  }

  [Fact]
  public void Every_permission_definition_carries_a_description_written_for_the_grantor()
  {
    // On this surface the description matters more than anywhere else: someone granting "view runs" needs to
    // know it does not hand over everyone's pay.
    foreach (var definition in new PayrollPermissionCatalogContributor().Permissions)
    {
      Assert.False(string.IsNullOrWhiteSpace(definition.Description));
      Assert.True(definition.Description.Length > 20, $"{definition.Name} has a placeholder description.");
    }
  }

  [Fact]
  [Trait("Decision", "BR-PAY-0010")]
  public void Compensation_and_payslips_are_separate_permissions_from_every_element_permission()
  {
    // `DEC-POS-0018` separated a permission for STRUCTURAL pay bands; individual compensation is personal
    // data, so the split applies with more force. This asserts the two families are distinct names — a
    // future merge would have to delete this test.
    var personal = new[] { PayrollPermissionNames.ViewCompensation, PayrollPermissionNames.ViewPayslips };
    var structural = new[] { PayrollPermissionNames.ViewElements, PayrollPermissionNames.ViewRuns };

    Assert.Empty(personal.Intersect(structural, StringComparer.Ordinal));
  }

  // ================================================================================================
  // THE SCHEMA THE RULINGS GAVE IT
  // ================================================================================================

  [Fact]
  [Trait("Decision", "DEC-PAY-0004")]
  // ⚠ CITED BY B18, body-confirmed: the DECIMAL(19,4) half. A floor of 4 columns is its control.
  [Trait("Criterion", "AC-PAY-0030")]
  public void Every_monetary_column_is_decimal_19_4()
  {
    var columns = ModelWalk.FlooredProperties(
      ModelWalk.FlooredEntities(PayrollEntities(), "Payroll domain", 6), "Payroll domain", 70);

    // ⚠ THE CONTROL ON THIS BAN'S OWN FILTER. The floors above prove the model was read; they cannot
    // prove `ClrType == decimal` still selects anything. Payroll is made of money -- a decimal filter that
    // finds nothing here has broken, and without this the ban is green over an empty set.
    var monetary = columns
      .Where(pair => pair.Property.ClrType == typeof(decimal) || pair.Property.ClrType == typeof(decimal?))
      .ToArray();

    Assert.True(monetary.Length >= 4,
      $"only {monetary.Length} decimal columns were found across {columns.Length} payroll properties; the " +
      "type filter has stopped matching and 'every monetary column is 19,4' would be a claim about nothing.");

    var offenders = monetary
      .Where(pair => pair.Property.GetPrecision() != 19 || pair.Property.GetScale() != 4)
      .Select(pair => $"{pair.Entity.ShortName()}.{pair.Property.Name}")
      .ToArray();

    Assert.Empty(offenders);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0007")]
  // ⚠ CITED BY B18, body-confirmed: the NVARCHAR half; the DECIMAL half is the test below. A floor of 12 columns is its control.
  [Trait("Criterion", "AC-PAY-0030")]
  public void Every_payroll_string_column_is_unicode()
  {
    // `Constraints.md` requires Arabic and English. A pay element's name is exactly the field a user writes
    // in their own language.
    var columns = ModelWalk.FlooredProperties(
      ModelWalk.FlooredEntities(PayrollEntities(), "Payroll domain", 6), "Payroll domain", 70);

    // ⚠ THE CONTROL ON THIS BAN'S OWN FILTER, and it is a DIFFERENT filter from the monetary one above
    // even though both walk the same properties. That is precisely why the floor cannot be shared with it.
    var strings = columns.Where(pair => pair.Property.ClrType == typeof(string)).ToArray();

    Assert.True(strings.Length >= 12,
      $"only {strings.Length} string columns were found across {columns.Length} payroll properties; the " +
      "type filter has stopped matching and the unicode ban would inspect nothing.");

    var offenders = strings
      .Where(pair => pair.Property.IsUnicode() == false)
      .Select(pair => $"{pair.Entity.ShortName()}.{pair.Property.Name}")
      .ToArray();

    Assert.Empty(offenders);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0010")]
  // ⚠ CITED BY B18, body-confirmed: ⚠ AND THE CRITERION'S COUNT IS STALE. It says "all FIVE payroll tables"; the manifest's exact list
  // carries SEVEN -- EmployeeCompensation, PayElement, PayElementAssignment, PayrollPeriod, PayrollRun,
  // PayrollRunDraftLine, PayrollRunLine -- and CutoverManifestArchitectureTests says so in its own
  // comment. The PROPERTY holds; the NUMBER in the criterion does not.
  [Trait("Criterion", "AC-PAY-0029")]
  public void Every_payroll_entity_is_tenant_owned_and_therefore_enters_the_cutover_manifest()
  {
    // ---- THE SILENT FAILURE THIS PREVENTS.
    //
    // `TenantCutoverCopyPlan.Build` derives its manifest by REFLECTING over `ITenantOwnedEntity`. A type
    // without the interface is absent from cutover and nothing says so — FP-011 shipped two such types
    // before catching them. Being an owned child is a DOMAIN fact; being copied is a REFLECTION fact.
    var entities = ModelWalk.FlooredEntities(PayrollEntities(), "Payroll domain", 6);

    // ⚠ The floor proves entities were found. The assignability test is the matcher, and the control
    // for it is the POSITIVE case: payroll entities are tenant-owned, so the same test must select them.
    Assert.Contains(entities, entity => typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType));

    var notTenantOwned = entities
      .Where(entity => !typeof(ITenantOwnedEntity).IsAssignableFrom(entity.ClrType))
      .Select(entity => entity.ShortName())
      .ToArray();

    Assert.Empty(notTenantOwned);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0009")]
  public void Only_mutable_payroll_aggregates_carry_a_row_version()
  {
    // A history row is never updated, so `EmployeeCompensation` deliberately has none — advertising one
    // would suggest an update path that does not exist. The append-only line has none for the same reason.
    var withRowVersion = PayrollEntities()
      .Where(entity => entity.GetProperties().Any(property => property.Name == "RowVersion"))
      .Select(entity => entity.ShortName())
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(["PayrollPeriod", "PayrollRun"], withRowVersion);
  }

  [Fact]
  // ---- ⚠⚠⚠ THE `AC-PAY-0031` CITATION WAS REMOVED HERE ON 2026-09-05. THE TEST STAYS; THE SUPERSET
  // ---- ARGUMENT THAT JUSTIFIED THE CITATION WAS FALSE.
  //
  // *"No foreign key crosses from a payroll table to a **Platform-database** table."*
  //
  // The removed comment claimed: *"⚠ SUPERSET. The criterion bans a foreign key to a PLATFORM-DATABASE
  // table; this bans one to ANY other module's table, **which is strictly wider**."*
  //
  // ***THE PREDICATE BELOW IS `SSAS.HR.` OR `SSAS.GL.`. `SSAS.Platform.` IS NOT IN IT.*** **Banning HR and
  // GL is not wider than banning Platform — it is a DIFFERENT SET that excludes the criterion's subject
  // entirely.** *`{HR, GL} ⊇ {Platform}` is false; the sets are disjoint.*
  //
  // ⚠⚠ ***THE PROPERTY HOLDS, STRUCTURALLY, AND FOR A REASON THE TEST NEVER STATES:*** the model below is
  // composed from `PayrollTenantModelContributor` in the TENANT context, so no Platform entity is in it and
  // a foreign key to one is not expressible. **Adding `SSAS.Platform.` to the predicate would be VACUOUS —
  // which is presumably why it is absent, and is exactly the argument that should have been written down.**
  //
  // ⚠ ***THIS ONE IS MORE DANGEROUS THAN A MISSING JUSTIFICATION, BECAUSE A READER WHO CHECKS THE REASONING
  // FINDS REASONING.*** A precise, confident, well-formatted argument that does not hold survives review in
  // a way that silence does not. **The test is good and stays; only the claim it was carrying is withdrawn.**
  public void No_payroll_foreign_key_points_at_another_modules_table()
  {
    // A database-level FK across a module boundary would couple the two migration streams and make the
    // boundary a fiction at the schema layer even while `ADR-012` held at the assembly layer.
    var entities = ModelWalk.FlooredEntities(PayrollEntities(), "Payroll domain", 6);

    // ⚠ THE FOREIGN-KEY LAYER IS ITS OWN WALK AND GETS ITS OWN FLOOR. A healthy entity list whose
    // `GetForeignKeys()` returns nothing is a different failure from an empty model, and the ban below
    // cannot tell the difference on its own.
    var keys = entities.SelectMany(entity => entity.GetForeignKeys()).ToArray();

    Assert.True(keys.Length >= 8,
      $"{entities.Length} payroll entities declared only {keys.Length} foreign keys; the relationship walk " +
      "has collapsed and 'no key crosses a module' would be a claim about nothing.");

    var crossing = keys
      .Where(key =>
      {
        var principal = key.PrincipalEntityType.ClrType.FullName ?? string.Empty;
        return principal.StartsWith("SSAS.HR.", StringComparison.Ordinal) ||
          principal.StartsWith("SSAS.GL.", StringComparison.Ordinal);
      })
      .Select(key => $"{key.DeclaringEntityType.ShortName()} -> {key.PrincipalEntityType.ShortName()}")
      .ToArray();

    Assert.Empty(crossing);
  }

  private static IEnumerable<IEntityType> PayrollEntities() =>
    ComposedModel().GetEntityTypes()
      .Where(entity => (entity.ClrType.FullName ?? string.Empty)
        .StartsWith("SSAS.Payroll.Domain", StringComparison.Ordinal));

  private static IModel ComposedModel()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    using var context = new TenantDbContext(
      options,
      new ModelOnlyUser(),
      new ModelOnlyTenant(),
      new ModelOnlyClock(),
      modelContributors: [new PayrollTenantModelContributor()]);

    return context.Model;
  }

  private sealed class ModelOnlyUser : SSAS.BuildingBlocks.Application.Abstractions.Identity.ICurrentUser
  {
    public string? UserId => null;

    public string? UserName => null;

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelOnlyTenant : SSAS.BuildingBlocks.Application.Abstractions.Tenancy.ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class ModelOnlyClock : SSAS.BuildingBlocks.Application.Abstractions.Time.IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}
