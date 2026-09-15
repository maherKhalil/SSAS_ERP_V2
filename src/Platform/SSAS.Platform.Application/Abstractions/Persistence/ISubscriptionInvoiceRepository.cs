using System;
using System.Threading;
using System.Threading.Tasks;
using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Application.Abstractions.Persistence;

public interface ISubscriptionInvoiceRepository
{
  Task<SubscriptionInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
  void Add(SubscriptionInvoice invoice);
}

public interface ISubscriptionPaymentAttemptRepository
{
  void Add(SubscriptionPaymentAttempt attempt);
}
