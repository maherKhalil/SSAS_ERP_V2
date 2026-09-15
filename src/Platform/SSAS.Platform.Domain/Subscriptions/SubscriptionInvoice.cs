using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;

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

  public string CurrencyCode { get; private set; }

  public DateTimeOffset IssuedUtc { get; private set; }

  public SubscriptionInvoiceState State { get; private set; } = SubscriptionInvoiceState.Draft;

  public IReadOnlyCollection<SubscriptionInvoiceLine> Lines => lines.AsReadOnly();

  public static Result<SubscriptionInvoice> CreateDraft(Guid tenantId, string currencyCode, DateTimeOffset issuedUtc)
  {
    return Result.Success(new SubscriptionInvoice(Guid.NewGuid(), tenantId, currencyCode, issuedUtc));
  }

  public Result UpdateDraft(string currencyCode, DateTimeOffset issuedUtc)
  {
    if (State != SubscriptionInvoiceState.Draft)
    {
      return Result.Failure(new Error("Invoice.NotDraft", "Only draft invoices can be updated."));
    }

    CurrencyCode = currencyCode;
    IssuedUtc = issuedUtc;
    return Result.Success();
  }

  public Result Issue(string invoiceNumber)
  {
    if (State != SubscriptionInvoiceState.Draft)
    {
      return Result.Failure(new Error("Invoice.NotDraft", "Invoice is already issued or voided."));
    }

    InvoiceNumber = invoiceNumber;
    State = SubscriptionInvoiceState.Issued;
    return Result.Success();
  }

  public Result Void()
  {
    if (State == SubscriptionInvoiceState.Voided)
    {
      return Result.Failure(new Error("Invoice.AlreadyVoided", "Invoice is already voided."));
    }

    State = SubscriptionInvoiceState.Voided;
    return Result.Success();
  }

  public void AddLine(SubscriptionInvoiceLine line)
  {
    lines.Add(line);
  }
  
  public void ClearLines()
  {
    lines.Clear();
  }
}
