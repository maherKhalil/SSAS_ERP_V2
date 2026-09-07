using Microsoft.EntityFrameworkCore.Metadata;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// FLOORS FOR A WALK OVER AN EF MODEL, ONE PER LAYER (T-265).
// ==================================================================================================
//
// ---- WHY THESE HAD NO FLOOR AT ALL UNTIL NOW.
//
// Twenty-five test methods ban something over a runtime enumeration -- `GetEntityTypes()`, endpoint data
// sources, resolved services -- and **not one of them carried a floor.** The reason is the same everywhere:
// there is no obvious collection to floor. A directory walk looks like something that might come back
// empty; `context.Model.GetEntityTypes()` does not. It looks like an object graph, and an object graph
// looks like it is simply there.
//
// It is not simply there. It is built from contributor registrations at runtime, and a model that failed to
// configure, a namespace filter that stopped matching after a move, or a renamed assembly all produce an
// empty walk -- from which **every ban over it passes.**
//
// ---- ⚠ ONE FLOOR PER LAYER, BECAUSE A FLOOR OVER A UNION CANNOT SEE ONE MEMBER COLLAPSE.
//
// This was learned the expensive way in T-263: a guard floored `fields.Concat(properties)` as a single
// number, and breaking the field walk left the property walk clearing the floor by itself while a
// field-held offender went undetected. **The entity walk and the property walk are two layers and each
// gets its own floor**, so that the layer that collapses is the layer that is named.
//
// ---- AND A FLOOR IS NOT A CONTROL, WHICH IS WHY THIS FILE DELIBERATELY OFFERS ONLY HALF.
//
// These floors prove the MODEL was read. They cannot prove a PREDICATE still matches, because each ban
// filters the same walk differently -- `IsUnicode() == false` and `GetPrecision() != 19` share a root and
// share nothing else. **A shared floor cannot discharge a per-predicate control, and there is no shared
// helper here that pretends otherwise.** Each ban asserts its own filter still selects real rows, next to
// the ban, where the person changing the filter is looking.
// ---- AND THE METHOD NAMES CARRY `Floored` BECAUSE THE CALL SITE IS WHERE THE GUARD IS JUDGED.
//
// These were `Entities` and `Properties`, which is accurate and exactly the problem: the call site read
// as data access, so nothing there said an assertion had happened. **An audit scanner run over the two
// files fixed by this very helper reported them as unprotected**, because the floor had moved to another
// file. A reader at the call site would have concluded the same thing, and correctly.
//
// A comment in here is a note that must be SOUGHT. A name is a note that is READ.
internal static class ModelWalk
{
  // ==================================================================================================
  // ⚠⚠⚠ CHOOSING `floor` — THE COLLAPSE COMES FIRST, OR THE NUMBER IS ARBITRARY BY CONSTRUCTION (T-099)
  // ==================================================================================================
  //
  // **You cannot derive a floor from the actual value. Only from a failure you can name.** Every floor in
  // this suite that does real work was chosen by someone who knew the collapse BEFORE picking the number —
  // `BranchTransfer`'s 30 sits above the TWO that a contributor-free model yields, which is a regression
  // that really happened. Every floor that binds nothing was a round number chosen after watching the
  // actual value pass.
  //
  // ---- ⚠⚠ A RUNNABLE TELL, AND ITS LIMIT, BECAUSE AN UNQUALIFIED ONE BECOMES THE NEXT FALSE CLAIM.
  //
  // Audited 2026-09-07 across the floors set that week: **every arbitrary floor was a round decimal — 40,
  // 80, 200, 100, 400, 500, 30 — measured against actuals of 134, 548, 2482, 173, 1181, 1537.** One was
  // 200 against 2482: it could not fire until 92% of the population had vanished.
  //
  // ⚠ THE TELL IS DIRECTIONAL, NOT EXACT. Two of the SIZED floors are also round — 30 and 10 — so a round
  // number is a PROMPT, not a verdict. **Grep for round literals in floor positions and ask each one what
  // collapse it names; if the answer is "it passed", it is arbitrary.** That is a cheap audit and it is
  // the only one of this week's findings that can be re-run mechanically.
  //
  // ---- SO, WRITING A NEW FLOOR: name the collapse, measure the actual, DATE it, and put all three in the
  // comment. An undated floor is a claim with an expiry and no label on it.

  // The entity layer. `name` is what appears when it fails, because "0 entities" is useless without
  // knowing which walk produced it.
  public static IEntityType[] FlooredEntities(IEnumerable<IEntityType> walk, string name, int floor)
  {
    var entities = walk.ToArray();

    Assert.True(entities.Length >= floor,
      $"the {name} walk found {entities.Length} entity types, below the floor of {floor}. The model failed " +
      "to build, or the filter selecting this module's entities has stopped matching after a move or a " +
      "rename. Every ban over this walk would pass by inspecting nothing.");

    return entities;
  }

  // The property layer, floored separately from the entities that yielded it: a healthy entity list whose
  // properties come back empty is a different failure and must say so.
  public static (IEntityType Entity, IProperty Property)[] FlooredProperties(
    IEntityType[] entities, string name, int floor)
  {
    var properties = entities
      .SelectMany(entity => entity.GetProperties().Select(property => (Entity: entity, Property: property)))
      .ToArray();

    Assert.True(properties.Length >= floor,
      $"the {name} walk found {entities.Length} entity types but only {properties.Length} properties, below " +
      $"the floor of {floor}. The PROPERTY layer has collapsed rather than the entity layer, and a ban over " +
      "columns would pass while the entity count still looked healthy.");

    return properties;
  }
}
