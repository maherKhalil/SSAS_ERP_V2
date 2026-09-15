using System;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Subscriptions.TenantSubscriptions;

public class TenantSubscriptionDto
{
  public Guid TenantSubscriptionId { get; set; }
  public Guid TenantId { get; set; }
  public Guid SubscriptionPlanId { get; set; }
  public DateTimeOffset EffectiveFromUtc { get; set; }
  public SubscriptionTermDto Term { get; set; } = null!;
  public string BillingCurrencyCode { get; set; } = string.Empty;
  public DateTimeOffset CreatedUtc { get; set; }
  public string ChangedBy { get; set; } = string.Empty;
  public string? ChangeReasonCode { get; set; }
  public string? ChangeReasonText { get; set; }
}

public class SubscriptionTermDto
{
  public SubscriptionTermKind Kind { get; set; }
  public DateTimeOffset StartUtc { get; set; }
  public DateTimeOffset? EndUtc { get; set; }
}
