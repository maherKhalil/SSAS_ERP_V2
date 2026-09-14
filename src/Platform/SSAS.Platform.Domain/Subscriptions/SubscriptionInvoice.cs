using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Subscriptions;

public sealed class SubscriptionInvoice : AggregateRoot<Guid>
{
  private readonly List<SubscriptionInvoiceLine> lines = new();

  private SubscriptionInvoice(Guid id, Guid tenantId, string currencyCode, DateTimeOffset issuedUtc)
    : base(id)
  {
    TenantId = tenantId;
    CurrencyCode = currencyCode;
    IssuedUtc = issuedUtc;
  }

  public string? InvoiceNumber { get; private set; }

  public Guid TenantId { get; }

  public string CurrencyCode { get; }

  public DateTimeOffset IssuedUtc { get; }

  public IReadOnlyCollection<SubscriptionInvoiceLine> Lines => lines.AsReadOnly();

  public static Result<SubscriptionInvoice> CreateDraft(Guid tenantId, string currencyCode, DateTimeOffset issuedUtc)
  {
    return Result.Success(new SubscriptionInvoice(Guid.NewGuid(), tenantId, currencyCode, issuedUtc));
  }

  public Result Issue(string invoiceNumber)
  {
    if (InvoiceNumber is not null)
    {
      return Result.Failure(new Error("Invoice.AlreadyIssued", "Invoice is already issued."));
    }

    InvoiceNumber = invoiceNumber;
    return Result.Success();
  }

  public void AddLine(SubscriptionInvoiceLine line)
  {
    lines.Add(line);
  }
}
