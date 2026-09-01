using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Tests.Positions;

// THE POSITION AGGREGATE'S LOCAL INVARIANTS (FP-008 Phase 1, DEC-POS-0001/0007/0011).
//
// ---- WHAT IS DELIBERATELY NOT TESTED HERE.
//
// That a referenced job grade exists, belongs to the same company, and is `Active` (`BRULE-POS-0009`) are
// cross-aggregate rules requiring repository lookups. They are Phase 2, they are NOT implemented, and there
// is no test here asserting them — because a test that passed against unimplemented behaviour would be
// asserting the absence of enforcement while reading like its presence.
//
// The same applies to "an inactive position refuses a new assignment" (`BRULE-POS-0013`): the refusal
// belongs to the operation doing the assigning, which is Phase 2 and Phase 3.
public sealed class PositionDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

  private const string Actor = "tester";

  [Fact]
  public void A_valid_position_is_created_active_and_ungraded()
  {
    var position = CreatePosition("ACC-SR", "Senior Accountant");

    Assert.True(position.IsSuccess);
    Assert.Equal(PositionStatus.Active, position.Value.Status);
    Assert.Null(position.Value.JobGradeId);
    Assert.Equal(Actor, position.Value.StatusChangedBy);
    Assert.Equal(Now, position.Value.StatusChangedUtc);
    Assert.NotEqual(Guid.Empty, position.Value.PositionId);
  }

  [Fact]
  public void A_position_may_be_created_already_graded()
  {
    var jobGradeId = Guid.NewGuid();

    var position = CreatePosition("ACC-SR", "Senior Accountant", jobGradeId);

    Assert.True(position.IsSuccess);
    Assert.Equal(jobGradeId, position.Value.JobGradeId);
  }

  // ---- CODE NORMALIZATION. The stored display value keeps its casing; the normalized value does not.
  [Theory]
  [InlineData("  acc-sr  ", "acc-sr", "ACC-SR")]
  [InlineData("Acc-Sr", "Acc-Sr", "ACC-SR")]
  [InlineData("ACC-SR", "ACC-SR", "ACC-SR")]
  public void The_code_is_trimmed_for_display_and_upper_cased_for_comparison(
    string input, string expectedValue, string expectedNormalized)
  {
    var position = CreatePosition(input, "Senior Accountant");

    Assert.True(position.IsSuccess);
    Assert.Equal(expectedValue, position.Value.Code.Value);
    Assert.Equal(expectedNormalized, position.Value.NormalizedCode);
  }

  // TWO CODES THAT NORMALIZE ALIKE ARE THE SAME CODE. This is what makes the binary-collated unique index
  // authoritative rather than advisory — the database and the value object agree on what collides.
  [Theory]
  [InlineData("acc-sr", "ACC-SR")]
  [InlineData(" ACC-SR ", "acc-sr")]
  [InlineData("Acc-Sr", "aCC-sR")]
  public void Codes_that_normalize_alike_are_equal(string left, string right)
  {
    var first = PositionCode.Create(left);
    var second = PositionCode.Create(right);

    Assert.True(first.IsSuccess);
    Assert.True(second.IsSuccess);
    Assert.Equal(first.Value, second.Value);
    Assert.Equal(first.Value.NormalizedValue, second.Value.NormalizedValue);
  }

  // NO UNICODE NORMALIZATION IS APPLIED, and that is deliberate: two visually identical values differing in
  // composition are different codes. If this ever starts failing, the index and the value object have
  // stopped agreeing on what collides.
  [Fact]
  public void Codes_differing_only_in_unicode_composition_do_not_collide()
  {
    // Written as escapes rather than as literal text, so the two cannot be silently normalized to the same
    // bytes by an editor, a checkout, or a copy-paste — which would make the test pass by accident.
    var precomposed = PositionCode.Create("CAF\u00C9");        // one code point
    var decomposed = PositionCode.Create("CAFE\u0301");        // E + combining acute

    Assert.True(precomposed.IsSuccess);
    Assert.True(decomposed.IsSuccess);
    Assert.NotEqual(precomposed.Value, decomposed.Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("bad\tcode")]
  public void An_invalid_code_is_refused(string? code)
  {
    var result = PositionCode.Create(code);

    Assert.True(result.IsFailure);
    Assert.Equal(PositionErrors.InvalidCode, result.Error);
  }

  [Fact]
  public void A_code_longer_than_the_column_is_refused()
  {
    var result = PositionCode.Create(new string('A', PositionCode.MaximumLength + 1));

    Assert.True(result.IsFailure);
    Assert.Equal(PositionErrors.InvalidCode, result.Error);
  }

  // THE BOUNDARY IS INCLUSIVE. A code of exactly the column width is accepted; one character more is not.
  //
  // A draft of this test asserted that a value fitting BEFORE normalization and not after is refused, using
  // U+00DF (ß) on the belief that uppercasing expands it. **It does not on .NET** — `ToUpperInvariant` uses
  // simple 1:1 case mapping and never changes a string's length — so the assertion failed and was removed
  // rather than reworded around a different character. The guard in `OrganizationalText` stays as defensive
  // code and now says so. Asserting an unreachable branch would be a test that could only ever pass for the
  // wrong reason.
  [Fact]
  public void A_code_of_exactly_the_maximum_length_is_accepted()
  {
    var atLimit = PositionCode.Create(new string('A', PositionCode.MaximumLength));

    Assert.True(atLimit.IsSuccess);
    Assert.Equal(PositionCode.MaximumLength, atLimit.Value.NormalizedValue.Length);
  }

  // Normalization does not change what fits: a lower-case code at the limit survives upper-casing.
  [Fact]
  public void A_lower_case_code_at_the_limit_survives_normalization()
  {
    var atLimit = PositionCode.Create(new string('a', PositionCode.MaximumLength));

    Assert.True(atLimit.IsSuccess);
    Assert.Equal(new string('A', PositionCode.MaximumLength), atLimit.Value.NormalizedValue);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("bad\ntitle")]
  public void An_invalid_title_is_refused(string? title)
  {
    var result = PositionTitle.Create(title);

    Assert.True(result.IsFailure);
    Assert.Equal(PositionErrors.InvalidTitle, result.Error);
  }

  // THE TITLE IS NOT UNIQUE AND NOT NORMALIZED. Two positions may share one; the code distinguishes them.
  [Fact]
  public void Titles_differing_only_in_casing_are_different_titles()
  {
    var first = PositionTitle.Create("Senior Accountant");
    var second = PositionTitle.Create("SENIOR ACCOUNTANT");

    Assert.True(first.IsSuccess);
    Assert.True(second.IsSuccess);
    Assert.NotEqual(first.Value, second.Value);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void An_invalid_actor_is_refused(string? actor)
  {
    var result = Position.Create(
      PositionCode.Create("ACC-SR").Value,
      PositionTitle.Create("Senior Accountant").Value,
      jobGradeId: null,
      actor!,
      Guid.NewGuid(),
      Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PositionErrors.InvalidActor, result.Error);
  }

  // AN EMPTY GUID IS NOT A GRADE — and it is distinct from "no grade", which is null and is legal.
  [Fact]
  public void An_empty_job_grade_reference_is_refused_but_a_null_one_is_not()
  {
    var empty = CreatePosition("ACC-SR", "Senior Accountant", Guid.Empty);
    var absent = CreatePosition("ACC-SR", "Senior Accountant", jobGradeId: null);

    Assert.True(empty.IsFailure);
    Assert.Equal(PositionErrors.InvalidGradeReference, empty.Error);
    Assert.True(absent.IsSuccess);
  }

  // ---- UPDATE. Code, title and grade; nothing else.
  [Fact]
  public void Updating_the_description_replaces_code_title_and_grade()
  {
    var position = CreatePosition("ACC-SR", "Senior Accountant").Value;
    var newGradeId = Guid.NewGuid();

    var result = position.UpdateDescription(
      PositionCode.Create("acc-lead").Value,
      PositionTitle.Create("Lead Accountant").Value,
      newGradeId,
      Guid.NewGuid(),
      Now);

    Assert.True(result.IsSuccess);
    Assert.Equal("acc-lead", position.Code.Value);
    Assert.Equal("ACC-LEAD", position.NormalizedCode);
    Assert.Equal("Lead Accountant", position.Title.Value);
    Assert.Equal(newGradeId, position.JobGradeId);
  }

  [Fact]
  public void Updating_the_description_does_not_change_the_status()
  {
    var position = CreatePosition("ACC-SR", "Senior Accountant").Value;
    position.Deactivate(Actor, Guid.NewGuid(), Now);

    position.UpdateDescription(
      PositionCode.Create("ACC-SR").Value,
      PositionTitle.Create("Renamed").Value,
      jobGradeId: null,
      Guid.NewGuid(),
      Now);

    Assert.Equal(PositionStatus.Inactive, position.Status);
  }

  // Re-grading to null is how a position is UNGRADED. The alternative would be that a mistaken grade can
  // never be withdrawn, only replaced with a different guess.
  [Fact]
  public void A_position_may_be_ungraded_by_updating_to_a_null_grade()
  {
    var position = CreatePosition("ACC-SR", "Senior Accountant", Guid.NewGuid()).Value;

    position.UpdateDescription(
      PositionCode.Create("ACC-SR").Value,
      PositionTitle.Create("Senior Accountant").Value,
      jobGradeId: null,
      Guid.NewGuid(),
      Now);

    Assert.Null(position.JobGradeId);
  }

  // ---- LIFECYCLE.
  [Fact]
  public void Deactivating_and_reactivating_records_who_and_when()
  {
    var position = CreatePosition("ACC-SR", "Senior Accountant").Value;
    var later = Now.AddHours(3);

    var deactivated = position.Deactivate("closer", Guid.NewGuid(), later);

    Assert.True(deactivated.IsSuccess);
    Assert.Equal(PositionStatus.Inactive, position.Status);
    Assert.Equal("closer", position.StatusChangedBy);
    Assert.Equal(later, position.StatusChangedUtc);

    var reactivated = position.Reactivate("reopener", Guid.NewGuid(), later.AddHours(1));

    Assert.True(reactivated.IsSuccess);
    Assert.Equal(PositionStatus.Active, position.Status);
    Assert.Equal("reopener", position.StatusChangedBy);
  }

  [Fact]
  public void Deactivating_an_inactive_position_is_refused()
  {
    var position = CreatePosition("ACC-SR", "Senior Accountant").Value;
    position.Deactivate(Actor, Guid.NewGuid(), Now);

    var result = position.Deactivate(Actor, Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PositionErrors.InvalidTransition, result.Error);
  }

  [Fact]
  public void Reactivating_an_active_position_is_refused()
  {
    var position = CreatePosition("ACC-SR", "Senior Accountant").Value;

    var result = position.Reactivate(Actor, Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PositionErrors.InvalidTransition, result.Error);
  }

  [Fact]
  public void A_lifecycle_transition_without_a_trusted_actor_is_refused()
  {
    var position = CreatePosition("ACC-SR", "Senior Accountant").Value;

    var result = position.Deactivate("   ", Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(PositionErrors.InvalidActor, result.Error);
    Assert.Equal(PositionStatus.Active, position.Status);
  }

  // ---- THE ONE THING THIS AGGREGATE MUST NOT DO (OD-POS-005, BRULE-POS-0014).
  //
  // "One ACTIVE position" qualifies the ASSIGNMENT, not the position's lifecycle status. Deactivation is
  // therefore a pure state transition that consults nothing — no incumbent lookup, no refusal, no
  // `position.has_incumbents`. Had the other reading been ruled, this method would need a repository.
  //
  // The assertion is structural rather than behavioural because there is nothing behavioural to observe:
  // the guarantee is that `Deactivate` takes only an actor, an event id and a time, so it CANNOT consult
  // incumbents even if a later edit wanted it to.
  [Fact]
  public void Deactivation_cannot_consult_incumbents_because_it_is_given_nothing_to_consult()
  {
    var parameters = typeof(Position)
      .GetMethod(nameof(Position.Deactivate))!
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Equal([typeof(string), typeof(Guid), typeof(DateTimeOffset)], parameters);
  }

  // ---- OWNERSHIP CLASSIFICATION (DEC-POS-0001). The absence is asserted, not assumed.
  [Fact]
  public void The_position_aggregate_is_tenant_and_company_owned_and_never_branch_owned()
  {
    var interfaces = typeof(Position).GetInterfaces().Select(type => type.Name).ToArray();

    Assert.Contains("ITenantOwnedEntity", interfaces);
    Assert.Contains("ICompanyOwnedEntity", interfaces);
    Assert.DoesNotContain(nameof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity), interfaces);
    Assert.Null(typeof(Position).GetProperty(nameof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity.BranchId)));
  }

  // ---- THE CYCLE TRAP (DEC-POS-0002). One convenience column breaks cutover for every tenant.
  [Fact]
  public void The_position_aggregate_has_no_reference_to_any_employee()
  {
    var employeeShaped = typeof(Position)
      .GetProperties()
      .Where(property => property.Name.Contains("Employee", StringComparison.Ordinal))
      .Select(property => property.Name)
      .ToArray();

    Assert.Empty(employeeShaped);
  }

  // ---- INDEPENDENCE FROM DEPARTMENT (OD-POS-003). One authority on an employee's department, not two.
  [Fact]
  public void The_position_aggregate_has_no_department_reference()
  {
    Assert.Null(typeof(Position).GetProperty(nameof(SSAS.HR.Domain.Employees.Employee.DepartmentId)));
  }

  private static Result<Position> CreatePosition(
    string code, string title, Guid? jobGradeId = null)
  {
    var positionCode = PositionCode.Create(code);
    if (positionCode.IsFailure)
    {
      return Result.Failure<Position>(positionCode.Error);
    }

    return Position.Create(
      positionCode.Value,
      PositionTitle.Create(title).Value,
      jobGradeId,
      Actor,
      Guid.NewGuid(),
      Now);
  }
}
