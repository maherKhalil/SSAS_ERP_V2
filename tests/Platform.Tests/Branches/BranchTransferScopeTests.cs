using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Branches;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Infrastructure.Branches;

namespace SSAS.Platform.Tests.Branches;

// THE SANCTIONED BRANCH-TRANSFER CHANNEL, WITHOUT A DATABASE (FP-006C2, ADR-024 decision 3).
//
// These cover the parts that decide whether the exception stays narrow: what a declaration will and will
// not match, and how long it lives. The parts that need authoritative state — destination authorization,
// administrator authority, inactive-source verification and the write boundary itself — are proven against
// real SQL Server, because an in-memory provider would agree with all of them and prove none.
public sealed class BranchTransferScopeTests
{
  private static readonly Guid Source = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid Destination = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid Elsewhere = Guid.Parse("33333333-3333-3333-3333-333333333333");

  // ---- A DECLARATION NAMES ONE ENTITY, ONE SOURCE AND ONE DESTINATION.
  [Fact]
  public void A_valid_declaration_carries_exactly_one_transition()
  {
    var entity = new Probe();

    var declaration = BranchTransferDeclaration.Create(
      entity, Source, Destination, BranchTransferMode.CurrentBranch);

    Assert.True(declaration.IsSuccess);
    Assert.Same(entity, declaration.Value.Entity);
    Assert.Equal(typeof(Probe), declaration.Value.EntityType);
    Assert.Equal(Source, declaration.Value.SourceBranchId);
    Assert.Equal(Destination, declaration.Value.DestinationBranchId);
    Assert.Equal(BranchTransferMode.CurrentBranch, declaration.Value.Mode);
  }

  // ---- AN UNUSABLE DECLARATION IS REFUSED rather than created and then found not to match anything.
  [Fact]
  public void An_empty_branch_identifier_is_refused()
  {
    Assert.Equal(
      BranchTransferErrors.TransferInvalid.Code,
      BranchTransferDeclaration.Create(new Probe(), Guid.Empty, Destination, BranchTransferMode.CurrentBranch)
        .Error.Code);

    Assert.Equal(
      BranchTransferErrors.TransferInvalid.Code,
      BranchTransferDeclaration.Create(new Probe(), Source, Guid.Empty, BranchTransferMode.CurrentBranch)
        .Error.Code);
  }

  // ---- A TRANSFER TO THE BRANCH IT IS ALREADY IN IS NOT A TRANSFER.
  [Fact]
  public void A_declaration_whose_source_equals_its_destination_is_refused()
  {
    var declaration = BranchTransferDeclaration.Create(
      new Probe(), Source, Source, BranchTransferMode.CurrentBranch);

    Assert.True(declaration.IsFailure);
    Assert.Equal(BranchTransferErrors.TransferInvalid.Code, declaration.Error.Code);
  }

  // ---- IT AUTHORIZES EXACTLY THE TRANSITION IT NAMES, and only when every clause holds.
  [Fact]
  public void A_declaration_authorizes_only_its_own_entity_source_and_destination()
  {
    var entity = new Probe();
    var other = new Probe();
    var declaration = BranchTransferDeclaration.Create(
      entity, Source, Destination, BranchTransferMode.CurrentBranch).Value;

    Assert.True(declaration.Authorizes(entity, Source, Destination));

    // ANOTHER ENTITY MAKING THE IDENTICAL MOVE IS NOT AUTHORIZED. This is the property that stops a
    // declaration becoming a "branch transition pair" permit that sweeps up every entity moving between the
    // same two branches (ADR-024 decision 3).
    Assert.False(declaration.Authorizes(other, Source, Destination));

    // Wrong source, wrong destination, and both wrong.
    Assert.False(declaration.Authorizes(entity, Elsewhere, Destination));
    Assert.False(declaration.Authorizes(entity, Source, Elsewhere));
    Assert.False(declaration.Authorizes(entity, Elsewhere, Elsewhere));

    // And the reverse move is a different transition, not the same one read backwards.
    Assert.False(declaration.Authorizes(entity, Destination, Source));
  }

  // ---- IDENTITY IS THE INSTANCE, NOT THE VALUE. Two entities that compare equal by every field are still
  // two entities, and a declaration for one must not authorize the other.
  [Fact]
  public void Two_distinct_instances_with_identical_state_are_not_interchangeable()
  {
    var entity = new Probe { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), BranchId = Source };
    var twin = new Probe { Id = entity.Id, BranchId = entity.BranchId };

    var declaration = BranchTransferDeclaration.Create(
      entity, Source, Destination, BranchTransferMode.CurrentBranch).Value;

    Assert.True(declaration.Authorizes(entity, Source, Destination));
    Assert.False(declaration.Authorizes(twin, Source, Destination));
  }

  // ---- THE SCOPE STARTS EMPTY, HOLDS ONE DECLARATION, AND CLEARS ON DISPOSAL.
  [Fact]
  public void A_scope_holds_one_declaration_for_the_lifetime_of_its_handle()
  {
    var scope = new BranchTransferScope();
    var declaration = Declaration(new Probe());

    Assert.Null(scope.Current);

    var begun = scope.Begin(declaration);
    Assert.True(begun.IsSuccess);
    Assert.Same(declaration, scope.Current);

    begun.Value.Dispose();
    Assert.Null(scope.Current);
  }

  // ---- NO LEAKAGE PAST THE OPERATION. This is the property the whole lifetime design exists for: a
  // declaration must not survive into a later save.
  [Fact]
  public void A_disposed_declaration_authorizes_nothing_afterwards()
  {
    var scope = new BranchTransferScope();

    using (scope.Begin(Declaration(new Probe())).Value)
    {
      Assert.NotNull(scope.Current);
    }

    Assert.Null(scope.Current);
  }

  // ---- AND IT IS CLEARED ON THE EXCEPTION PATH TOO. An operation that throws mid-transfer must not leave
  // an authorization standing for whatever runs next in the same scope.
  [Fact]
  public void A_declaration_is_cleared_when_the_operation_throws()
  {
    var scope = new BranchTransferScope();

    void FailAfterDeclaring()
    {
      using var transfer = scope.Begin(Declaration(new Probe())).Value;
      throw new InvalidOperationException("the operation failed after declaring");
    }

    Assert.Throws<InvalidOperationException>(FailAfterDeclaring);

    Assert.Null(scope.Current);
  }

  // ---- NESTING IS REFUSED RATHER THAN STACKED. Two open declarations would make "which transfer is in
  // force" ambiguous at the boundary, and the safe reading of an ambiguous authorization is none.
  [Fact]
  public void A_second_declaration_is_refused_while_one_is_open()
  {
    var scope = new BranchTransferScope();
    var first = Declaration(new Probe());

    using var open = scope.Begin(first).Value;

    var second = scope.Begin(Declaration(new Probe()));

    Assert.True(second.IsFailure);
    Assert.Equal(BranchTransferErrors.TransferAlreadyInProgress.Code, second.Error.Code);

    // The refusal changed nothing: the first declaration is still the one in force.
    Assert.Same(first, scope.Current);
  }

  // ---- A NEW DECLARATION IS AVAILABLE ONCE THE FIRST CLOSES, so refusing to nest does not mean refusing
  // to transfer twice in one request.
  [Fact]
  public void A_new_declaration_may_be_opened_after_the_previous_one_closes()
  {
    var scope = new BranchTransferScope();

    scope.Begin(Declaration(new Probe())).Value.Dispose();

    var second = Declaration(new Probe());
    using var open = scope.Begin(second).Value;

    Assert.Same(second, scope.Current);
  }

  // ---- DISPOSING TWICE IS A NO-OP, and a stale handle disposed after the scope moved on must not cancel a
  // transfer it did not open.
  [Fact]
  public void A_stale_handle_does_not_clear_a_later_declaration()
  {
    var scope = new BranchTransferScope();

    var firstHandle = scope.Begin(Declaration(new Probe())).Value;
    firstHandle.Dispose();

    var second = Declaration(new Probe());
    using var open = scope.Begin(second).Value;

    // The already-disposed handle from the first transfer is disposed again, out of order.
    firstHandle.Dispose();

    Assert.Same(second, scope.Current);
  }

  // ---- TWO SCOPES DO NOT SEE ONE ANOTHER. Scoped instance state, never static: two concurrent operations
  // must not be able to observe or overwrite one another's declarations.
  [Fact]
  public void Two_scopes_hold_independent_declarations()
  {
    var first = new BranchTransferScope();
    var second = new BranchTransferScope();

    var firstDeclaration = Declaration(new Probe());
    using var open = first.Begin(firstDeclaration).Value;

    Assert.Same(firstDeclaration, first.Current);
    Assert.Null(second.Current);

    // And the second scope can open its own without being blocked by the first.
    var secondDeclaration = Declaration(new Probe());
    var begun = second.Begin(secondDeclaration);

    Assert.True(begun.IsSuccess);
    Assert.Same(secondDeclaration, second.Current);
    Assert.Same(firstDeclaration, first.Current);
  }

  // ---- CONCURRENT OPERATIONS ON SEPARATE SCOPES STAY SEPARATE under real parallelism, which is the shape
  // the scoped registration is meant to guarantee.
  [Fact]
  public async Task Concurrent_operations_do_not_observe_one_anothers_declarations()
  {
    var observed = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
    {
      var scope = new BranchTransferScope();
      var declaration = Declaration(new Probe());

      using var open = scope.Begin(declaration).Value;

      // Give the scheduler every chance to interleave these before the assertion is taken.
      Thread.Yield();

      return ReferenceEquals(scope.Current, declaration);
    })));

    Assert.All(observed, isolated => Assert.True(isolated));
  }

  private static BranchTransferDeclaration Declaration(Probe entity) =>
    BranchTransferDeclaration.Create(entity, Source, Destination, BranchTransferMode.CurrentBranch).Value;

  private sealed class Probe : IBranchOwnedEntity
  {
    public Guid Id { get; set; }

    public Guid BranchId { get; set; }
  }
}
