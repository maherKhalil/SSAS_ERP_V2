using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using SSAS.BuildingBlocks.Api.Transport;
using SSAS.Platform.API.Transport;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.Subscriptions.Invoices;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.API.Subscriptions;

public static class InvoicesEndpointRouteBuilderExtensions
{
  public static IEndpointRouteBuilder MapPlatformInvoicesEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    var group = endpoints.MapGroup("/api/platform/invoices").WithTags("Platform Invoices");

    group.MapGet("", GetInvoicesAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewInvoices)
      .WithName("PlatformInvoicesGet");

    group.MapGet("/{invoiceId:guid}", GetInvoiceByIdAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewInvoices)
      .WithName("PlatformInvoiceByIdGet");

    group.MapPost("", CreateInvoiceAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerInvoices)
      .WithName("PlatformInvoicesCreate");

    group.MapPut("/{invoiceId:guid}", UpdateInvoiceAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerInvoices)
      .WithName("PlatformInvoicesUpdate");

    group.MapPost("/{invoiceId:guid}/issue", IssueInvoiceAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerInvoices)
      .WithName("PlatformInvoicesIssue");

    group.MapPost("/{invoiceId:guid}/void", VoidInvoiceAsync)
      .RequirePlatformPermission(PlatformPermissionNames.AdministerInvoices)
      .WithName("PlatformInvoicesVoid");

    group.MapGet("/{invoiceId:guid}/attempts", GetInvoiceAttemptsAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewInvoices)
      .WithName("PlatformInvoiceAttemptsGet");

    var tenantGroup = endpoints.MapGroup("/api/platform/tenants/{tenantId:guid}/invoices").WithTags("Platform Tenant Invoices");
    
    tenantGroup.MapGet("", GetTenantInvoicesAsync)
      .RequirePlatformPermission(PlatformPermissionNames.ViewInvoices)
      .WithName("PlatformTenantInvoicesGet");

    return endpoints;
  }

  private static async Task<IResult> GetInvoicesAsync(
    HttpContext context,
    InvoicesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetInvoicesQuery(), cancellationToken);
    return result.IsFailure ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error)) : Results.Ok(result.Value);
  }

  private static async Task<IResult> GetInvoiceByIdAsync(
    HttpContext context,
    Guid invoiceId,
    InvoicesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetInvoiceByIdQuery(invoiceId), cancellationToken);
    return result.IsFailure ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error)) : Results.Ok(result.Value);
  }

  private static async Task<IResult> GetTenantInvoicesAsync(
    HttpContext context,
    Guid tenantId,
    InvoicesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetTenantInvoicesQuery(tenantId), cancellationToken);
    return result.IsFailure ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error)) : Results.Ok(result.Value);
  }

  private static async Task<IResult> CreateInvoiceAsync(
    HttpContext context,
    InvoicesCommandHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<CreateInvoiceRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["tenantId"] = [JsonValueKind.String],
        ["currencyCode"] = [JsonValueKind.String],
        ["issuedUtc"] = [JsonValueKind.String]
      }, cancellationToken);
      
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    var lines = new List<CreateInvoiceLineCommand>();
    if (request.Lines != null)
    {
      foreach (var line in request.Lines)
      {
        lines.Add(new CreateInvoiceLineCommand(line.TenantSubscriptionId, line.Amount, line.Description ?? ""));
      }
    }

    var result = await handler.HandleCreateAsync(new CreateInvoiceCommand(request.TenantId, request.CurrencyCode ?? "", request.IssuedUtc, lines), cancellationToken);
    return result.IsFailure ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error)) : Results.Created($"/api/platform/invoices/{result.Value}", new { id = result.Value });
  }

  private static async Task<IResult> UpdateInvoiceAsync(
    HttpContext context,
    Guid invoiceId,
    InvoicesCommandHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<UpdateInvoiceRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["currencyCode"] = [JsonValueKind.String],
        ["issuedUtc"] = [JsonValueKind.String]
      }, cancellationToken);
      
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    var lines = new List<CreateInvoiceLineCommand>();
    if (request.Lines != null)
    {
      foreach (var line in request.Lines)
      {
        lines.Add(new CreateInvoiceLineCommand(line.TenantSubscriptionId, line.Amount, line.Description ?? ""));
      }
    }

    var result = await handler.HandleUpdateDraftAsync(new UpdateInvoiceDraftCommand(invoiceId, request.CurrencyCode ?? "", request.IssuedUtc, lines), cancellationToken);
    return result.IsFailure ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error)) : Results.NoContent();
  }

  private static async Task<IResult> IssueInvoiceAsync(
    HttpContext context,
    Guid invoiceId,
    InvoicesCommandHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var request = await StrictRequestReader.ReadStrictJsonAsync<IssueInvoiceRequest>(context,
      new Dictionary<string, JsonValueKind[]>
      {
        ["invoiceNumber"] = [JsonValueKind.String]
      }, cancellationToken);
      
    if (request is null) return ProblemResults.Problem(context, ProblemResults.RequestInvalid);

    var result = await handler.HandleIssueAsync(new IssueInvoiceCommand(invoiceId, request.InvoiceNumber ?? ""), cancellationToken);
    if (result.IsFailure)
    {
      return ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error));
    }
    return Results.NoContent();
  }

  private static async Task<IResult> VoidInvoiceAsync(
    HttpContext context,
    Guid invoiceId,
    InvoicesCommandHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleVoidAsync(new VoidInvoiceCommand(invoiceId), cancellationToken);
    return result.IsFailure ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error)) : Results.NoContent();
  }

  private static async Task<IResult> GetInvoiceAttemptsAsync(
    HttpContext context,
    Guid invoiceId,
    InvoicesQueryHandler handler,
    CancellationToken cancellationToken)
  {
    ApiResponseSecurity.Apply(context);
    var result = await handler.HandleAsync(new GetInvoiceAttemptsQuery(invoiceId), cancellationToken);
    return result.IsFailure ? ProblemResults.Problem(context, SubscriptionApiErrorMapper.Map(result.Error)) : Results.Ok(result.Value);
  }
}

public class CreateInvoiceRequest
{
  [JsonPropertyName("tenantId")]
  public Guid TenantId { get; set; }

  [JsonPropertyName("currencyCode")]
  public string? CurrencyCode { get; set; }

  [JsonPropertyName("issuedUtc")]
  public DateTimeOffset IssuedUtc { get; set; }

  [JsonPropertyName("lines")]
  public List<InvoiceLineRequest>? Lines { get; set; }
}

public class UpdateInvoiceRequest
{
  [JsonPropertyName("currencyCode")]
  public string? CurrencyCode { get; set; }

  [JsonPropertyName("issuedUtc")]
  public DateTimeOffset IssuedUtc { get; set; }

  [JsonPropertyName("lines")]
  public List<InvoiceLineRequest>? Lines { get; set; }
}

public class IssueInvoiceRequest
{
  [JsonPropertyName("invoiceNumber")]
  public string? InvoiceNumber { get; set; }
}

public class InvoiceLineRequest
{
  [JsonPropertyName("tenantSubscriptionId")]
  public Guid TenantSubscriptionId { get; set; }

  [JsonPropertyName("amount")]
  public decimal Amount { get; set; }

  [JsonPropertyName("description")]
  public string? Description { get; set; }
}
