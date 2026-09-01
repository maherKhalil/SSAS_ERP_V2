using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Tests.Departments;

// THE DEPARTMENT AGGREGATE'S LOCAL INVARIANTS (FP-007 Phase 1, ADR-026).
//
// ---- WHAT IS DELIBERATELY NOT TESTED HERE.
//
// Cross-company parents, inactive parents, descendant cycles, manager employment status, and "cannot
// deactivate while active children remain" are all cross-aggregate rules requiring repository lookups. They
// are Phase 2, they are NOT implemented, and there is no test here asserting them — because a test that
// passed against unimplemented behaviour would be asserting the absence of enforcement while reading like
// its presence.
public sealed class DepartmentDomainTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

  private const string Actor = "tester";

  [Fact]
  // CITED BY B18 pass 17, body-confirmed. TWO criteria, ONE assertion, and they sit in DIFFERENT
  // sections of the specification: `AC-DEP-0001` is *creating with a code, a name and no parent
  // produces an Active root*; `AC-DEP-0025` is *a new department is Active*. The mechanism
  // grouping found them together; the document's own headings would have searched twice and
  // found this same test twice.
  [Trait("Criterion", "AC-DEP-0001")]
  [Trait("Criterion", "AC-DEP-0025")]
  public void A_valid_department_is_created_active_at_the_root()
  {
    var department = CreateDepartment("SALES", "Sales");

    Assert.True(department.IsSuccess);
    Assert.Equal(DepartmentStatus.Active, department.Value.Status);
    Assert.Null(department.Value.ParentDepartmentId);
    Assert.Equal(Actor, department.Value.StatusChangedBy);
    Assert.Equal(Now, department.Value.StatusChangedUtc);
    Assert.NotEqual(Guid.Empty, department.Value.DepartmentId);
  }

  [Fact]
  // CITED BY B18 pass 17, body-confirmed: a department created with a parent in the same company
  // is placed beneath it.
  [Trait("Criterion", "AC-DEP-0010")]
  public void A_department_may_be_created_beneath_a_parent()
  {
    var parentId = Guid.NewGuid();

    var department = CreateDepartment("SALES-N", "Sales North", parentId);

    Assert.True(department.IsSuccess);
    Assert.Equal(parentId, department.Value.ParentDepartmentId);
  }

  // ---- CODE NORMALIZATION. The stored display value keeps its casing; the normalized value does not.
  [Theory]
  [InlineData("  sales  ", "sales", "SALES")]
  [InlineData("Sales", "Sales", "SALES")]
  [InlineData("SALES", "SALES", "SALES")]
  public void The_code_is_trimmed_for_display_and_upper_cased_for_comparison(
    string input, string expectedValue, string expectedNormalized)
  {
    var department = CreateDepartment(input, "Sales");

    Assert.True(department.IsSuccess);
    Assert.Equal(expectedValue, department.Value.Code.Value);
    Assert.Equal(expectedNormalized, department.Value.NormalizedCode);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  // CITED BY B18 pass 17, body-confirmed: `AC-DEP-0005` is *a blank or whitespace-only name OR
  // code is refused* -- four cases. This theory covers code x {null, empty, whitespace}; its
  // sibling `An_empty_name_is_refused` covers the name half on the same three inputs.
  [Trait("Criterion", "AC-DEP-0005")]
  public void An_empty_code_is_refused(string? code)
  {
    var result = DepartmentCode.Create(code);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidCode, result.Error);
  }

  [Fact]
  public void An_overlength_code_is_refused()
  {
    var result = DepartmentCode.Create(new string('A', DepartmentCode.MaximumLength + 1));

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidCode, result.Error);
  }

  // A code exactly at the limit is valid. The boundary is asserted from both sides so an off-by-one in
  // either direction fails.
  [Fact]
  public void A_code_at_the_maximum_length_is_accepted()
  {
    var result = DepartmentCode.Create(new string('A', DepartmentCode.MaximumLength));

    Assert.True(result.IsSuccess);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  // CITED BY B18 pass 17: `AC-DEP-0005`'s NAME half. See `An_empty_code_is_refused` for the
  // other half -- neither test alone covers the criterion.
  [Trait("Criterion", "AC-DEP-0005")]
  public void An_empty_name_is_refused(string? name)
  {
    var result = DepartmentName.Create(name);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidName, result.Error);
  }

  [Fact]
  public void An_overlength_name_is_refused()
  {
    var result = DepartmentName.Create(new string('A', DepartmentName.MaximumLength + 1));

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidName, result.Error);
  }

  [Fact]
  public void A_name_at_the_maximum_length_is_accepted()
  {
    var result = DepartmentName.Create(new string('A', DepartmentName.MaximumLength));

    Assert.True(result.IsSuccess);
  }

  // The name is NOT normalized for comparison, unlike the code. Two departments may share a name.
  [Fact]
  public void The_name_keeps_its_casing_and_is_not_normalized()
  {
    var name = DepartmentName.Create("  Sales North  ");

    Assert.True(name.IsSuccess);
    Assert.Equal("Sales North", name.Value.Value);
  }

  [Fact]
  public void A_blank_actor_is_refused()
  {
    var code = DepartmentCode.Create("SALES").Value;
    var name = DepartmentName.Create("Sales").Value;

    var result = Department.Create(code, name, parentDepartmentId: null, "  ", Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidActor, result.Error);
  }

  // ---- LIFECYCLE.
  [Fact]
  public void An_active_department_can_be_deactivated_and_reactivated()
  {
    var department = CreateDepartment("SALES", "Sales").Value;

    var deactivated = department.Deactivate("closer", Guid.NewGuid(), Now.AddHours(1));

    Assert.True(deactivated.IsSuccess);
    Assert.Equal(DepartmentStatus.Inactive, department.Status);
    Assert.Equal("closer", department.StatusChangedBy);
    Assert.Equal(Now.AddHours(1), department.StatusChangedUtc);

    var reactivated = department.Reactivate("opener", Guid.NewGuid(), Now.AddHours(2));

    Assert.True(reactivated.IsSuccess);
    Assert.Equal(DepartmentStatus.Active, department.Status);
    Assert.Equal("opener", department.StatusChangedBy);
  }

  // `Inactive` is REVERSIBLE, which is what distinguishes it from Employee's terminal `Terminated`.
  [Fact]
  public void Deactivating_twice_is_refused()
  {
    var department = CreateDepartment("SALES", "Sales").Value;
    department.Deactivate(Actor, Guid.NewGuid(), Now);

    var again = department.Deactivate(Actor, Guid.NewGuid(), Now.AddHours(1));

    Assert.True(again.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidTransition, again.Error);
  }

  [Fact]
  public void Reactivating_an_active_department_is_refused()
  {
    var department = CreateDepartment("SALES", "Sales").Value;

    var result = department.Reactivate(Actor, Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.InvalidTransition, result.Error);
  }

  // ---- HIERARCHY: THE ONE CASE PHASE 1 DECIDES.
  [Fact]
  public void A_department_cannot_be_its_own_parent()
  {
    var department = CreateDepartment("SALES", "Sales").Value;

    var result = department.ChangeParent(department.Id, Guid.NewGuid(), Now);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.ParentIsSelf, result.Error);
    Assert.Null(department.ParentDepartmentId);
  }

  [Fact]
  public void A_department_can_be_moved_beneath_another_and_back_to_the_root()
  {
    var department = CreateDepartment("SALES", "Sales").Value;
    var newParentId = Guid.NewGuid();

    var moved = department.ChangeParent(newParentId, Guid.NewGuid(), Now);

    Assert.True(moved.IsSuccess);
    Assert.Equal(newParentId, department.ParentDepartmentId);

    var promoted = department.ChangeParent(null, Guid.NewGuid(), Now.AddHours(1));

    Assert.True(promoted.IsSuccess);
    Assert.Null(department.ParentDepartmentId);
  }

  // ---- THE DESCRIPTIVE UPDATE TOUCHES NOTHING ELSE.
  [Fact]
  public void Updating_the_description_changes_neither_status_nor_parent()
  {
    var department = CreateDepartment("SALES", "Sales").Value;
    var parentId = Guid.NewGuid();
    department.ChangeParent(parentId, Guid.NewGuid(), Now);

    var result = department.UpdateDescription(
      DepartmentCode.Create("SLS").Value, DepartmentName.Create("Sales Team").Value, Guid.NewGuid(), Now);

    Assert.True(result.IsSuccess);
    Assert.Equal("SLS", department.Code.Value);
    Assert.Equal("SLS", department.NormalizedCode);
    Assert.Equal("Sales Team", department.Name.Value);
    Assert.Equal(DepartmentStatus.Active, department.Status);
    Assert.Equal(parentId, department.ParentDepartmentId);
  }

  private static SSAS.BuildingBlocks.Domain.Result<Department> CreateDepartment(
    string code, string name, Guid? parentDepartmentId = null) =>
    Department.Create(
      DepartmentCode.Create(code).Value,
      DepartmentName.Create(name).Value,
      parentDepartmentId,
      Actor,
      Guid.NewGuid(),
      Now);
}
