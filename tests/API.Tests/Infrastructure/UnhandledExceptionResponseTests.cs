using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SSAS.Host.API.Configuration;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// AN UNHANDLED EXCEPTION LEAVES THE REAL PIPELINE AS A GENERIC PROBLEM DOCUMENT (T-147).
// ==================================================================================================
//
// ---- ⚠⚠⚠ WHY: `ProblemDetailsTests` TESTS THE HANDLER AND NOTHING TESTED THAT IT IS INSTALLED.
//
// `Exception_handler_writes_status_and_correlation_id` resolves `GlobalExceptionHandler` from DI and calls
// `TryHandleAsync` on a `new DefaultHttpContext()` — **six exception kinds, no HTTP request, no middleware.**
// *A good unit test of the delegate.* ⚠ **`app.UseExceptionHandler()` is the one line that puts it in the
// pipeline, and removing it left the entire API suite green.**
//
// ---- ⚠⚠ THE GREEN MEANT "SILENTLY SUBSTITUTED", NOT "NOTHING GUARDS THIS". BOTH SIDES WERE MEASURED.
//
// With the line, `GET /` on a throwing dependency answers:
//
//     500  application/problem+json
//     {"type":"https://httpstatuses.com/500","title":"An unexpected error occurred.","status":500,
//      "correlationId":"286ea0…"}
//
// Without it, `DeveloperExceptionPageMiddlewareImpl` answers instead — also 500, also
// `application/problem+json`:
//
//     {"type":"…rfc9110…","title":"System.InvalidOperationException","status":500,
//      "detail":"probe-unhandled-exception",
//      "exception":{"details":"System.InvalidOperationException: … at …\\UnhandledExceptionProbe.cs:line 49
//                              at …HostEndpointRouteBuilderExtensions.cs:line 15 at StaticFileMiddleware…"},
//      "headers":{"Host":["localhost"]},"path":"/","endpoint":"HTTP: GET /"}
//
// ***THE EXCEPTION TYPE, THE MESSAGE, THE FULL STACK WITH ABSOLUTE FILE PATHS, THE MIDDLEWARE CHAIN AND THE
// REQUEST HEADERS.*** **So the status code and the content type are NOT discriminating — the negative
// assertions below are the ones that separate the two responders.**
//
// ---- ⚠⚠⚠ WHAT THIS PROVES, AND THE BOUND I WILL NOT OVERSTATE.
//
// **This runs in Development, where the developer exception page IS registered. So it proves OUR handler WINS
// WHEN IT IS PRESENT, against a live and loud competitor.** ***IT SAYS NOTHING ABOUT PRODUCTION, WHICH IS NOT
// MEASURED HERE:*** the substitute is not registered there, so removing the line would produce some third
// response that this file has never seen. *The disclosure above is a Development observation and is not
// evidence that a stack trace reaches a customer.*
[Collection(HostIntegrationTestGroup.Name)]
public sealed class UnhandledExceptionResponseTests
{
  [Fact]
  public async Task An_unhandled_exception_is_returned_as_a_generic_problem_document()
  {
    using var factory = new ThrowingDependencyFactory();
    using var client = factory.CreateClient();

    var response = await client.GetAsync(new Uri("/", UriKind.Relative));
    var body = await response.Content.ReadAsStringAsync();
    using var document = JsonDocument.Parse(body);
    var root = document.RootElement;

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

    // ---- THE POSITIVE HALF: OUR HANDLER'S SHAPE.
    Assert.Equal("An unexpected error occurred.", root.GetProperty("title").GetString());
    Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));

    // ---- ⚠⚠⚠ THE NEGATIVE HALF, WHICH IS THE PART THAT DISCRIMINATES.
    //
    // Status and content type are identical for both responders, so asserting them alone would pass with the
    // middleware removed. **Each property below is one the substitute emits and ours never does**, and
    // `Thrown` is the exception's own message: the strongest single check is that the caller is not told it.
    Assert.False(root.TryGetProperty("exception", out _), "the response carries an `exception` object — a stack trace is being returned to the caller.");
    Assert.False(root.TryGetProperty("headers", out _), "the response echoes the request `headers`.");
    Assert.False(root.TryGetProperty("endpoint", out _), "the response names the matched `endpoint`.");
    Assert.DoesNotContain(Thrown, body, StringComparison.Ordinal);
    Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
  }

  private const string Thrown = "unhandled-exception-response-test";

  // The exception is provoked from TEST SERVICES ONLY: `GET /` takes `IOptions<ApplicationOptions>`, and this
  // replacement throws when the endpoint reads `.Value`. ⚠ **No product code is altered, and the throw happens
  // INSIDE the endpoint — downstream of every middleware, which is exactly where a real unhandled exception
  // originates.**
  private sealed class ThrowingDependencyFactory : WebApplicationFactory<global::Program>
  {
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.UseEnvironment("Development");
      builder.ConfigureTestServices(services =>
      {
        services.RemoveAll<IOptions<ApplicationOptions>>();
        services.AddSingleton<IOptions<ApplicationOptions>>(new ThrowingOptions());
      });
    }
  }

  private sealed class ThrowingOptions : IOptions<ApplicationOptions>
  {
    public ApplicationOptions Value => throw new InvalidOperationException(Thrown);
  }
}
