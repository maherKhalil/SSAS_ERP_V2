using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Subscriptions.Invoices;

public record GetInvoicesQuery();
public record GetInvoiceByIdQuery(Guid InvoiceId);
public record GetTenantInvoicesQuery(Guid TenantId);
public record GetInvoiceAttemptsQuery(Guid InvoiceId);

public interface ISubscriptionInvoiceQueries
{
  Task<Result<IReadOnlyCollection<InvoiceDto>>> GetInvoicesAsync(CancellationToken cancellationToken = default);
  Task<Result<InvoiceDto>> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);
  Task<Result<IReadOnlyCollection<InvoiceDto>>> GetTenantInvoicesAsync(Guid tenantId, CancellationToken cancellationToken = default);
  Task<Result<IReadOnlyCollection<PaymentAttemptDto>>> GetInvoiceAttemptsAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}

public sealed class InvoicesQueryHandler(ISubscriptionInvoiceQueries queries)
{
  public Task<Result<IReadOnlyCollection<InvoiceDto>>> HandleAsync(GetInvoicesQuery query, CancellationToken cancellationToken) => queries.GetInvoicesAsync(cancellationToken);
  public Task<Result<InvoiceDto>> HandleAsync(GetInvoiceByIdQuery query, CancellationToken cancellationToken) => queries.GetInvoiceByIdAsync(query.InvoiceId, cancellationToken);
  public Task<Result<IReadOnlyCollection<InvoiceDto>>> HandleAsync(GetTenantInvoicesQuery query, CancellationToken cancellationToken) => queries.GetTenantInvoicesAsync(query.TenantId, cancellationToken);
  public Task<Result<IReadOnlyCollection<PaymentAttemptDto>>> HandleAsync(GetInvoiceAttemptsQuery query, CancellationToken cancellationToken) => queries.GetInvoiceAttemptsAsync(query.InvoiceId, cancellationToken);
}
