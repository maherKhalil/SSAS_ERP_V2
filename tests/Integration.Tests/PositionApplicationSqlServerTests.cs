using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.Integration.Tests;

// THE POSITION APPLICATION SURFACE AGAINST REAL SQL SERVER (FP-008 Phase 2).
//
// Everything here runs the production handlers over the production repositories and read services against a
// real tenant database. What these prove that `HR.Tests` cannot: that the dependent refusals see committed
// state, that the rowversion refusals are the DATABASE's and not only the handler's pre-check, and that the
// scope predicates actually restrict what SQL Server returns.
[Trait("Category", "SqlServer")]
public sealed class PositionApplicationSqlServerTests
{
  // ================================================================================================
  // CREATE, AND THE OWNERSHIP IT STAMPS RATHER THAN ACCEPTS (FR-POS-0201, BRULE-POS-0001)
  // ================================================================================================
  [Fact]
  [Trait("Requirement", "FR-POS-0201")]
  public async Task A_created_position_is_active_and_carries_the_stamped_ownership()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var graph = fixture.Graph();

    var created = await graph.CreatePosition().HandleAsync(
      new CreatePositionCommand(fixture.CompanyA, "ACC-SR", "Senior Accountant", null));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    var read = await graph.GetPosition().HandleAsync(new GetPositionQuery(created.Value));

    Assert.True(read.IsSuccess);
    Assert.Equal(fixture.CompanyA, read.Value.CompanyId);
    Assert.Equal("ACC-SR", read.Value.Code);
    Assert.Equal("Senior Accountant", read.Value.Title);
    Assert.Equal(PositionStatus.Active, read.Value.Status);
    Assert.Null(read.Value.JobGrade);

    // The tenant was never in the command. It is on the row because the boundary put it there.
    Assert.Equal(1, await fixture.ScalarAsync(
      $"SELECT COUNT(*) FROM [tenant].[Positions] WHERE [TenantId] = '{fixture.Tenant}'"));
  }

  // ---- A SECOND POSITION WHOSE CODE NORMALIZES ALIKE IS REFUSED (BRULE-POS-0004).
  [Fact]
  [Trait("Rule", "BRULE-POS-0004")]
  public async Task A_duplicate_normalized_position_code_is_refused_within_the_company()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant");

    var conflict = await fixture.Graph().CreatePosition().HandleAsync(
      new CreatePositionCommand(fixture.CompanyA, "acc-sr", "Another Title", null));

    Assert.True(conflict.IsFailure);
    Assert.Equal(PositionErrors.PositionCodeConflict, conflict.Error);
  }

  // ---- THE SAME CODE IN ANOTHER COMPANY IS NOT A CONFLICT. Uniqueness is per company, not per tenant.
  [Fact]
  [Trait("Rule", "BRULE-POS-0004")]
  public async Task The_same_position_code_is_free_in_another_company()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant");

    var other = await fixture.Graph(fixture.CompanyB).CreatePosition().HandleAsync(
      new CreatePositionCommand(fixture.CompanyB, "ACC-SR", "Senior Accountant", null));

    Assert.True(other.IsSuccess, other.IsFailure ? other.Error.Code : null);
  }

  // ---- A CALLER SCOPED TO ONE COMPANY CANNOT CREATE IN ANOTHER (BR-PLT-0002).
  [Fact]
  [Trait("Rule", "BRULE-POS-0002")]
  public async Task Creating_a_position_in_an_unauthorized_company_is_refused()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();

    var refused = await fixture.Graph(fixture.CompanyA).CreatePosition().HandleAsync(
      new CreatePositionCommand(fixture.CompanyB, "ACC-SR", "Senior Accountant", null));

    Assert.True(refused.IsFailure);
    Assert.Equal(PositionErrors.CompanyScopeDenied, refused.Error);

    Assert.Equal(0, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[Positions]"));
  }

  // ================================================================================================
  // THE GRADE REFERENCE, AND ITS THREE WAYS OF BEING WRONG (BRULE-POS-0009, 0011)
  // ================================================================================================
  [Fact]
  [Trait("Rule", "BRULE-POS-0009")]
  public async Task A_position_may_reference_an_active_grade_in_its_own_company()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70);
    var graph = fixture.Graph();

    var created = await graph.CreatePosition().HandleAsync(
      new CreatePositionCommand(fixture.CompanyA, "ACC-SR", "Senior Accountant", jobGradeId));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    // ---- AND THE GRADE BLOCK IS RESOLVED IN THE SAME READ, UNDER THE SAME SCOPE.
    var read = await graph.GetPosition().HandleAsync(new GetPositionQuery(created.Value));

    Assert.True(read.IsSuccess);
    Assert.NotNull(read.Value.JobGrade);
    Assert.Equal("G7", read.Value.JobGrade!.Code);
    Assert.Equal("Grade 7", read.Value.JobGrade.Name);
    Assert.Equal(70, read.Value.JobGrade.RankOrder);
  }

  [Fact]
  [Trait("Rule", "BRULE-POS-0011")]
  public async Task A_grade_that_does_not_exist_is_refused_as_an_invalid_reference()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();

    var refused = await fixture.Graph().CreatePosition().HandleAsync(
      new CreatePositionCommand(fixture.CompanyA, "ACC-SR", "Senior Accountant", Guid.NewGuid()));

    Assert.True(refused.IsFailure);
    Assert.Equal(PositionErrors.GradeReferenceNotFound, refused.Error);
  }

  // ---- A GRADE FROM ANOTHER COMPANY IS REFUSED (BRULE-POS-0011).
  //
  // The grade genuinely exists, so this proves the COMPANY check rather than a lookup miss — which is the
  // distinction the previous test cannot make.
  [Fact]
  [Trait("Rule", "BRULE-POS-0011")]
  public async Task A_grade_from_another_company_is_refused()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var foreignGradeId = await fixture.CreateJobGradeAsync(
      "G7", "Grade 7", 70, company: fixture.CompanyB);

    var refused = await fixture.Graph(fixture.CompanyA).CreatePosition().HandleAsync(
      new CreatePositionCommand(fixture.CompanyA, "ACC-SR", "Senior Accountant", foreignGradeId));

    Assert.True(refused.IsFailure);
    Assert.Equal(PositionErrors.GradeInDifferentCompany, refused.Error);
  }

  [Fact]
  [Trait("Rule", "BRULE-POS-0009")]
  public async Task An_inactive_grade_cannot_be_assigned()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70);

    var deactivated = await fixture.Graph().DeactivateJobGrade().HandleAsync(
      new DeactivateJobGradeCommand(
        jobGradeId, await fixture.RowVersionAsync("JobGrades", "JobGradeId", jobGradeId)));
    Assert.True(deactivated.IsSuccess, deactivated.IsFailure ? deactivated.Error.Code : null);

    var refused = await fixture.Graph().CreatePosition().HandleAsync(
      new CreatePositionCommand(fixture.CompanyA, "ACC-SR", "Senior Accountant", jobGradeId));

    Assert.True(refused.IsFailure);
    Assert.Equal(PositionErrors.GradeInactive, refused.Error);
  }

  // ---- AN UPDATE RE-VALIDATES THE REFERENCE EVEN WHEN IT IS UNCHANGED.
  //
  // The grade was Active when it was assigned and is not now. An update that preserved the reference
  // silently would let the aggregate drift past `BRULE-POS-0009` without any operation having broken it.
  [Fact]
  [Trait("Rule", "BRULE-POS-0009")]
  public async Task Updating_a_position_revalidates_a_grade_that_has_since_been_deactivated()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70);
    var positionId = await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant", jobGradeId);

    // The position must be inactive first, or the grade could not be deactivated at all.
    await fixture.Graph().DeactivatePosition().HandleAsync(new DeactivatePositionCommand(
      positionId, await fixture.RowVersionAsync("Positions", "PositionId", positionId)));

    await fixture.Graph().DeactivateJobGrade().HandleAsync(new DeactivateJobGradeCommand(
      jobGradeId, await fixture.RowVersionAsync("JobGrades", "JobGradeId", jobGradeId)));

    var refused = await fixture.Graph().UpdatePosition().HandleAsync(new UpdatePositionCommand(
      positionId, "ACC-SR", "Senior Accountant II", jobGradeId,
      await fixture.RowVersionAsync("Positions", "PositionId", positionId)));

    Assert.True(refused.IsFailure);
    Assert.Equal(PositionErrors.GradeInactive, refused.Error);
  }

  // ================================================================================================
  // THE DEPENDENT REFUSAL (DEC-POS-0013, BRULE-POS-0015)
  // ================================================================================================
  //
  // A grade with ACTIVE dependents may not be deactivated, and deactivation does not cascade. The refusal is
  // what makes the reference safe: without it an Active position would be left aimed at an Inactive grade.
  [Fact]
  [Trait("Decision", "DEC-POS-0013")]
  public async Task A_job_grade_with_an_active_position_cannot_be_deactivated()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70);
    var positionId = await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant", jobGradeId);

    var refused = await fixture.Graph().DeactivateJobGrade().HandleAsync(new DeactivateJobGradeCommand(
      jobGradeId, await fixture.RowVersionAsync("JobGrades", "JobGradeId", jobGradeId)));

    Assert.True(refused.IsFailure);
    Assert.Equal(PositionErrors.GradeHasActiveDependents, refused.Error);

    // ---- AND NOTHING CASCADED. The position is untouched, which is what makes the refusal reversible
    // rather than a partial write the operator now has to unpick.
    Assert.Equal(1, await fixture.ScalarAsync(
      $"SELECT COUNT(*) FROM [tenant].[Positions] " +
      $"WHERE [PositionId] = '{positionId}' AND [Status] = N'Active'"));

    // Once the dependent is out of the way the same call succeeds. The rule is "not while", not "never".
    await fixture.Graph().DeactivatePosition().HandleAsync(new DeactivatePositionCommand(
      positionId, await fixture.RowVersionAsync("Positions", "PositionId", positionId)));

    var allowed = await fixture.Graph().DeactivateJobGrade().HandleAsync(new DeactivateJobGradeCommand(
      jobGradeId, await fixture.RowVersionAsync("JobGrades", "JobGradeId", jobGradeId)));

    Assert.True(allowed.IsSuccess, allowed.IsFailure ? allowed.Error.Code : null);
  }

  // ---- AN INACTIVE DEPENDENT DOES NOT BLOCK. It is already in the state that makes the reference harmless.
  [Fact]
  [Trait("Decision", "DEC-POS-0013")]
  public async Task A_salary_grade_with_only_inactive_job_grades_can_be_deactivated()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var salaryGradeId = await fixture.CreateSalaryGradeAsync("S7", "Band 7", 70);
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70, salaryGradeId);

    var blocked = await fixture.Graph().DeactivateSalaryGrade().HandleAsync(
      new DeactivateSalaryGradeCommand(
        salaryGradeId, await fixture.RowVersionAsync("SalaryGrades", "SalaryGradeId", salaryGradeId)));

    Assert.True(blocked.IsFailure);
    Assert.Equal(PositionErrors.GradeHasActiveDependents, blocked.Error);

    await fixture.Graph().DeactivateJobGrade().HandleAsync(new DeactivateJobGradeCommand(
      jobGradeId, await fixture.RowVersionAsync("JobGrades", "JobGradeId", jobGradeId)));

    var allowed = await fixture.Graph().DeactivateSalaryGrade().HandleAsync(
      new DeactivateSalaryGradeCommand(
        salaryGradeId, await fixture.RowVersionAsync("SalaryGrades", "SalaryGradeId", salaryGradeId)));

    Assert.True(allowed.IsSuccess, allowed.IsFailure ? allowed.Error.Code : null);
  }

  // ================================================================================================
  // A POSITION WITH HOLDERS MAY BE DEACTIVATED — THE ASYMMETRY IS THE RULING (OD-POS-005)
  // ================================================================================================
  //
  // The employee half of this cannot be written until Phase 3 gives `Employee` a `PositionId`. What IS
  // provable now is that the handler consults no dependent set at all: deactivation succeeds with no
  // repository question asked beyond the load, and the aggregate refuses only the second attempt.
  [Fact]
  [Trait("Decision", "OD-POS-005")]
  public async Task Deactivating_a_position_asks_no_dependent_question_and_is_reversible()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var positionId = await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant");

    var deactivated = await fixture.Graph().DeactivatePosition().HandleAsync(
      new DeactivatePositionCommand(
        positionId, await fixture.RowVersionAsync("Positions", "PositionId", positionId)));
    Assert.True(deactivated.IsSuccess, deactivated.IsFailure ? deactivated.Error.Code : null);

    // A second deactivation is an invalid transition, not a silent success. The established lifecycle
    // convention: Employee and Department both answer this way.
    var again = await fixture.Graph().DeactivatePosition().HandleAsync(new DeactivatePositionCommand(
      positionId, await fixture.RowVersionAsync("Positions", "PositionId", positionId)));

    Assert.True(again.IsFailure);
    Assert.Equal(PositionErrors.InvalidTransition, again.Error);

    var reactivated = await fixture.Graph().ReactivatePosition().HandleAsync(
      new ReactivatePositionCommand(
        positionId, await fixture.RowVersionAsync("Positions", "PositionId", positionId)));

    Assert.True(reactivated.IsSuccess, reactivated.IsFailure ? reactivated.Error.Code : null);
    Assert.Equal(1, await fixture.ScalarAsync(
      $"SELECT COUNT(*) FROM [tenant].[Positions] " +
      $"WHERE [PositionId] = '{positionId}' AND [Status] = N'Active'"));
  }

  // ---- A POSITION MAY BE REACTIVATED WHILE ITS GRADE IS INACTIVE, AND THAT IS DELIBERATE.
  //
  // Refusing would strand it: re-pointing the grade needs `HR.Positions.Update`, which the holder of
  // `HR.Positions.Deactivate` may not have, so the refusal would be unactionable for exactly the role the
  // permission split was made to serve.
  [Fact]
  [Trait("Decision", "DEC-DEP-0025")]
  public async Task A_position_reactivates_even_while_its_grade_is_inactive()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70);
    var positionId = await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant", jobGradeId);

    await fixture.Graph().DeactivatePosition().HandleAsync(new DeactivatePositionCommand(
      positionId, await fixture.RowVersionAsync("Positions", "PositionId", positionId)));
    await fixture.Graph().DeactivateJobGrade().HandleAsync(new DeactivateJobGradeCommand(
      jobGradeId, await fixture.RowVersionAsync("JobGrades", "JobGradeId", jobGradeId)));

    var reactivated = await fixture.Graph().ReactivatePosition().HandleAsync(
      new ReactivatePositionCommand(
        positionId, await fixture.RowVersionAsync("Positions", "PositionId", positionId)));

    Assert.True(reactivated.IsSuccess, reactivated.IsFailure ? reactivated.Error.Code : null);
  }

  // ================================================================================================
  // OPTIMISTIC CONCURRENCY (NFR-POS-0302, DEC-POS-0021)
  // ================================================================================================
  [Theory]
  [InlineData("Positions")]
  [InlineData("JobGrades")]
  [InlineData("SalaryGrades")]
  [Trait("Requirement", "NFR-POS-0302")]
  public async Task A_stale_row_version_is_refused_on_every_family(string table)
  {
    await using var fixture = await PositionAppFixture.CreateAsync();

    var stale = table switch
    {
      "JobGrades" => await StaleJobGradeAsync(fixture),
      "SalaryGrades" => await StaleSalaryGradeAsync(fixture),
      _ => await StalePositionAsync(fixture)
    };

    Assert.True(stale.IsFailure);
    Assert.Equal(PositionErrors.ConcurrencyConflict, stale.Error);
  }

  // ---- AND THE DATABASE HAS THE LAST WORD, not only the handler's pre-check.
  //
  // Two graphs load the SAME grade and both pass their pre-check, because both read the same current token.
  // The loser is then refused by the rowversion comparison SQL Server performs on UPDATE — which is the rule,
  // while the pre-check is only the friendly message.
  [Fact]
  [Trait("Requirement", "NFR-POS-0302")]
  public async Task Two_concurrent_deactivations_of_one_grade_leave_exactly_one_winner()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70);
    var rowVersion = await fixture.RowVersionAsync("JobGrades", "JobGradeId", jobGradeId);

    var first = fixture.Graph();
    var second = fixture.Graph();

    var firstResult = await first.DeactivateJobGrade().HandleAsync(
      new DeactivateJobGradeCommand(jobGradeId, rowVersion));

    // The SAME token, which was current when both callers read it and is not current now.
    var secondResult = await second.DeactivateJobGrade().HandleAsync(
      new DeactivateJobGradeCommand(jobGradeId, rowVersion));

    Assert.True(firstResult.IsSuccess, firstResult.IsFailure ? firstResult.Error.Code : null);
    Assert.True(secondResult.IsFailure);

    // The second caller is refused, and it does not matter here whether the handler's comparison or the
    // database's caught it — both are concurrency refusals and both leave one winner. What must NOT happen
    // is two successes.
    Assert.Equal(1, await fixture.ScalarAsync(
      $"SELECT COUNT(*) FROM [tenant].[JobGrades] " +
      $"WHERE [JobGradeId] = '{jobGradeId}' AND [Status] = N'Inactive'"));
  }

  // ================================================================================================
  // THE TWO UNIQUE INDEXES ON A GRADE ARE NOT INTERCHANGEABLE (BRULE-POS-0007)
  // ================================================================================================
  //
  // `api-contracts.md` records this as a case the Department precedent does not cover: Department had
  // exactly one unique index per operation, and a grade has two. A caller who collides on rank must be told
  // about the rank rather than about the code.
  [Fact]
  [Trait("Rule", "BRULE-POS-0007")]
  public async Task A_grade_code_conflict_and_a_rank_conflict_are_distinguishable()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    await fixture.CreateJobGradeAsync("G7", "Grade 7", 70);

    var codeConflict = await fixture.Graph().CreateJobGrade().HandleAsync(
      new CreateJobGradeCommand(fixture.CompanyA, "g7", "Another", 80, null));

    Assert.True(codeConflict.IsFailure);
    Assert.Equal(PositionErrors.JobGradeCodeConflict, codeConflict.Error);

    var rankConflict = await fixture.Graph().CreateJobGrade().HandleAsync(
      new CreateJobGradeCommand(fixture.CompanyA, "G8", "Grade 8", 70, null));

    Assert.True(rankConflict.IsFailure);
    Assert.Equal(PositionErrors.JobGradeRankConflict, rankConflict.Error);
  }

  // ---- THE TWO LADDERS DO NOT SHARE A RANK SPACE. Rank 70 exists once in each.
  [Fact]
  [Trait("Decision", "OD-POS-002")]
  public async Task The_two_ladders_may_each_hold_the_same_rank()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();

    await fixture.CreateSalaryGradeAsync("S7", "Band 7", 70);

    var jobGrade = await fixture.Graph().CreateJobGrade().HandleAsync(
      new CreateJobGradeCommand(fixture.CompanyA, "G7", "Grade 7", 70, null));

    Assert.True(jobGrade.IsSuccess, jobGrade.IsFailure ? jobGrade.Error.Code : null);
  }

  // ================================================================================================
  // THE SALARY BAND THROUGH THE APPLICATION (DEC-POS-0027, BRULE-POS-0008)
  // ================================================================================================
  //
  // The command carries three nullable decimals precisely so a caller CAN send a half-filled band and be
  // refused by name. A command shaped as a constructed `SalaryBand` could not express the mistake, and the
  // refusal would then be untestable because it would be unreachable.
  //
  // The amounts arrive as `int?` because an attribute argument cannot be a `decimal` literal — C# has no
  // constant form for one. They are widened at the call below; the values chosen are exact in both types,
  // so nothing about the refusal depends on the conversion.
  [Theory]
  [InlineData(100, null, null, "Position.SalaryBandIncomplete")]
  [InlineData(100, 200, null, "Position.SalaryBandIncomplete")]
  [InlineData(null, 200, null, "Position.SalaryBandIncomplete")]
  [InlineData(-1, 200, 300, "Position.SalaryBandNegative")]
  [InlineData(300, 200, 100, "Position.SalaryBandOutOfOrder")]
  [Trait("Decision", "DEC-POS-0027")]
  public async Task A_partial_or_disordered_band_is_refused_by_name(
    int? minimum, int? midpoint, int? maximum, string expected)
  {
    await using var fixture = await PositionAppFixture.CreateAsync();

    var refused = await fixture.Graph().CreateSalaryGrade().HandleAsync(
      new CreateSalaryGradeCommand(
        fixture.CompanyA, "S7", "Band 7", 70,
        (decimal?)minimum, (decimal?)midpoint, (decimal?)maximum));

    Assert.True(refused.IsFailure);
    Assert.Equal(expected, refused.Error.Code);
    Assert.Equal(0, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[SalaryGrades]"));
  }

  // ---- A BAND CAN BE ADDED AND WITHDRAWN. Un-pricing is a legal correction, not a lost mistake.
  [Fact]
  [Trait("Decision", "DEC-POS-0027")]
  public async Task A_band_can_be_priced_then_withdrawn_through_the_update()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var salaryGradeId = await fixture.CreateSalaryGradeAsync("S7", "Band 7", 70);
    var graph = fixture.Graph();

    var priced = await graph.UpdateSalaryGrade().HandleAsync(new UpdateSalaryGradeCommand(
      salaryGradeId, "S7", "Band 7", 70, 12000.5000m, 15000m, 18000m,
      await fixture.RowVersionAsync("SalaryGrades", "SalaryGradeId", salaryGradeId)));
    Assert.True(priced.IsSuccess, priced.IsFailure ? priced.Error.Code : null);

    var read = await graph.GetSalaryGrade().HandleAsync(new GetSalaryGradeQuery(salaryGradeId));
    Assert.True(read.IsSuccess);
    Assert.Equal(12000.5000m, read.Value.MinimumAmount);
    Assert.Equal(18000m, read.Value.MaximumAmount);

    var withdrawn = await graph.UpdateSalaryGrade().HandleAsync(new UpdateSalaryGradeCommand(
      salaryGradeId, "S7", "Band 7", 70, null, null, null,
      await fixture.RowVersionAsync("SalaryGrades", "SalaryGradeId", salaryGradeId)));
    Assert.True(withdrawn.IsSuccess, withdrawn.IsFailure ? withdrawn.Error.Code : null);

    var unpriced = await graph.GetSalaryGrade().HandleAsync(new GetSalaryGradeQuery(salaryGradeId));
    Assert.True(unpriced.IsSuccess);
    Assert.Null(unpriced.Value.MinimumAmount);
    Assert.Null(unpriced.Value.MidpointAmount);
    Assert.Null(unpriced.Value.MaximumAmount);
  }

  // ================================================================================================
  // THE READ SCOPE IS THE PERMISSION (DEC-POS-0018, DEC-POS-0020)
  // ================================================================================================
  //
  // Against real data, not only against the resolver: a caller holding every position and job grade
  // permission reads positions and grades, and is refused the pay bands.
  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  public async Task A_caller_without_the_salary_grade_view_reads_positions_but_no_pay_band()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var salaryGradeId = await fixture.CreateSalaryGradeAsync("S7", "Band 7", 70, 100m, 200m, 300m);
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70, salaryGradeId);
    var positionId = await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant", jobGradeId);

    var graph = fixture.Graph(permissions:
      [HrPermissionNames.ViewPositions, HrPermissionNames.ViewJobGrades]);

    Assert.True((await graph.GetPosition().HandleAsync(new GetPositionQuery(positionId))).IsSuccess);

    var jobGrade = await graph.GetJobGrade().HandleAsync(new GetJobGradeQuery(jobGradeId));
    Assert.True(jobGrade.IsSuccess);

    // The job grade discloses the IDENTIFIER of the band it sits on and nothing more — a structural fact
    // about the ladder, not the pay structure.
    Assert.Equal(salaryGradeId, jobGrade.Value.SalaryGradeId);

    var refused = await graph.GetSalaryGrade().HandleAsync(new GetSalaryGradeQuery(salaryGradeId));

    Assert.True(refused.IsFailure);
    Assert.Equal(PositionErrors.PermissionDenied, refused.Error);
  }

  // ---- A POSITION IN ANOTHER COMPANY IS NOT FOUND, NOT FORBIDDEN (BR-PLT-0002).
  //
  // A distinct refusal would confirm the position exists in a company the caller may not see.
  [Fact]
  [Trait("Rule", "BRULE-POS-0002")]
  public async Task A_position_outside_the_company_scope_is_indistinguishable_from_absent()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var foreignId = await fixture.CreatePositionAsync(
      "ACC-SR", "Senior Accountant", company: fixture.CompanyB);

    var graph = fixture.Graph(fixture.CompanyA);

    var outOfScope = await graph.GetPosition().HandleAsync(new GetPositionQuery(foreignId));
    var nonexistent = await graph.GetPosition().HandleAsync(new GetPositionQuery(Guid.NewGuid()));

    Assert.True(outOfScope.IsFailure);
    Assert.Equal(nonexistent.Error, outOfScope.Error);
    Assert.Equal(PositionErrors.PositionNotFound, outOfScope.Error);
  }

  // ---- AND A WRITE TO ONE IS ANSWERED THE SAME WAY, for the same reason.
  [Fact]
  [Trait("Rule", "BRULE-POS-0002")]
  public async Task A_write_to_a_position_outside_the_company_scope_answers_not_found()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var foreignId = await fixture.CreatePositionAsync(
      "ACC-SR", "Senior Accountant", company: fixture.CompanyB);

    var refused = await fixture.Graph(fixture.CompanyA).UpdatePosition().HandleAsync(
      new UpdatePositionCommand(
        foreignId, "ACC-SR", "Renamed", null,
        await fixture.RowVersionAsync("Positions", "PositionId", foreignId)));

    Assert.True(refused.IsFailure);
    Assert.Equal(PositionErrors.PositionNotFound, refused.Error);
  }

  // ================================================================================================
  // SEARCH (FR-POS-0203)
  // ================================================================================================
  [Fact]
  [Trait("Requirement", "FR-POS-0203")]
  public async Task A_search_is_scoped_filtered_and_deterministically_ordered()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70);

    await fixture.CreatePositionAsync("ACC-JR", "Junior Accountant");
    await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant", jobGradeId);
    await fixture.CreatePositionAsync("DEV-SR", "Senior Developer", jobGradeId);
    await fixture.CreatePositionAsync("ZZZ-01", "Elsewhere", company: fixture.CompanyB);

    var graph = fixture.Graph(fixture.CompanyA);

    var all = await graph.SearchPositions().HandleAsync(new SearchPositionsQuery());
    Assert.True(all.IsSuccess);
    Assert.Equal(3, all.Value.TotalCount);
    Assert.Equal(["ACC-JR", "ACC-SR", "DEV-SR"], all.Value.Items.Select(item => item.Code));

    var byGrade = await graph.SearchPositions().HandleAsync(
      new SearchPositionsQuery(JobGradeId: jobGradeId));
    Assert.True(byGrade.IsSuccess);
    Assert.Equal(2, byGrade.Value.TotalCount);

    // Matched on the normalized CODE prefix, case-insensitively because the caller's text is normalized the
    // same way the stored value was.
    var byText = await graph.SearchPositions().HandleAsync(new SearchPositionsQuery(SearchText: "acc"));
    Assert.True(byText.IsSuccess);
    Assert.Equal(2, byText.Value.TotalCount);

    // ---- AND THE TITLE HALF, WHICH IS THE ONE THAT USED TO RETURN NOTHING (DEC-POS-0030).
    //
    // "Developer" is the TITLE of DEV-SR and matches no code prefix, so only the title predicate can find
    // it. This assertion was `Assert.Equal(0, ...)` in the commit that first shipped this test, documenting
    // an unimplemented half rather than hiding it; the ruled search column is what makes it a 1.
    var byTitle = await graph.SearchPositions().HandleAsync(
      new SearchPositionsQuery(SearchText: "Developer"));
    Assert.True(byTitle.IsSuccess);
    Assert.Equal(1, byTitle.Value.TotalCount);
    Assert.Equal("DEV-SR", byTitle.Value.Items.Single().Code);

    // CASE-INSENSITIVE, over a BINARY-collated column. Both sides are upper-invariant — the stored value by
    // the domain, the pattern by the query — which is what makes an ordinal column searchable without a
    // case-insensitive collation.
    var lowerCase = await graph.SearchPositions().HandleAsync(
      new SearchPositionsQuery(SearchText: "senior developer"));
    Assert.True(lowerCase.IsSuccess);
    Assert.Equal(1, lowerCase.Value.TotalCount);

    // A MID-WORD FRAGMENT matches the title but not the code, because the title half is a CONTAINS and the
    // code half is a PREFIX. Asserting both halves' shapes in one query.
    var fragment = await graph.SearchPositions().HandleAsync(new SearchPositionsQuery(SearchText: "ccount"));
    Assert.True(fragment.IsSuccess);
    Assert.Equal(2, fragment.Value.TotalCount);
  }

  // ================================================================================================
  // A WILDCARD IN THE SEARCH TEXT IS A LITERAL CHARACTER, NOT AN OPERATOR (DEC-POS-0030)
  // ================================================================================================
  //
  // The failure this prevents is quiet rather than loud: an unescaped `%` reaching the LIKE pattern makes
  // the predicate match everything, and the caller sees a full page of results instead of an error. A
  // search that returns too much looks like a search that works.
  [Theory]
  [InlineData("%", 1)]
  [InlineData("_", 1)]
  [InlineData("[", 1)]
  [Trait("Decision", "DEC-POS-0030")]
  public async Task A_wildcard_character_in_the_search_text_matches_only_itself(
    string wildcard, int expected)
  {
    await using var fixture = await PositionAppFixture.CreateAsync();

    // One position whose title contains the character, and two that do not. An unescaped pattern would
    // return all three.
    await fixture.CreatePositionAsync("ACC-JR", $"Junior {wildcard} Accountant");
    await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant");
    await fixture.CreatePositionAsync("DEV-SR", "Senior Developer");

    var found = await fixture.Graph().SearchPositions().HandleAsync(
      new SearchPositionsQuery(SearchText: wildcard));

    Assert.True(found.IsSuccess);
    Assert.Equal(expected, found.Value.TotalCount);
    Assert.Equal("ACC-JR", found.Value.Items.Single().Code);
  }

  // ---- AND THE ESCAPE CHARACTER ITSELF IS ESCAPED FIRST.
  //
  // A typed backslash must not turn the character after it into an escape sequence. Ordering the
  // replacements wrongly — `%` before `\` — produces a pattern that matches nothing, which is the failure
  // mode a single-wildcard test would miss entirely.
  [Fact]
  [Trait("Decision", "DEC-POS-0030")]
  public async Task A_backslash_in_the_search_text_matches_only_itself()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();

    await fixture.CreatePositionAsync("ACC-JR", @"Junior \ Accountant");
    await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant");

    var found = await fixture.Graph().SearchPositions().HandleAsync(
      new SearchPositionsQuery(SearchText: @"\"));

    Assert.True(found.IsSuccess);
    Assert.Equal(1, found.Value.TotalCount);
    Assert.Equal("ACC-JR", found.Value.Items.Single().Code);
  }

  // ---- THE GRADE LADDERS SEARCH THEIR NAMES TOO, ON THE SAME MECHANISM.
  [Fact]
  [Trait("Requirement", "FR-POS-0206")]
  public async Task A_grade_search_matches_the_name_as_well_as_the_code()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    await fixture.CreateJobGradeAsync("G7", "Professional Band", 70);
    await fixture.CreateJobGradeAsync("G8", "Leadership Band", 80);

    var graph = fixture.Graph();

    var byName = await graph.SearchJobGrades().HandleAsync(
      new SearchJobGradesQuery(SearchText: "leadership"));
    Assert.True(byName.IsSuccess);
    Assert.Equal("G8", byName.Value.Items.Single().Code);

    var byCode = await graph.SearchJobGrades().HandleAsync(new SearchJobGradesQuery(SearchText: "G7"));
    Assert.True(byCode.IsSuccess);
    Assert.Equal("G7", byCode.Value.Items.Single().Code);
  }

  // ---- A GRADE LADDER LISTS BY RANK, NOT ALPHABETICALLY.
  //
  // The order IS the ladder (`DEC-POS-0006`). Sorted by code, G10 would sit between G1 and G2 and the one
  // view whose purpose is to show the ladder would be unreadable.
  [Fact]
  [Trait("Decision", "DEC-POS-0006")]
  public async Task Grades_list_in_rank_order_rather_than_code_order()
  {
    await using var fixture = await PositionAppFixture.CreateAsync();
    await fixture.CreateJobGradeAsync("G1", "Grade 1", 10);
    await fixture.CreateJobGradeAsync("G10", "Grade 10", 100);
    await fixture.CreateJobGradeAsync("G2", "Grade 2", 20);

    var listed = await fixture.Graph().SearchJobGrades().HandleAsync(new SearchJobGradesQuery());

    Assert.True(listed.IsSuccess);
    Assert.Equal(["G1", "G2", "G10"], listed.Value.Items.Select(item => item.Code));
  }

  // ---- PAGINATION IS REFUSED, NOT CLAMPED.
  //
  // Silently reducing a page size of 5000 to 200 would return a page the caller did not ask for and let
  // them believe they had seen the rest.
  [Theory]
  [InlineData(0, 25)]
  [InlineData(1, 0)]
  [InlineData(1, 201)]
  public async Task An_out_of_range_page_request_is_refused(int page, int pageSize)
  {
    await using var fixture = await PositionAppFixture.CreateAsync();

    var refused = await fixture.Graph().SearchPositions().HandleAsync(
      new SearchPositionsQuery(Page: page, PageSize: pageSize));

    Assert.True(refused.IsFailure);
    Assert.Equal(PositionErrors.InvalidPagination, refused.Error);
  }

  private static async Task<SSAS.BuildingBlocks.Domain.Result> StalePositionAsync(
    PositionAppFixture fixture)
  {
    var positionId = await fixture.CreatePositionAsync("ACC-SR", "Senior Accountant");
    var stale = await fixture.RowVersionAsync("Positions", "PositionId", positionId);

    // One successful write moves the token on, so the captured one is genuinely stale rather than merely
    // wrong — which is what makes this a concurrency proof instead of a malformed-input proof.
    await fixture.Graph().UpdatePosition().HandleAsync(
      new UpdatePositionCommand(positionId, "ACC-SR", "Renamed", null, stale));

    return await fixture.Graph().UpdatePosition().HandleAsync(
      new UpdatePositionCommand(positionId, "ACC-SR", "Renamed Again", null, stale));
  }

  private static async Task<SSAS.BuildingBlocks.Domain.Result> StaleJobGradeAsync(
    PositionAppFixture fixture)
  {
    var jobGradeId = await fixture.CreateJobGradeAsync("G7", "Grade 7", 70);
    var stale = await fixture.RowVersionAsync("JobGrades", "JobGradeId", jobGradeId);

    await fixture.Graph().UpdateJobGrade().HandleAsync(
      new UpdateJobGradeCommand(jobGradeId, "G7", "Renamed", 70, null, stale));

    return await fixture.Graph().UpdateJobGrade().HandleAsync(
      new UpdateJobGradeCommand(jobGradeId, "G7", "Renamed Again", 70, null, stale));
  }

  private static async Task<SSAS.BuildingBlocks.Domain.Result> StaleSalaryGradeAsync(
    PositionAppFixture fixture)
  {
    var salaryGradeId = await fixture.CreateSalaryGradeAsync("S7", "Band 7", 70);
    var stale = await fixture.RowVersionAsync("SalaryGrades", "SalaryGradeId", salaryGradeId);

    await fixture.Graph().UpdateSalaryGrade().HandleAsync(
      new UpdateSalaryGradeCommand(salaryGradeId, "S7", "Renamed", 70, null, null, null, stale));

    return await fixture.Graph().UpdateSalaryGrade().HandleAsync(
      new UpdateSalaryGradeCommand(salaryGradeId, "S7", "Renamed Again", 70, null, null, null, stale));
  }
}
