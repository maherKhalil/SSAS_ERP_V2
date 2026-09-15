using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Application.Subscriptions.Invoices;

public record CreateInvoiceLineCommand(Guid TenantSubscriptionId, decimal Amount, string Description);
public record CreateInvoiceCommand(Guid TenantId, string CurrencyCode, DateTimeOffset IssuedUtc, List<CreateInvoiceLineCommand> Lines);
public record UpdateInvoiceDraftCommand(Guid InvoiceId, string CurrencyCode, DateTimeOffset IssuedUtc, List<CreateInvoiceLineCommand> Lines);
public record IssueInvoiceCommand(Guid InvoiceId, string InvoiceNumber);
public record VoidInvoiceCommand(Guid InvoiceId);

public sealed class InvoicesCommandHandler(
  ISubscriptionInvoiceRepository invoiceRepository,
  IPlatformUnitOfWork unitOfWork)
{
  public async Task<Result<Guid>> HandleCreateAsync(CreateInvoiceCommand command, CancellationToken cancellationToken)
  {
    var draftResult = SubscriptionInvoice.CreateDraft(command.TenantId, command.CurrencyCode, command.IssuedUtc);
    if (draftResult.IsFailure) return Result.Failure<Guid>(draftResult.Error);

    var invoice = draftResult.Value;
    foreach (var line in command.Lines)
    {
      invoice.AddLine(new SubscriptionInvoiceLine(Guid.NewGuid(), invoice.Id, line.TenantSubscriptionId, line.Amount, line.Description));
    }

    invoiceRepository.Add(invoice);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result.Success(invoice.Id);
  }

  public async Task<Result> HandleUpdateDraftAsync(UpdateInvoiceDraftCommand command, CancellationToken cancellationToken)
  {
    var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);
    if (invoice is null) return Result.Failure(new Error("Invoice.NotFound", "Invoice not found."));

    var updateResult = invoice.UpdateDraft(command.CurrencyCode, command.IssuedUtc);
    if (updateResult.IsFailure) return updateResult;

    invoice.ClearLines();
    foreach (var line in command.Lines)
    {
      invoice.AddLine(new SubscriptionInvoiceLine(Guid.NewGuid(), invoice.Id, line.TenantSubscriptionId, line.Amount, line.Description));
    }

    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }

  public async Task<Result> HandleIssueAsync(IssueInvoiceCommand command, CancellationToken cancellationToken)
  {
    var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);
    if (invoice is null) return Result.Failure(new Error("Invoice.NotFound", "Invoice not found."));

    var issueResult = invoice.Issue(command.InvoiceNumber);
    if (issueResult.IsFailure) return issueResult;

    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }

  public async Task<Result> HandleVoidAsync(VoidInvoiceCommand command, CancellationToken cancellationToken)
  {
    var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);
    if (invoice is null) return Result.Failure(new Error("Invoice.NotFound", "Invoice not found."));

    var voidResult = invoice.Void();
    if (voidResult.IsFailure) return voidResult;

    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
