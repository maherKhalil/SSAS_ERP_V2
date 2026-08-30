using SSAS.GL.API;
using SSAS.Platform.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// GL'S UNCLASSIFIED UNIQUE VIOLATION IS A CONFLICT THAT NAMES NO INDEX (T-245).
// ==================================================================================================
//
// `DEC-DEP-0027` as amended. The arm exists; these assert the two properties the amendment turns on, and
// they are separable — one is about the STATUS being right and the other about the MESSAGE not lying.
//
// ---- WHY BOTH, AND WHY NEITHER ALONE WOULD DO.
//
// **T-165's objection was entirely about the message.** GL has six unique indexes and one arm cannot know
// which lost, so a code naming any of them would be false five times in six. That objection was right and
// still binds: the second test is what holds it.
//
// **What T-245 overturned was the conclusion drawn from it** — that the 500 default was therefore correct.
// A 500 asserts the server broke. The first test is what holds that.
//
// Mapping this onto `gl.conflict` would satisfy the first and quietly break nothing visible, which is why
// the code is asserted by name: the generic arm needs to stay DISTINGUISHABLE from a classified conflict,
// so an operator can see how often a path nobody translated is firing.
public sealed class GlUniqueConflictMappingTests
{
  [Fact]
  public void An_unclassified_unique_violation_is_a_conflict_rather_than_a_server_fault()
  {
    var mapped = GlApiErrorMapper.Map(IdentityAccessErrors.UniqueConstraintViolation);

    Assert.Equal(409, mapped.StatusCode);
  }

  [Fact]
  public void It_names_no_index_and_stays_distinct_from_a_classified_conflict()
  {
    var mapped = GlApiErrorMapper.Map(IdentityAccessErrors.UniqueConstraintViolation);

    Assert.Equal("gl.unique_conflict", mapped.Code);

    // ⚠ The six indexes T-165 enumerated. None may appear in a code this switch cannot verify — that is
    // the half of `DEC-DEP-0027` the amendment left standing.
    foreach (var index in new[] { "account", "fiscal", "line", "number", "reversal", "journal" })
    {
      Assert.DoesNotContain(index, mapped.Code, StringComparison.OrdinalIgnoreCase);
    }
  }
}
