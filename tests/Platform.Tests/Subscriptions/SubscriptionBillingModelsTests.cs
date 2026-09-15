using SSAS.Platform.Domain.Subscriptions;

namespace SSAS.Platform.Tests.Subscriptions;

public sealed class SubscriptionBillingModelsTests
{
  [Fact]
  public void SubscriptionInvoice_Draft_Creation_Initializes_Correctly()
  {
    var tenantId = Guid.NewGuid();
    var issuedUtc = DateTimeOffset.UtcNow;
    var draft = SubscriptionInvoice.CreateDraft(tenantId, "USD", issuedUtc).Value;

    Assert.Equal(tenantId, draft.TenantId);
    Assert.Equal("USD", draft.CurrencyCode);
    Assert.Equal(issuedUtc, draft.IssuedUtc);
    Assert.Null(draft.InvoiceNumber);
    Assert.Empty(draft.Lines);
  }

  [Fact]
  public void SubscriptionInvoice_Issue_Sets_InvoiceNumber()
  {
    var draft = SubscriptionInvoice.CreateDraft(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow).Value;
    var result = draft.Issue("INV-1001");

    Assert.True(result.IsSuccess);
    Assert.Equal("INV-1001", draft.InvoiceNumber);
  }

  [Fact]
  public void SubscriptionInvoice_Issue_Fails_If_Already_Issued()
  {
    var draft = SubscriptionInvoice.CreateDraft(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow).Value;
    draft.Issue("INV-1001");
    
    var result = draft.Issue("INV-1002");

    Assert.True(result.IsFailure);
    Assert.Equal("Invoice.NotDraft", result.Error.Code);
  }

  [Fact]
  public void SubscriptionInvoiceLine_Creation_Initializes_Correctly()
  {
    var line = new SubscriptionInvoiceLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100.00m, "Seat Usage");
    
    Assert.Equal(100.00m, line.Amount);
    Assert.Equal("Seat Usage", line.Description);
  }

  [Fact]
  public void SubscriptionPaymentAttempt_Creation_Initializes_Correctly()
  {
    var attempt = new SubscriptionPaymentAttempt(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "Success", "STRIPE-123");
    
    Assert.Equal("Success", attempt.Outcome);
    Assert.Equal("STRIPE-123", attempt.ProviderReference);
  }

  [Fact]
  public void TenantSeatUsageSample_Creation_Initializes_Correctly()
  {
    var sample = new TenantSeatUsageSample(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25, DateTimeOffset.UtcNow);
    
    Assert.Equal(25, sample.ObservedSeatCount);
  }
}
