using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Domain;
using SSAS.Host.API.Diagnostics;
using SSAS.Host.API.Errors;

namespace SSAS.API.Tests.Infrastructure;

[Collection(HostIntegrationTestGroup.Name)]
public sealed class ProblemDetailsTests(HostWebApplicationFactory factory)
{
  [Theory]
  [InlineData("validation", StatusCodes.Status400BadRequest)]
  [InlineData("unauthorized", StatusCodes.Status401Unauthorized)]
  [InlineData("forbidden", StatusCodes.Status403Forbidden)]
  [InlineData("not-found", StatusCodes.Status404NotFound)]
  [InlineData("business-rule", StatusCodes.Status409Conflict)]
  [InlineData("unexpected", StatusCodes.Status500InternalServerError)]
  public async Task Exception_handler_writes_status_and_correlation_id(string exceptionKind, int expectedStatusCode)
  {
    using var scope = factory.Services.CreateScope();
    var context = new DefaultHttpContext
    {
      RequestServices = scope.ServiceProvider,
      Response = { Body = new MemoryStream() }
    };
    context.Response.Headers[CorrelationIdMiddleware.HeaderName] = "problem-correlation-123";
    var handler = scope.ServiceProvider.GetServices<IExceptionHandler>().OfType<GlobalExceptionHandler>().Single();

    var handled = await handler.TryHandleAsync(context, CreateException(exceptionKind), CancellationToken.None);

    context.Response.Body.Position = 0;
    using var document = await JsonDocument.ParseAsync(context.Response.Body);
    Assert.True(handled);
    Assert.Equal(expectedStatusCode, context.Response.StatusCode);
    Assert.Equal("problem-correlation-123", document.RootElement.GetProperty("correlationId").GetString());
  }

  // ==================================================================================================
  // ⚠ AC-EMP-0044 — A WRITE-BOUNDARY REFUSAL DISCLOSES NO TOPOLOGY (B18 pass 11).
  // ==================================================================================================
  //
  // The criterion: *"Refusals originating in the branch or company write boundaries are surfaced as generic
  // scope denials. No response discloses table names, database…"*
  //
  // ⚠ THE BOUNDARY THROWS WITH A MESSAGE THAT NAMES THE OWNERSHIP DIMENSION -- `TenantDbContext` raises
  // *"Branch ownership cannot be changed after an entity is created."* and `PersistenceDbContext` the tenant
  // equivalent. **Those strings are correct where they are: they are read by DEVELOPERS in test failures and
  // logs.** The criterion is about what a CALLER sees.
  //
  // ---- WHY NOTHING REACHED THIS BEFORE.
  //
  // `Exception_handler_writes_status_and_correlation_id` above asserts the STATUS and the CORRELATION ID for
  // six exception kinds. ⚠ **It never asserts what the body does NOT contain**, and absence is the whole
  // content of this criterion. The handler is generic by construction -- an `InvalidOperationException`
  // falls to the `_ =>` arm and `ProblemDetailsWriter` is handed the mapped TITLE, never the exception's
  // message -- so the criterion is SATISFIED. **It was satisfied and unasserted, which is the shape this
  // sweep keeps finding.**
  [Theory]
  [Trait("Criterion", "AC-EMP-0044")]
  [InlineData("Branch ownership cannot be changed after an entity is created.", "Branch ownership")]
  [InlineData("Tenant ownership must match the trusted tenant context.", "Tenant ownership")]
  [InlineData("Company ownership cannot be changed after an entity is created.", "Company ownership")]
  public async Task A_write_boundary_refusal_discloses_nothing_to_the_caller(string boundaryMessage, string disclosure)
  {
    using var scope = factory.Services.CreateScope();
    var context = new DefaultHttpContext
    {
      RequestServices = scope.ServiceProvider,
      Response = { Body = new MemoryStream() }
    };
    var handler = scope.ServiceProvider.GetServices<IExceptionHandler>().OfType<GlobalExceptionHandler>().Single();

    var handled = await handler.TryHandleAsync(
      context, new InvalidOperationException(boundaryMessage), CancellationToken.None);

    context.Response.Body.Position = 0;
    var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

    Assert.True(handled);

    // ⚠ THE ABSENCE, WHICH IS THE CRITERION.
    Assert.DoesNotContain(disclosure, body, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("Employees", body, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("tenant.", body, StringComparison.OrdinalIgnoreCase);

    // ⚠ AND THE PRESENCE, WITHOUT WHICH THE ABSENCE IS FREE: a handler that wrote an EMPTY body would
    // satisfy every assertion above. The caller must still receive the generic denial.
    Assert.Contains("An unexpected error occurred", body, StringComparison.Ordinal);
  }

  private static Exception CreateException(string exceptionKind)
  {
    return exceptionKind switch
    {
      "validation" => new ValidationException("Invalid request"),
      "unauthorized" => new UnauthorizedAccessException(),
      "forbidden" => new System.Security.SecurityException(),
      "not-found" => new KeyNotFoundException(),
      "business-rule" => new BusinessRuleViolationException("Business rule failed"),
      _ => new InvalidOperationException("Unexpected")
    };
  }
}
