using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.GL.Application.Journals;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// GL'S OWN POSTING PATH, AGAINST A REAL DATABASE (T-142).
// ==================================================================================================
//
// **GL has TWO implementations of one posting orchestration, and until now only one of them had ever met a
// database.**
//
//   `GlJournalPoster`                 payroll's path      exercised by `PayrollChainSqlServerTests`
//   `PostJournalDraftCommandHandler`  the ROUTE's path    never run against SQL Server
//
// **The untested one is the one a client reaches**, so divergence would be invisible on exactly the side
// that ships. T-132 established that neither had a test driving the route's handler; this is it.
//
// ---- WHAT THIS TARGETS, AND IT IS NOT THE ARITHMETIC.
//
// **The posting arithmetic is already proven through `GlJournalPoster`** — balance, period resolution,
// journal numbering. Repeating it here would test the shared half twice and the unshared half not at all.
//
// **What the two do NOT share is a persisted draft.** The poster builds its draft in memory and never
// stores one. The route's handler LOADS one, and on success **deletes it and its lines inside the same
// transaction that inserts the journal**:
//
// ```
// drafts.GetByIdAsync   ->  .Include(draft => draft.Lines)
// drafts.Remove(draft)  ->  if (draft.Lines.Count > 0) RemoveRange(lines); then Remove(draft)
// ```
//
// ---- ⚠ AND THAT DELETION SITS ON A SEAM THAT HAS ALREADY DETONATED TWICE.
//
// `JournalConfigurations.cs` declares `OnDelete(DeleteBehavior.Cascade)` from draft to lines **and says in
// its own comment that the declaration is inert**:
//
// > *"This declaration expresses INTENT and does not take effect... the removal that actually happens is
// > EXPLICIT, in the repository. **Believing this line cost two shipped defects (FP-013): payroll
// > recalculation and journal-draft update both failed against a real database on orphans nothing
// > deleted.**"*
//
// **So correctness here is a three-link chain and every link is a reading**: the `Include` populates
// `Lines`, which makes `Count > 0` true, which takes the branch that removes them. **Reading is what failed
// twice.** T-142 followed all three and they hold — **and that is exactly why this test exists rather than
// why it does not.**
//
// ---- WHY A PERMISSIVE SCOPE STUB IS DELIBERATE.
//
// `PostJournalDraftCommandHandler` also authorises through `IGlScopeResolver`, which needs a company-access
// resolver, a tenant user and a current user to answer. **Wiring all four would make authorisation the
// subject of this fixture**, and authorisation already has coverage in the API tests. **The stub grants, so
// the assertions below are about persistence and nothing else** — stated because a permissive stub that
// nobody explains reads as an oversight.
public sealed class GlPostingChainSqlServerTests
{
  // ---- THE WHOLE POINT: A PERSISTED DRAFT IS POSTED AND LEAVES NOTHING BEHIND.
  //
  // Asserted on the DATABASE rather than on the handler's result, because the result reports what the
  // handler believes and the rows report what SQL Server kept.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public async Task Posting_a_persisted_draft_writes_the_journal_and_removes_the_draft_and_its_lines()
  {
    await using var fixture = await GlFixture.CreateAsync();
    var seeded = await SeedDraftAsync(fixture);

    await using (var context = fixture.CreateContext())
    {
      var posted = await HandlerFor(context).HandleAsync(new PostJournalDraftCommand(seeded.DraftId));

      Assert.True(posted.IsSuccess, posted.IsFailure ? posted.Error.Code : null);
    }

    await using var verify = fixture.CreateContext();

    // The journal landed.
    Assert.Equal(1, await verify.Set<JournalEntry>().CountAsync());

    // ---- AND THE DRAFT IS GONE, LINES INCLUDED. This is the assertion the cascade would have satisfied if
    // ---- the cascade worked, and it does not: the repository removes the lines explicitly, and nothing has
    // ---- ever checked that against a database.
    Assert.Equal(0, await verify.Set<JournalDraft>().CountAsync());
    Assert.Equal(0, await verify.Set<JournalDraftLine>().CountAsync());
  }

  // ---- ORPHANS ARE ASSERTED SEPARATELY FROM THE HEADER, BECAUSE THEY FAIL SEPARATELY.
  //
  // FP-013's two defects were orphaned LINES, not missing headers — a header delete can succeed while its
  // children survive, and a count of one table cannot see the other. Named so a failure says which.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public async Task No_draft_line_survives_its_header()
  {
    await using var fixture = await GlFixture.CreateAsync();
    var seeded = await SeedDraftAsync(fixture);

    await using (var context = fixture.CreateContext())
    {
      Assert.Equal(2, await context.Set<JournalDraftLine>().CountAsync());
      var posted = await HandlerFor(context).HandleAsync(new PostJournalDraftCommand(seeded.DraftId));
      Assert.True(posted.IsSuccess, posted.IsFailure ? posted.Error.Code : null);
    }

    await using var verify = fixture.CreateContext();

    Assert.Empty(await verify.Set<JournalDraftLine>()
      .Where(line => line.JournalDraftId == seeded.DraftId)
      .ToListAsync());
  }

  private static PostJournalDraftCommandHandler HandlerFor(TenantDbContext context)
  {
    var accessor = new SingleContext(context);

    return new PostJournalDraftCommandHandler(
      new JournalDraftRepository(accessor),
      new JournalEntryRepository(accessor),
      new AccountRepository(accessor),
      new FiscalCalendarRepository(accessor),
      new GrantingScope(),
      new SingleContextUnitOfWork(context),
      new PostingUser());
  }

  private static async Task<(Guid DraftId, Guid DebitAccountId)> SeedDraftAsync(GlFixture fixture)
  {
    await using var context = fixture.CreateContext();

    var debit = Account.Create("1000", "Cash").Value;
    var credit = Account.Create("4100", "Receivables").Value;
    context.Set<Account>().AddRange(debit, credit);
    await context.SaveChangesAsync();

    var year = FiscalYear.Create(
      "FY2026",
      new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
      new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
      [("FY2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero))]).Value;
    year.CompanyId = fixture.CompanyA;
    context.Set<FiscalYear>().Add(year);
    await context.SaveChangesAsync();

    // PERSISTED, which is the whole difference from the poster's path.
    var draft = JournalDraft.Create(fixture.EntryDate, "Chain", "CHAIN").Value;
    draft.CompanyId = fixture.CompanyA;
    draft.ReplaceLines([(debit.Id, 100m, 0m, "debit"), (credit.Id, 0m, 100m, "credit")]);
    context.Set<JournalDraft>().Add(draft);
    await context.SaveChangesAsync();

    return (draft.Id, debit.Id);
  }

  private sealed class SingleContext(TenantDbContext context) : ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<DbContext>(context);
  }

  private sealed class SingleContextUnitOfWork(TenantDbContext context) : ITenantUnitOfWork
  {
    public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      Result.Success(await context.SaveChangesAsync(cancellationToken));

    // Adapts EF's transaction to the product's `ITransaction`, the same translation `TenantUnitOfWork`
    // performs — a double that returned EF's type directly would not compile against the interface the
    // handler holds, which is the interface doing its job.
    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      new EfTransaction(await context.Database.BeginTransactionAsync(cancellationToken));

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
      public Task CommitAsync(CancellationToken cancellationToken = default) =>
        transaction.CommitAsync(cancellationToken);

      public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        transaction.RollbackAsync(cancellationToken);

      public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
  }

  // Grants. See the note in the file header: authorisation is covered elsewhere and wiring it here would
  // make it this fixture's subject rather than persistence.
  private sealed class GrantingScope : IGlScopeResolver
  {
    public Result RequirePermission(string permissionName) => Result.Success();

    public Task<Result<GlReadScope>> ResolveAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Posting does not resolve a read scope.");

    public Task<Result> AuthorizeAsync(
      string permissionName, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success());
  }

  private sealed class PostingUser : ICurrentUser
  {
    public string? UserId => "gl-posting-chain";

    public string? UserName => "gl-posting-chain";

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }
}
