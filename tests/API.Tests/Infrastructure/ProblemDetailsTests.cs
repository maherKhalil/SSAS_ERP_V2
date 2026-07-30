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
