using SSAS.BuildingBlocks.SharedKernel;
using SSAS.Platform.Domain;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE COPY MUST EQUAL THE ORIGINAL (T-166).
// ==================================================================================================
//
// `PersistenceErrorCodes` quotes codes that `IdentityAccessErrors` declares. **A module under
// `src/Modules` cannot reference `SSAS.Platform.Domain` (`ADR-012`), so it compares on the string** — and
// the constant is the only safety a string comparison can have, because a literal is a typo away from
// silently never matching.
//
// ---- ⚠ THIS TEST IS THE CONDITION ON THAT FILE EXISTING, NOT A NICETY BESIDE IT.
//
// **The moment the code is recorded in two places, `DEC-L-080` says it goes stale in one.** Nothing else
// would notice: a constant that drifts from the declaration produces no compiler error and no failing
// handler test — the guarded branch simply stops being taken, and the caller quietly gets the default arm
// again. **That is the 500 this whole task existed to remove, returning by a different route.**
//
// ---- WHAT THIS DOES NOT CLAIM.
//
// It does not prove the unit of work RETURNS these errors — `TenantUnitOfWork` mapping SQL 2601/2627 to
// `UniqueConstraintViolation` is its own code and its own test. **This proves only that the two spellings
// of each code agree**, which is the half that can drift silently.
public sealed class PersistenceErrorCodeParityTests
{
  [Theory]
  [Trait("Decision", "DEC-L-080")]
  [InlineData(PersistenceErrorCodes.UniqueConstraint, nameof(IdentityAccessErrors.UniqueConstraintViolation))]
  [InlineData(PersistenceErrorCodes.ConcurrencyConflict, nameof(IdentityAccessErrors.ConcurrencyConflict))]
  [InlineData(PersistenceErrorCodes.WriteFailure, nameof(IdentityAccessErrors.WriteFailure))]
  public void Every_quoted_code_equals_the_error_that_declares_it(string quoted, string declaringMember)
  {
    var declared = typeof(IdentityAccessErrors)
      .GetField(declaringMember)?
      .GetValue(null) as SSAS.BuildingBlocks.Domain.Error;

    // Without this the comparison below passes when the member is renamed out from under the constant —
    // the drift this test exists to catch, arriving as a null rather than a mismatch (`DEC-L-070`).
    Assert.NotNull(declared);

    Assert.Equal(declared!.Code, quoted);
  }

  // ---- AND THE QUOTED SET MUST NOT GROW SILENTLY.
  //
  // A fourth constant added without a line in the theory above is unchecked, and unchecked is exactly the
  // state this file exists to prevent. **Asserting the count means adding one is a decision** — the same
  // reasoning as `The_named_documents_are_every_contract_document_on_disk`.
  [Fact]
  public void The_quoted_set_is_exactly_what_this_test_checks()
  {
    var quoted = typeof(PersistenceErrorCodes)
      .GetFields()
      .Where(field => field is { IsLiteral: true, IsInitOnly: false })
      .Select(field => (string)field.GetRawConstantValue()!)
      .OrderBy(code => code, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(
      [
        PersistenceErrorCodes.ConcurrencyConflict,
        PersistenceErrorCodes.UniqueConstraint,
        PersistenceErrorCodes.WriteFailure
      ],
      quoted);
  }
}
