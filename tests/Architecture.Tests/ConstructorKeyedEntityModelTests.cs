using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

using SSAS.TestSupport.CutoverModel;

namespace SSAS.Architecture.Tests;

// ================================================================================================
// THE GUARD FOR A DEFECT THAT SHIPPED, AND THE ONLY REASON IT WAS EVER FOUND.
// ================================================================================================
//
// **`CalculatePayrollRunCommandHandler` did not work against a real database.** It threw
// "Tenant ownership cannot be changed after an entity is created" on every calculation, and the money path
// of the product was broken on main.
//
// ---- THE MECHANISM, BECAUSE IT IS SUBTLE AND WILL RECUR OTHERWISE.
//
// Every aggregate here assigns its own key in its constructor — `new PayrollRunDraftLine(Guid.NewGuid(), …)`.
// EF Core's convention for a `Guid` primary key is `ValueGeneratedOnAdd`, and EF treats a store-generated
// key that already holds a NON-DEFAULT value, discovered while fixing up a tracked graph, as an entity that
// **already exists**. So brand-new lines were classified `Modified` rather than `Added`.
//
// `PersistenceDbContext.ApplyPersistenceRules` stamps `TenantId` on `Added` tenant-owned entities and
// REFUSES a `Modified` one whose `TenantId` changed. Classified `Modified`, the lines were never stamped,
// their `TenantId` stayed `Guid.Empty`, and the save was refused — correctly, by a rule doing its job on a
// classification that was wrong upstream.
//
// ---- WHY IT SURVIVED EVERY EXISTING SUITE.
//
// `PayrollSchemaSqlServerTests` builds its calculated run by calling `SetCalculation` on a NEW run and then
// `context.Add(run)`, so the whole graph is `Added` and the misclassification cannot arise. The API tests
// stub `IPayrollRunRepository` entirely. **The calculate path had therefore never been executed against a
// real database**, and `PayrollChainSqlServerTests` — the end-to-end spine — was the first thing to do it.
//
// ---- WHY THIS GUARD IS A MODEL ASSERTION RATHER THAN A PAYROLL TEST.
//
// HR and Platform already had the convention: nineteen configurations across the two of them declare
// `ValueGeneratedNever()` on their constructor-assigned keys. **GL, Payroll and Attendance had none** — the
// convention existed and three modules had simply not been held to it.
//
// So a Payroll regression test would have closed one hole and left the same hole open in GL and Attendance,
// and open again in the next module somebody writes. This asserts it over the whole COMPOSED TENANT MODEL,
// so the class of defect dies across every tenant module at once rather than one module at a time.
//
// ---- WHAT IT DOES NOT COVER, STATED RATHER THAN LEFT TO BE ASSUMED.
//
// The composed TENANT model only. Platform's own context (identity, authentication, localization) is a
// separate model this does not build. Its eight configurations already declare `ValueGeneratedNever`, so it
// is compliant today — but compliant by habit rather than by assertion, and if that context ever grows an
// entity that forgets, nothing here will notice. Extending the sweep to it is a small, separate change.
// ---- ⚠ MOVED OUT OF THE INTEGRATION SUITE, AND IT IS THE NINTH OF ITS KIND (T-257).
//
// Neither test opens a connection: `ComposedContext()` builds the EF model from a deliberately unusable
// connection string, `"Server=unused;Database=model-only"`. They assert about a MODEL, not a database.
//
// **`GATE_SCOPE=TASK` never runs the Integration suite**, so both spent their lives behind a 24-minute
// SQL Server dependency that ordinary development does not invoke. Here they run in every gate.
//
// ---- WHY THIS ONE MATTERS MORE THAN THE EIGHT BEFORE IT.
//
// A duration sweep of the Integration suite on 2026-08-27 found eight database-free tests and they were
// moved. Re-running the sweep on a current corpus three days later found **one — this file — and it had
// not existed on the 27th.** The eight were never a backlog to clear: **they are an arrival rate.**
//
// That is why `IntegrationSuiteTimingGuardTests` now asserts the property continuously rather than a
// person re-running the sweep. A cleanup that runs once is the wrong instrument for a source that keeps
// producing.
//
// ---- WHAT UNBLOCKED IT.
//
// `CutoverTenantModel` moved to `tests/TestSupport/SSAS.TestSupport.CutoverModel` in T-253 so both suites
// could share one definition. Until then this file could not follow the other six.
public sealed class ConstructorKeyedEntityModelTests
{
  [Fact]
  [Trait("Decision", "FP-013-followup")]
  public void Every_constructor_keyed_entity_declares_its_key_value_generated_never()
  {
    using var context = ComposedContext();

    var entities = ModelWalk.FlooredEntities(context.Model.GetEntityTypes(), "composed tenant model", 28);

    // ⚠ THE SELECTING HALF OF THE CHAIN, ASSERTED SEPARATELY FROM THE BANNING HALF.
    //
    // Four `.Where` links stand between the walk and the offender list, and the ban is satisfied if ANY
    // of them stops matching -- not owned, single-property key, Guid key, then the violation itself.
    // The floor above proves the model was built; this proves the first three links still select the
    // candidates the fourth is supposed to judge.
    var guidKeyedCandidates = entities
      .Where(entity => !entity.IsOwned())
      .Select(entity => new { Entity = entity, Key = entity.FindPrimaryKey() })
      .Where(candidate => candidate.Key is { Properties.Count: 1 })
      .Where(candidate => candidate.Key!.Properties[0].ClrType == typeof(Guid))
      .ToArray();

    Assert.True(guidKeyedCandidates.Length >= 10,
      $"only {guidKeyedCandidates.Length} Guid single-key entities were found among {entities.Length}; " +
      "the selection chain has stopped matching and the ban below would judge nothing.");

    var offenders = guidKeyedCandidates
      .Where(candidate => candidate.Key!.Properties[0].ValueGenerated != ValueGenerated.Never)
      .Select(candidate =>
        candidate.Entity.ShortName() + "." + candidate.Key!.Properties[0].Name
        + " is " + candidate.Key.Properties[0].ValueGenerated)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      offenders.Length == 0,
      "These entities have a single Guid primary key that EF still treats as store-generated. Every " +
      "aggregate in this product assigns its key in its constructor, so a non-default key on a tracked " +
      "graph is classified Modified rather than Added, TenantId is never stamped, and the write boundary " +
      "refuses the save. Add `.ValueGeneratedNever()` to the key property in the entity's configuration: " +
      string.Join(", ", offenders));
  }

  // ---- AND THE INVENTORY IS DERIVED, SO A NEW MODULE JOINS IT BY EXISTING.
  //
  // Stated as a floor rather than an exact count: an exact count is a second thing to update when a module
  // is added, and the assertion above is what actually protects the product. This only proves the sweep
  // swept something — a reflection guard that silently matches nothing passes, which is the failure mode
  // FP-012 found in GL's original absence guard.
  [Fact]
  public void The_sweep_covers_the_whole_composed_model_rather_than_one_module()
  {
    using var context = ComposedContext();

    var guidKeyed = context.Model.GetEntityTypes()
      .Where(entity => !entity.IsOwned())
      .Select(entity => entity.FindPrimaryKey())
      .Count(key => key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(Guid));

    // Platform + HR + GL + Payroll + Attendance. Thirty-four tenant-owned entities alone appear in the
    // cutover manifest, so anything near zero means the model was not composed.
    Assert.True(guidKeyed >= 30, $"Only {guidKeyed} Guid-keyed entities found; the model is not composed.");
  }

  // The same four contributors the Host registers and the cutover tests use. Reusing that one definition
  // rather than building a fifth list — the reason `CutoverTenantModel` exists.
  private static TenantDbContext ComposedContext()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=unused;Database=model-only;Trusted_Connection=True;TrustServerCertificate=True")
      .Options;

    return new TenantDbContext(
      options, new ModelOnlyUser(), new ModelOnlyTenant(), new ModelOnlyClock(),
      modelContributors: CutoverTenantModel.Contributors);
  }

  // No database is opened: `context.Model` is built from the contributors alone, so this test needs no SQL
  // Server despite living beside tests that do.
  private sealed class ModelOnlyUser : ICurrentUser
  {
    public string? UserId => "model-only";

    public string? UserName => "model-only";

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelOnlyTenant : ICurrentTenant
  {
    public Guid? TenantId => Guid.Empty;
  }

  private sealed class ModelOnlyClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}
