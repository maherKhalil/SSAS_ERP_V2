using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Subscriptions.Invoices;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Subscriptions;

public class SubscriptionInvoiceQueries(PlatformDbContext context) : ISubscriptionInvoiceQueries
{
  public async Task<Result<IReadOnlyCollection<InvoiceDto>>> GetInvoicesAsync(CancellationToken cancellationToken = default)
  {
    var invoices = await context.SubscriptionInvoices
      .Include(i => i.Lines)
      .AsNoTracking()
      .OrderByDescending(i => i.IssuedUtc)
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyCollection<InvoiceDto>>(invoices.Select(Map).ToList());
  }

  public async Task<Result<InvoiceDto>> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
  {
    var invoice = await context.SubscriptionInvoices
      .Include(i => i.Lines)
      .AsNoTracking()
      .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

    if (invoice is null)
      return Result.Failure<InvoiceDto>(new Error("Invoice.NotFound", "Invoice not found."));

    return Result.Success(Map(invoice));
  }

  public async Task<Result<IReadOnlyCollection<InvoiceDto>>> GetTenantInvoicesAsync(Guid tenantId, CancellationToken cancellationToken = default)
  {
    var invoices = await context.SubscriptionInvoices
      .Include(i => i.Lines)
      .AsNoTracking()
      .Where(i => i.TenantId == tenantId)
      .OrderByDescending(i => i.IssuedUtc)
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyCollection<InvoiceDto>>(invoices.Select(Map).ToList());
  }

  public async Task<Result<IReadOnlyCollection<PaymentAttemptDto>>> GetInvoiceAttemptsAsync(Guid invoiceId, CancellationToken cancellationToken = default)
  {
    var attempts = await context.SubscriptionPaymentAttempts
      .AsNoTracking()
      .Where(a => a.SubscriptionInvoiceId == invoiceId)
      .OrderByDescending(a => a.AttemptedUtc)
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyCollection<PaymentAttemptDto>>(
      attempts.Select(a => new PaymentAttemptDto(a.Id, a.SubscriptionInvoiceId, a.AttemptedUtc, a.Outcome, a.ProviderReference)).ToList()
    );
  }

  private static InvoiceDto Map(SSAS.Platform.Domain.Subscriptions.SubscriptionInvoice invoice)
  {
    return new InvoiceDto(
      invoice.Id,
      invoice.InvoiceNumber,
      invoice.TenantId,
      invoice.CurrencyCode,
      invoice.IssuedUtc,
      invoice.State,
      invoice.Lines.Select(l => new InvoiceLineDto(l.Id, l.TenantSubscriptionId, l.Amount, l.Description)).ToList()
    );
  }
}
