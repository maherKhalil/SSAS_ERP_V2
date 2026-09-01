using System.Reflection;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Tests.Positions;

// THE APPEND-ONLY POSITION HISTORY (FP-008 Phase 1, DEC-POS-0008).
//
// ---- THESE TESTS REACH `internal` FACTORIES, AND THAT IS THE POINT.
//
// `DEC-POS-0008` requires the factories to be `internal` so nothing outside the domain assembly can
// fabricate an audit row. Their production caller is `Employee`, which does not reference positions until
// Phase 3. FP-007 resolved the same tension by shipping its equivalents PUBLIC in Phase 1 and narrowing in
// Phase 3 — leaving a window in which the wider surface existed to be depended on.
//
// This phase takes the third option: the factories are `internal` from the outset and `SSAS.HR.Domain`
// grants `InternalsVisibleTo` to this assembly, following the convention `SSAS.Platform.Domain` already
// uses. The guards are therefore proven NOW rather than on the promise of a later narrowing.
public sealed class EmployeePositionAssignmentDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

  private const string Actor = "tester";

  // ---- THE INITIAL RECORD IS THE ONE WITH NO SOURCE, AND NOTHING ELSE IDENTIFIES IT.
  [Fact]
  public void The_initial_record_has_a_null_source()
  {
    var destination = Guid.NewGuid();

    var record = EmployeePositionAssignment.CreateInitial(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), destination, Now, Actor);

    Assert.True(record.IsSuccess);
    Assert.Null(record.Value.SourcePositionId);
    Assert.Equal(destination, record.Value.DestinationPositionId);
    Assert.Equal(Actor, record.Value.ChangedBy);
    Assert.Null(record.Value.ReasonCode);
    Assert.Null(record.Value.ReasonText);
  }

  [Fact]
  public void A_change_record_carries_both_ends()
  {
    var source = Guid.NewGuid();
    var destination = Guid.NewGuid();

    var record = EmployeePositionAssignment.CreateChange(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), source, destination, Now, Actor,
      "PROMOTION", "Annual review outcome.");

    Assert.True(record.IsSuccess);
    Assert.Equal(source, record.Value.SourcePositionId);
    Assert.Equal(destination, record.Value.DestinationPositionId);
    Assert.Equal("PROMOTION", record.Value.ReasonCode);
    Assert.Equal("Annual review outcome.", record.Value.ReasonText);
  }

  // A MOVE TO THE POSITION ALREADY HELD IS NOT A MOVE. A check constraint says the same thing to SQL Server,
  // so a record can never describe one even if written directly.
  [Fact]
  public void A_change_from_a_position_to_itself_is_refused()
  {
    var same = Guid.NewGuid();

    var record = EmployeePositionAssignment.CreateChange(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), same, same, Now, Actor, null, null);

    Assert.True(record.IsFailure);
    Assert.Equal(PositionErrors.InvalidPositionAssignment, record.Error);
  }

  [Fact]
  public void An_empty_identifier_anywhere_is_refused()
  {
    Assert.Equal(
      PositionErrors.InvalidPositionAssignment,
      EmployeePositionAssignment.CreateInitial(
        Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Now, Actor).Error);

    Assert.Equal(
      PositionErrors.InvalidPositionAssignment,
      EmployeePositionAssignment.CreateInitial(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Now, Actor).Error);

    Assert.Equal(
      PositionErrors.InvalidPositionAssignment,
      EmployeePositionAssignment.CreateChange(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(),
        Now, Actor, null, null).Error);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void A_record_without_a_trusted_actor_is_refused(string? actor)
  {
    var record = EmployeePositionAssignment.CreateInitial(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, actor!);

    Assert.True(record.IsFailure);
    Assert.Equal(PositionErrors.InvalidActor, record.Error);
  }

  [Fact]
  public void An_over_long_reason_code_or_text_is_refused()
  {
    var longCode = EmployeePositionAssignment.CreateChange(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, Actor,
      new string('X', EmployeePositionAssignment.ReasonCodeMaximumLength + 1), null);

    var longText = EmployeePositionAssignment.CreateChange(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, Actor,
      null, new string('X', EmployeePositionAssignment.ReasonTextMaximumLength + 1));

    Assert.Equal(PositionErrors.InvalidPositionAssignment, longCode.Error);
    Assert.Equal(PositionErrors.InvalidPositionAssignment, longText.Error);
  }

  // Blank reasons become null rather than empty strings, so "no reason given" has ONE representation in the
  // column rather than two a query would have to know about.
  [Fact]
  public void A_blank_reason_is_stored_as_null()
  {
    var record = EmployeePositionAssignment.CreateChange(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, Actor,
      "   ", "   ");

    Assert.True(record.IsSuccess);
    Assert.Null(record.Value.ReasonCode);
    Assert.Null(record.Value.ReasonText);
  }

  [Fact]
  public void The_effective_instant_is_normalized_to_utc()
  {
    var offset = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.FromHours(3));

    var record = EmployeePositionAssignment.CreateInitial(
      Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), offset, Actor);

    Assert.Equal(TimeSpan.Zero, record.Value.EffectiveFromUtc.Offset);
    Assert.Equal(offset.UtcDateTime, record.Value.EffectiveFromUtc.UtcDateTime);
  }

  // ================================================================================================
  // APPEND-ONLY IS STRUCTURAL, NOT A CONVENTION
  // ================================================================================================

  // NO PUBLIC MUTATOR OF ANY KIND. A history record that could be edited is not history.
  [Fact]
  public void The_record_exposes_no_public_setter()
  {
    var settable = typeof(EmployeePositionAssignment)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance)
      .Where(property => property.SetMethod is { IsPublic: true })
      .Select(property => property.Name)
      .Except(["TenantId", "CompanyId"])   // the ownership interfaces require these for stamping
      .ToArray();

    Assert.Empty(settable);
  }

  // NO RowVersion. A record that is never updated has no concurrency state to protect; concurrent changes
  // serialize on `Employee.RowVersion` instead, exactly as transfers and department changes do.
  [Fact]
  public void The_record_carries_no_row_version()
  {
    Assert.Null(typeof(EmployeePositionAssignment).GetProperty(nameof(SSAS.HR.Domain.Employees.Employee.RowVersion)));
  }

  // NO EffectiveToUtc. Closing an interval would mean UPDATING the previous row, which is precisely the
  // history mutation this model exists to prevent. The interval is derived by ordering.
  [Fact]
  public void The_record_carries_no_end_date()
  {
    Assert.Null(typeof(EmployeePositionAssignment).GetProperty("EffectiveToUtc"));
  }

  // NOT BRANCH OWNED. A position change says nothing about a branch.
  [Fact]
  public void The_record_is_tenant_and_company_owned_and_never_branch_owned()
  {
    var interfaces = typeof(EmployeePositionAssignment)
      .GetInterfaces().Select(type => type.Name).ToArray();

    Assert.Contains("ITenantOwnedEntity", interfaces);
    Assert.Contains("ICompanyOwnedEntity", interfaces);
    Assert.Contains("IAppendOnlyEntity", interfaces);
    Assert.DoesNotContain(nameof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity), interfaces);
    Assert.Null(typeof(EmployeePositionAssignment).GetProperty(nameof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity.BranchId)));
  }

  // ---- THE FACTORY PROTECTION ITSELF (DEC-POS-0008).
  //
  // `internal` FROM THE OUTSET. If either factory is ever widened to `public`, this fails — which is what
  // stops the FP-007 sequence, where a Phase 1 public surface had to be narrowed later, from repeating.
  [Theory]
  [InlineData(nameof(EmployeePositionAssignment.CreateInitial))]
  [InlineData(nameof(EmployeePositionAssignment.CreateChange))]
  public void The_history_factories_are_internal(string factoryName)
  {
    var factory = typeof(EmployeePositionAssignment).GetMethod(
      factoryName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

    Assert.NotNull(factory);
    Assert.False(factory!.IsPublic);
    Assert.True(factory.IsAssembly);
  }

  // AND THERE IS NO PUBLIC CONSTRUCTOR EITHER — a factory guard that left one open would guard nothing.
  [Fact]
  public void The_record_exposes_no_public_constructor()
  {
    var constructors = typeof(EmployeePositionAssignment)
      .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

    Assert.Empty(constructors);
  }
}
