using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Tests.Positions;

// THE TWO GRADE LADDERS AND THE SALARY BAND (FP-008 Phase 1, DEC-POS-0005/0006/0016/0027).
//
// ---- WHAT IS DELIBERATELY NOT TESTED HERE.
//
// "A grade with active dependents cannot be deactivated" (`DEC-POS-0013`) is a cross-aggregate rule needing
// a repository lookup. The OPERATION is Phase 2 and is NOT implemented; only the error constant exists.
// There is no test here asserting the refusal, because a test that passed against unimplemented behaviour
// would assert the absence of enforcement while reading like its presence.
//
// Rank-order UNIQUENESS is likewise absent: it is a unique index, proven against real SQL in
// Integration.Tests, not by a unit test over an aggregate that cannot see its siblings.
public sealed class GradeDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

  private const string Actor = "tester";

  // ================================================================================================
  // RANK ORDER IS DATA, NOT A READING OF THE CODE (DEC-POS-0006)
  // ================================================================================================

  // THE LEXICAL TRAP, MADE EXECUTABLE (TS-POS-0016). "G10" sorts BEFORE "G9" under an ordinal comparison,
  // so a ladder ordered by its codes is ordered wrongly the moment it reaches ten grades. Without
  // `RankOrder` this test cannot pass.
  [Fact]
  public void Rank_order_orders_a_ladder_that_the_code_orders_wrongly()
  {
    var ninth = CreateJobGrade("G9", "Grade 9", rankOrder: 90).Value;
    var tenth = CreateJobGrade("G10", "Grade 10", rankOrder: 100).Value;

    // What the code would say, and it is wrong.
    Assert.True(string.CompareOrdinal(tenth.NormalizedCode, ninth.NormalizedCode) < 0);

    // What the data says, and it is right.
    Assert.True(ninth.RankOrder < tenth.RankOrder);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [InlineData(int.MinValue)]
  public void A_non_positive_rank_order_is_refused(int rankOrder)
  {
    var jobGrade = CreateJobGrade("G1", "Grade 1", rankOrder);
    var salaryGrade = CreateSalaryGrade("S1", "Band 1", rankOrder);

    Assert.True(jobGrade.IsFailure);
    Assert.Equal(PositionErrors.InvalidRankOrder, jobGrade.Error);
    Assert.True(salaryGrade.IsFailure);
    Assert.Equal(PositionErrors.InvalidRankOrder, salaryGrade.Error);
  }

  // SPARSENESS IS THE INTENT, NOT A RULE. Consecutive ranks are legal and merely inconvenient; a rule
  // requiring gaps would be one no requirement asks for.
  [Fact]
  public void Consecutive_rank_orders_are_legal()
  {
    Assert.True(CreateJobGrade("G1", "Grade 1", rankOrder: 1).IsSuccess);
    Assert.True(CreateJobGrade("G2", "Grade 2", rankOrder: 2).IsSuccess);
  }

  // ================================================================================================
  // THE SALARY BAND IS ATOMIC (DEC-POS-0027)
  // ================================================================================================

  [Fact]
  public void A_band_with_all_three_amounts_is_accepted()
  {
    var band = SalaryBand.Create(12000m, 15000m, 18000m);

    Assert.True(band.IsSuccess);
    Assert.NotNull(band.Value);
    Assert.Equal(12000m, band.Value!.MinimumAmount);
    Assert.Equal(15000m, band.Value.MidpointAmount);
    Assert.Equal(18000m, band.Value.MaximumAmount);
  }

  // ABSENCE IS A LEGAL ANSWER AND IT IS NOT AN ERROR — which is why `Create` returns `Result<SalaryBand?>`.
  // A caller that could not tell "unpriced" from "invalid" would have to guess, and the guess would be
  // wrong in exactly the case that matters.
  [Fact]
  public void A_band_with_no_amounts_is_a_successful_absence_not_a_failure()
  {
    var band = SalaryBand.Create(null, null, null);

    Assert.True(band.IsSuccess);
    Assert.Null(band.Value);
  }

  // ---- ONE OR TWO AMOUNTS IS THE REFUSED CASE. All six partial combinations, not a representative one:
  // an atomicity rule that held for five of six combinations would be no rule at all.
  [Theory]
  [InlineData(1.0, null, null)]
  [InlineData(null, 2.0, null)]
  [InlineData(null, null, 3.0)]
  [InlineData(1.0, 2.0, null)]
  [InlineData(1.0, null, 3.0)]
  [InlineData(null, 2.0, 3.0)]
  public void A_partially_specified_band_is_refused(double? minimum, double? midpoint, double? maximum)
  {
    var band = SalaryBand.Create(
      (decimal?)minimum, (decimal?)midpoint, (decimal?)maximum);

    Assert.True(band.IsFailure);
    Assert.Equal(PositionErrors.SalaryBandIncomplete, band.Error);
  }

  // A PARTIAL BAND HAS NO ORDERING TO BE WRONG ABOUT, so it must answer "incomplete" rather than
  // "out of order" — otherwise the refusal names the wrong defect and the caller fixes the wrong field.
  [Fact]
  public void A_partial_band_that_is_also_out_of_order_answers_incomplete()
  {
    var band = SalaryBand.Create(99000m, 1000m, null);

    Assert.True(band.IsFailure);
    Assert.Equal(PositionErrors.SalaryBandIncomplete, band.Error);
  }

  [Theory]
  [InlineData(18000, 15000, 12000)]
  [InlineData(12000, 18000, 15000)]
  [InlineData(15000, 12000, 18000)]
  public void A_band_out_of_order_is_refused(int minimum, int midpoint, int maximum)
  {
    var band = SalaryBand.Create(minimum, midpoint, maximum);

    Assert.True(band.IsFailure);
    Assert.Equal(PositionErrors.SalaryBandOutOfOrder, band.Error);
  }

  // NON-STRICT ORDERING. A band whose three amounts are equal is a fixed-rate grade — a real structure, and
  // refusing it would be a rule no requirement asks for.
  [Fact]
  public void A_single_point_band_is_accepted()
  {
    var band = SalaryBand.Create(15000m, 15000m, 15000m);

    Assert.True(band.IsSuccess);
    Assert.NotNull(band.Value);
  }

  [Theory]
  [InlineData(-1, 15000, 18000)]
  [InlineData(12000, -1, 18000)]
  [InlineData(12000, 15000, -1)]
  public void A_negative_amount_is_refused(int minimum, int midpoint, int maximum)
  {
    var band = SalaryBand.Create(minimum, midpoint, maximum);

    Assert.True(band.IsFailure);
    Assert.Equal(PositionErrors.SalaryBandNegative, band.Error);
  }

  // Zero is not negative. A grade band starting at zero is unusual but it is not invalid, and the rule the
  // package states is non-negativity rather than positivity.
  [Fact]
  public void A_zero_amount_is_accepted()
  {
    var band = SalaryBand.Create(0m, 0m, 1m);

    Assert.True(band.IsSuccess);
    Assert.NotNull(band.Value);
  }

  // NEGATIVITY IS CHECKED BEFORE ORDERING. A band that is both negative and out of order answers the
  // negative case, because that is the defect the caller must fix first.
  [Fact]
  public void A_band_that_is_both_negative_and_out_of_order_answers_negative()
  {
    var band = SalaryBand.Create(-5m, -10m, -20m);

    Assert.True(band.IsFailure);
    Assert.Equal(PositionErrors.SalaryBandNegative, band.Error);
  }

  // FOUR DECIMAL PLACES SURVIVE (ADR-027 decision 1). The scale exists for three-decimal currencies with a
  // guard digit; a value object that rounded would defeat the column.
  [Fact]
  public void A_band_preserves_four_decimal_places()
  {
    var band = SalaryBand.Create(1234.5678m, 2345.6789m, 3456.7891m);

    Assert.True(band.IsSuccess);
    Assert.Equal(1234.5678m, band.Value!.MinimumAmount);
    Assert.Equal(3456.7891m, band.Value.MaximumAmount);
  }

  [Fact]
  public void Bands_with_the_same_amounts_are_equal()
  {
    var first = SalaryBand.Create(1m, 2m, 3m);
    var second = SalaryBand.Create(1m, 2m, 3m);

    Assert.Equal(first.Value, second.Value);
  }

  // ================================================================================================
  // GRADE IDENTITY AND LIFECYCLE
  // ================================================================================================

  [Fact]
  public void A_valid_job_grade_is_created_active()
  {
    var grade = CreateJobGrade("G7", "Grade 7", rankOrder: 70);

    Assert.True(grade.IsSuccess);
    Assert.Equal(JobGradeStatus.Active, grade.Value.Status);
    Assert.Equal(70, grade.Value.RankOrder);
    Assert.Null(grade.Value.SalaryGradeId);
    Assert.NotEqual(Guid.Empty, grade.Value.JobGradeId);
  }

  [Fact]
  public void A_valid_salary_grade_is_created_active_and_unpriced()
  {
    var grade = CreateSalaryGrade("S7", "Band 7", rankOrder: 70);

    Assert.True(grade.IsSuccess);
    Assert.Equal(SalaryGradeStatus.Active, grade.Value.Status);
    Assert.Null(grade.Value.Band);
  }

  [Fact]
  public void A_salary_grade_may_be_created_priced()
  {
    var band = SalaryBand.Create(12000m, 15000m, 18000m).Value;

    var grade = CreateSalaryGrade("S7", "Band 7", rankOrder: 70, band: band);

    Assert.True(grade.IsSuccess);
    Assert.NotNull(grade.Value.Band);
    Assert.Equal(15000m, grade.Value.Band!.MidpointAmount);
  }

  // A BAND MAY BE WITHDRAWN, not only replaced. The alternative would be that a mistaken band can never be
  // removed, only overwritten with a different guess.
  [Fact]
  public void A_salary_grade_may_be_unpriced_again()
  {
    var band = SalaryBand.Create(12000m, 15000m, 18000m).Value;
    var grade = CreateSalaryGrade("S7", "Band 7", rankOrder: 70, band: band).Value;

    grade.UpdateDescription(
      SalaryGradeCode.Create("S7").Value,
      SalaryGradeName.Create("Band 7").Value,
      rankOrder: 70,
      band: null,
      Guid.NewGuid(),
      Now);

    Assert.Null(grade.Band);
  }

  [Theory]
  [InlineData("  g7  ", "g7", "G7")]
  [InlineData("G7", "G7", "G7")]
  public void A_job_grade_code_is_trimmed_for_display_and_upper_cased_for_comparison(
    string input, string expectedValue, string expectedNormalized)
  {
    var grade = CreateJobGrade(input, "Grade 7", rankOrder: 70);

    Assert.True(grade.IsSuccess);
    Assert.Equal(expectedValue, grade.Value.Code.Value);
    Assert.Equal(expectedNormalized, grade.Value.NormalizedCode);
  }

  // THE THREE CODE TYPES ARE UNRELATED, and that is what stops one being passed where another belongs.
  // Shared MECHANICS, distinct TYPES (`OrganizationalText`).
  [Fact]
  public void The_three_code_types_are_not_assignable_to_one_another()
  {
    Assert.False(typeof(PositionCode).IsAssignableFrom(typeof(JobGradeCode)));
    Assert.False(typeof(JobGradeCode).IsAssignableFrom(typeof(SalaryGradeCode)));
    Assert.False(typeof(SalaryGradeCode).IsAssignableFrom(typeof(PositionCode)));
  }

  // ...but they must agree on what COLLIDES, or the unique index enforces one answer while the pre-check
  // gives another.
  [Fact]
  public void The_three_code_types_normalize_identically()
  {
    const string Raw = "  g7-a  ";

    Assert.Equal("G7-A", PositionCode.Create(Raw).Value.NormalizedValue);
    Assert.Equal("G7-A", JobGradeCode.Create(Raw).Value.NormalizedValue);
    Assert.Equal("G7-A", SalaryGradeCode.Create(Raw).Value.NormalizedValue);
  }

  [Fact]
  public void Each_grade_code_refusal_names_its_own_ladder()
  {
    Assert.Equal(PositionErrors.InvalidJobGradeCode, JobGradeCode.Create("  ").Error);
    Assert.Equal(PositionErrors.InvalidSalaryGradeCode, SalaryGradeCode.Create("  ").Error);
    Assert.Equal(PositionErrors.InvalidJobGradeName, JobGradeName.Create("  ").Error);
    Assert.Equal(PositionErrors.InvalidSalaryGradeName, SalaryGradeName.Create("  ").Error);
  }

  [Fact]
  public void Grade_lifecycle_transitions_are_reversible_and_refuse_repeats()
  {
    var jobGrade = CreateJobGrade("G7", "Grade 7", rankOrder: 70).Value;

    Assert.True(jobGrade.Deactivate(Actor, Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(JobGradeStatus.Inactive, jobGrade.Status);
    Assert.Equal(PositionErrors.InvalidTransition,
      jobGrade.Deactivate(Actor, Guid.NewGuid(), Now).Error);

    Assert.True(jobGrade.Reactivate(Actor, Guid.NewGuid(), Now).IsSuccess);
    Assert.Equal(JobGradeStatus.Active, jobGrade.Status);
    Assert.Equal(PositionErrors.InvalidTransition,
      jobGrade.Reactivate(Actor, Guid.NewGuid(), Now).Error);
  }

  [Fact]
  public void A_salary_grade_lifecycle_transition_records_who_and_when()
  {
    var grade = CreateSalaryGrade("S7", "Band 7", rankOrder: 70).Value;
    var later = Now.AddHours(2);

    grade.Deactivate("closer", Guid.NewGuid(), later);

    Assert.Equal(SalaryGradeStatus.Inactive, grade.Status);
    Assert.Equal("closer", grade.StatusChangedBy);
    Assert.Equal(later, grade.StatusChangedUtc);
  }

  // ---- OWNERSHIP CLASSIFICATION (DEC-POS-0001). Asserted for both ladders, not assumed from Position.
  [Theory]
  [InlineData(typeof(JobGrade))]
  [InlineData(typeof(SalaryGrade))]
  public void Both_grade_aggregates_are_tenant_and_company_owned_and_never_branch_owned(Type type)
  {
    var interfaces = type.GetInterfaces().Select(contract => contract.Name).ToArray();

    Assert.Contains("ITenantOwnedEntity", interfaces);
    Assert.Contains("ICompanyOwnedEntity", interfaces);
    Assert.DoesNotContain(nameof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity), interfaces);
    Assert.Null(type.GetProperty(nameof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity.BranchId)));
  }

  // ================================================================================================
  // THE REFERENCE RUNS ONE WAY: JobGrade -> SalaryGrade, AND NEVER BACK (DEC-POS-0002, AC-POS-0017)
  // ================================================================================================
  //
  // A `SalaryGrade -> JobGrade` reference would restore the foreign-key cycle that makes
  // `TenantCutoverCopyPlan.Build` return `CutoverCopyOrderUndecidable`. The composed-model proof lives in
  // Architecture.Tests; this is the domain half, and it fails the moment somebody adds the convenient
  // back-pointer.
  [Fact]
  public void A_salary_grade_holds_no_reference_to_a_job_grade()
  {
    Assert.Null(typeof(SalaryGrade).GetProperty(nameof(SSAS.HR.Domain.Positions.Position.JobGradeId)));
    Assert.NotNull(typeof(JobGrade).GetProperty("SalaryGradeId"));
  }

  [Fact]
  public void Neither_grade_aggregate_references_an_employee()
  {
    foreach (var type in new[] { typeof(JobGrade), typeof(SalaryGrade) })
    {
      Assert.Empty(type.GetProperties()
        .Where(property => property.Name.Contains("Employee", StringComparison.Ordinal)));
    }
  }

  private static Result<JobGrade> CreateJobGrade(
    string code, string name, int rankOrder, Guid? salaryGradeId = null)
  {
    var gradeCode = JobGradeCode.Create(code);
    if (gradeCode.IsFailure)
    {
      return Result.Failure<JobGrade>(gradeCode.Error);
    }

    return JobGrade.Create(
      gradeCode.Value,
      JobGradeName.Create(name).Value,
      rankOrder,
      salaryGradeId,
      Actor,
      Guid.NewGuid(),
      Now);
  }

  private static Result<SalaryGrade> CreateSalaryGrade(
    string code, string name, int rankOrder, SalaryBand? band = null)
  {
    var gradeCode = SalaryGradeCode.Create(code);
    if (gradeCode.IsFailure)
    {
      return Result.Failure<SalaryGrade>(gradeCode.Error);
    }

    return SalaryGrade.Create(
      gradeCode.Value,
      SalaryGradeName.Create(name).Value,
      rankOrder,
      band,
      Actor,
      Guid.NewGuid(),
      Now);
  }
}
