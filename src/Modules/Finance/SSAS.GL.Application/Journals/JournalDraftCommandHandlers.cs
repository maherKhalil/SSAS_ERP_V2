using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.GL.Application.Abstractions;
using SSAS.GL.Application.Permissions;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Journals;

namespace SSAS.GL.Application.Journals;

// THE DRAFT'S WRITE PATH (OD-GL-0007, option 3).
//
// Drafts are the mutable half of the two-aggregate model, and every operation here is an ordinary one —
// create, edit, replace lines, discard. That ordinariness is the point: it exists so the POSTED half can be
// append-only from creation, with `BR-GL-0002` enforced by the write boundary rather than by a guard a
// future path could bypass.
//
// `GL.Drafts.Manage` is separate from `GL.Journals.Post` throughout. A user who prepares work for someone
// else to post is a real separation of duties, and it is only expressible because the draft is a distinct
// aggregate.

public sealed record JournalLineInput(Guid AccountId, decimal Debit, decimal Credit, string? Description);

public sealed record CreateJournalDraftCommand(
  Guid CompanyId,
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  IReadOnlyList<JournalLineInput> Lines);

public sealed class CreateJournalDraftCommandHandler(
  IJournalDraftRepository drafts,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<Guid>> HandleAsync(
    CreateJournalDraftCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(GlScopeErrors.InvalidActor);
    }

    var authorized = await scope.AuthorizeAsync(
      GlPermissionNames.ManageDrafts, command.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(authorized.Error);
    }

    var draft = JournalDraft.Create(command.EntryDateUtc, command.Description, command.Reference);
    if (draft.IsFailure)
    {
      return Result.Failure<Guid>(draft.Error);
    }

    // ---- A DRAFT IS NOT REQUIRED TO BALANCE, AND LINES ARE OPTIONAL AT CREATION.
    //
    // That is what a draft IS: work in progress that does not yet satisfy `BR-GL-0001` needs somewhere to
    // live. The balance rule is a precondition of POSTING (`DEC-GL-0008`), not of drafting — enforcing it
    // here would make the draft aggregate pointless, since anything that could be saved could be posted.
    //
    // The line SHAPE is still validated: a line carrying both a debit and a credit, or neither, or a
    // negative amount, is refused now rather than at post time, because those are malformed rather than
    // incomplete.
    if (command.Lines.Count > 0)
    {
      var lines = draft.Value.ReplaceLines(
        [.. command.Lines.Select(line => (line.AccountId, line.Debit, line.Credit, line.Description))]);
      if (lines.IsFailure)
      {
        return Result.Failure<Guid>(lines.Error);
      }
    }

    draft.Value.CompanyId = command.CompanyId;
    await drafts.AddAsync(draft.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(draft.Value.Id);
  }
}

public sealed record UpdateJournalDraftCommand(
  Guid JournalDraftId,
  DateTimeOffset EntryDateUtc,
  string Description,
  string? Reference,
  IReadOnlyList<JournalLineInput> Lines,
  byte[]? RowVersion);

public sealed class UpdateJournalDraftCommandHandler(
  IJournalDraftRepository drafts,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    UpdateJournalDraftCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(GlScopeErrors.InvalidActor);
    }

    var draft = await drafts.GetByIdAsync(command.JournalDraftId, cancellationToken);
    if (draft is null)
    {
      return Result.Failure(JournalErrors.DraftNotFound);
    }

    // The company comes from the loaded draft, never from the caller — the same reasoning the period
    // handler uses. A caller who could name the company could name one they may not reach.
    var authorized = await scope.AuthorizeAsync(
      GlPermissionNames.ManageDrafts, draft.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    var updated = draft.Update(command.EntryDateUtc, command.Description, command.Reference);
    if (updated.IsFailure)
    {
      return updated;
    }

    // ---- THE OLD LINES ARE DELETED EXPLICITLY FIRST (FP-013 follow-up).
    //
    // The platform sets every foreign key to `Restrict` after the module contributors run, so this type's
    // configured cascade never applies and the lines `ReplaceLines` clears would be orphans nothing deletes,
    // against a non-nullable foreign key EF cannot null. Updating a draft that already had lines failed on
    // exactly that — unobserved, because this path had never been driven against real SQL through this
    // handler. Payroll's identical defect is what led anyone to look.
    await drafts.RemoveLinesAsync(draft, cancellationToken);

    // Lines are REPLACED WHOLESALE rather than patched — see `JournalDraft.ReplaceLines` for why. An empty
    // list is a legitimate edit: it clears the draft back to a header, which a user rebuilding a journal
    // from scratch needs.
    var lines = draft.ReplaceLines(
      [.. command.Lines.Select(line => (line.AccountId, line.Debit, line.Credit, line.Description))]);
    if (lines.IsFailure)
    {
      return lines;
    }

    if (command.RowVersion is { Length: > 0 })
    {
      draft.RowVersion = command.RowVersion;
    }

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}

// ---- DISCARDING A DRAFT IS THE ONLY DELETE IN THIS MODULE.
//
// It is not an exception to `BR-GL-0002`; it is the reason `OD-GL-0007` chose two aggregates. A draft was
// never part of the ledger, so removing one destroys no history — whereas deleting a posted journal would,
// which is why `JournalEntry` carries `IAppendOnlyEntity` and the write boundary refuses it outright.
public sealed record DiscardJournalDraftCommand(Guid JournalDraftId);

public sealed class DiscardJournalDraftCommandHandler(
  IJournalDraftRepository drafts,
  IGlScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentUser currentUser)
{
  public async Task<Result> HandleAsync(
    DiscardJournalDraftCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(GlScopeErrors.InvalidActor);
    }

    var draft = await drafts.GetByIdAsync(command.JournalDraftId, cancellationToken);
    if (draft is null)
    {
      return Result.Failure(JournalErrors.DraftNotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      GlPermissionNames.ManageDrafts, draft.CompanyId, cancellationToken);
    if (authorized.IsFailure)
    {
      return authorized;
    }

    drafts.Remove(draft);

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
