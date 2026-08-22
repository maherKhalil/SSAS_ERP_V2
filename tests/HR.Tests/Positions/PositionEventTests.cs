using SSAS.HR.Domain.Events;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Tests.Positions;

// THE AGGREGATES ACTUALLY RAISE THEIR EVENTS (FP-008 Phase 1, ADR-009).
//
// NOTHING CONSUMES THEM IN PHASE 1, WHICH IS EXACTLY WHY THEY ARE ASSERTED HERE. An event with no
// subscriber and no test is an event nobody notices has stopped being raised; the vocabulary is settled now
// so that handlers written against it in a later phase are written against something real.
//
// `EmployeeDomainTests.Employee_events_carry_no_personal_data` guards what they may CARRY. This guards that
// they are EMITTED. Neither check implies the other: an event can be perfectly shaped and never raised.
public sealed class PositionEventTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

  private const string Actor = "tester";

  [Fact]
  public void Stamping_a_position_raises_created_with_the_trusted_ownership()
  {
    var tenantId = Guid.NewGuid();
    var companyId = Guid.NewGuid();
    var jobGradeId = Guid.NewGuid();
    var position = NewPosition(jobGradeId);

    position.StampCreated(tenantId, companyId, Guid.NewGuid(), Now);

    var raised = Assert.Single(position.DomainEvents.OfType<PositionCreated>());
    Assert.Equal(position.PositionId, raised.PositionId);
    Assert.Equal(tenantId, raised.TenantId);
    Assert.Equal(companyId, raised.CompanyId);
    Assert.Equal(jobGradeId, raised.JobGradeId);
    Assert.Equal(PositionStatus.Active, raised.NewStatus);
  }

  // THE EVENT CARRIES BOTH GRADES so a consumer can tell a re-grade from a first grading without
  // re-reading the row.
  [Fact]
  public void Updating_a_position_raises_updated_with_both_grades()
  {
    var firstGrade = Guid.NewGuid();
    var secondGrade = Guid.NewGuid();
    var position = NewPosition(firstGrade);

    position.UpdateDescription(
      PositionCode.Create("ACC-LEAD").Value,
      PositionTitle.Create("Lead Accountant").Value,
      secondGrade,
      Guid.NewGuid(),
      Now);

    var raised = Assert.Single(position.DomainEvents.OfType<PositionUpdated>());
    Assert.Equal(firstGrade, raised.PreviousJobGradeId);
    Assert.Equal(secondGrade, raised.NewJobGradeId);
  }

  [Fact]
  public void Position_lifecycle_transitions_raise_their_events_with_both_states()
  {
    var position = NewPosition(jobGradeId: null);

    position.Deactivate(Actor, Guid.NewGuid(), Now);
    var deactivated = Assert.Single(position.DomainEvents.OfType<PositionDeactivated>());
    Assert.Equal(PositionStatus.Active, deactivated.PreviousStatus);
    Assert.Equal(PositionStatus.Inactive, deactivated.NewStatus);

    position.Reactivate(Actor, Guid.NewGuid(), Now);
    var reactivated = Assert.Single(position.DomainEvents.OfType<PositionReactivated>());
    Assert.Equal(PositionStatus.Inactive, reactivated.PreviousStatus);
    Assert.Equal(PositionStatus.Active, reactivated.NewStatus);
  }

  // A REFUSED TRANSITION RAISES NOTHING. An event emitted for a change that did not happen is worse than a
  // missing one: every consumer acts on a state the aggregate never entered.
  [Fact]
  public void A_refused_transition_raises_no_event()
  {
    var position = NewPosition(jobGradeId: null);

    var refused = position.Reactivate(Actor, Guid.NewGuid(), Now);

    Assert.True(refused.IsFailure);
    Assert.Empty(position.DomainEvents.OfType<PositionReactivated>());
  }

  [Fact]
  public void Job_grade_events_carry_the_rank_and_the_salary_grade()
  {
    var salaryGradeId = Guid.NewGuid();
    var grade = JobGrade.Create(
      JobGradeCode.Create("G7").Value,
      JobGradeName.Create("Grade 7").Value,
      rankOrder: 70,
      salaryGradeId,
      Actor,
      Guid.NewGuid(),
      Now).Value;

    grade.StampCreated(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

    var raised = Assert.Single(grade.DomainEvents.OfType<JobGradeCreated>());
    Assert.Equal(70, raised.RankOrder);
    Assert.Equal(salaryGradeId, raised.SalaryGradeId);
  }

  // ================================================================================================
  // THE SALARY EVENTS CARRY WHETHER A BAND EXISTS, NEVER THE AMOUNTS (DEC-POS-0018)
  // ================================================================================================
  //
  // `HR.SalaryGrades.View` exists so that reading the org chart does not also mean reading the pay
  // structure. A permission that guards a table while the event stream publishes its contents guards
  // nothing — so this asserts the boolean is TRUE for a priced grade and that no amount reaches the event.
  [Fact]
  public void A_priced_salary_grade_reports_that_it_is_priced_without_publishing_the_amounts()
  {
    var band = SalaryBand.Create(12000m, 15000m, 18000m).Value;
    var grade = NewSalaryGrade(band);

    grade.StampCreated(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

    var raised = Assert.Single(grade.DomainEvents.OfType<SalaryGradeCreated>());
    Assert.True(raised.IsPriced);
    Assert.Equal(70, raised.RankOrder);

    Assert.Empty(raised.GetType().GetProperties()
      .Where(property => property.PropertyType == typeof(decimal) ||
        property.PropertyType == typeof(decimal?)));
  }

  [Fact]
  public void An_unpriced_salary_grade_reports_that_it_is_not_priced()
  {
    var grade = NewSalaryGrade(band: null);

    grade.StampCreated(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

    Assert.False(Assert.Single(grade.DomainEvents.OfType<SalaryGradeCreated>()).IsPriced);
  }

  // Un-pricing is observable on the event, so a consumer can tell "band withdrawn" from "band unchanged".
  [Fact]
  public void Un_pricing_a_salary_grade_reports_that_it_is_no_longer_priced()
  {
    var grade = NewSalaryGrade(SalaryBand.Create(1m, 2m, 3m).Value);

    grade.UpdateDescription(
      SalaryGradeCode.Create("S7").Value,
      SalaryGradeName.Create("Band 7").Value,
      rankOrder: 70,
      band: null,
      Guid.NewGuid(),
      Now);

    Assert.False(Assert.Single(grade.DomainEvents.OfType<SalaryGradeUpdated>()).IsPriced);
  }

  private static Position NewPosition(Guid? jobGradeId) =>
    Position.Create(
      PositionCode.Create("ACC-SR").Value,
      PositionTitle.Create("Senior Accountant").Value,
      jobGradeId,
      Actor,
      Guid.NewGuid(),
      Now).Value;

  private static SalaryGrade NewSalaryGrade(SalaryBand? band) =>
    SalaryGrade.Create(
      SalaryGradeCode.Create("S7").Value,
      SalaryGradeName.Create("Band 7").Value,
      rankOrder: 70,
      band,
      Actor,
      Guid.NewGuid(),
      Now).Value;
}
