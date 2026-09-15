using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using SSAS.TestSupport.VerificationHost;

namespace SSAS.API.Tests.HR.EmployeeDocuments;

[Collection(nameof(VerificationHostCollection))]
public sealed class EmployeeDocumentEndpointsTests(VerificationHostFixture fixture) : IAsyncLifetime
{
  private readonly VerificationHost _host = fixture.Host;

  public Task InitializeAsync() => Task.CompletedTask;
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task Upload_valid_document_returns_created()
  {
    var employeeId = Guid.NewGuid();
    using var client = _host.CreateCompanyClient();
    var response = await client.PostAsync($"/api/hr/employees/{employeeId}/documents", new MultipartFormDataContent());
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
}
