using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Subscriptions.Invoices;

public record InvoiceDto(
  [property: JsonPropertyName("id")] Guid Id,
  [property: JsonPropertyName("invoiceNumber")] string? InvoiceNumber,
  [property: JsonPropertyName("tenantId")] Guid TenantId,
  [property: JsonPropertyName("currencyCode")] string CurrencyCode,
  [property: JsonPropertyName("issuedUtc")] DateTimeOffset IssuedUtc,
  [property: JsonPropertyName("state")] [property: JsonConverter(typeof(JsonStringEnumConverter))] SubscriptionInvoiceState State,
  [property: JsonPropertyName("lines")] IReadOnlyCollection<InvoiceLineDto> Lines
);

public record InvoiceLineDto(
  [property: JsonPropertyName("id")] Guid Id,
  [property: JsonPropertyName("tenantSubscriptionId")] Guid TenantSubscriptionId,
  [property: JsonPropertyName("amount")] decimal Amount,
  [property: JsonPropertyName("description")] string Description
);

public record PaymentAttemptDto(
  [property: JsonPropertyName("id")] Guid Id,
  [property: JsonPropertyName("subscriptionInvoiceId")] Guid SubscriptionInvoiceId,
  [property: JsonPropertyName("attemptedUtc")] DateTimeOffset AttemptedUtc,
  [property: JsonPropertyName("outcome")] string Outcome,
  [property: JsonPropertyName("providerReference")] string ProviderReference
);
