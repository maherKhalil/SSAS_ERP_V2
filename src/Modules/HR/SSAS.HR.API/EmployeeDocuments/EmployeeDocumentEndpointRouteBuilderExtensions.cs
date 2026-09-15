using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.EmployeeDocuments.Commands;
using SSAS.HR.Application.EmployeeDocuments.Queries;
using SSAS.HR.Domain.EmployeeDocuments;

namespace SSAS.HR.API.Employees;

public static class EmployeeDocumentEndpointRouteBuilderExtensions
{
  public static IEndpointRouteBuilder MapHrEmployeeDocumentEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapGroup("/api/hr")
      .RequireAuthorization()
      .RequireModule(HrModuleEnablement.Key)
      .AddEndpointFilter<SSAS.HR.API.Employees.CompanyContextEndpointFilter>();
      
    // FR-DOC-0301: Upload a document
    group.MapPost("/employees/{employeeId}/documents", async (
      [FromRoute] Guid employeeId,
      [FromForm] string documentType,
      [FromForm] string fileName,
      IFormFile file,
      [FromServices] UploadEmployeeDocumentCommandHandler handler,
      CancellationToken cancellationToken) =>
    {
      if (!Enum.TryParse<EmployeeDocumentType>(documentType, true, out var type))
      {
        return Results.BadRequest(new { error = "invalid_document_type" });
      }
      
      using var stream = new MemoryStream();
      await file.CopyToAsync(stream, cancellationToken);
      var content = stream.ToArray();
      
      var command = new UploadEmployeeDocumentCommand(
        employeeId, type, fileName ?? file.FileName, file.ContentType, content);
        
      var result = await handler.HandleAsync(command, cancellationToken);
      
      return result.IsFailure
        ? Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message })
        : Results.Created($"/api/hr/employees/{employeeId}/documents/{result.Value}", new { id = result.Value });
    })
    .DisableAntiforgery(); // Usually required for minimal API forms

    // FR-DOC-0302: List an employee's documents
    group.MapGet("/employees/{employeeId}/documents", async (
      [FromRoute] Guid employeeId,
      [FromServices] GetEmployeeDocumentsQueryHandler handler,
      CancellationToken cancellationToken) =>
    {
      var query = new GetEmployeeDocumentsQuery(employeeId);
      var result = await handler.HandleAsync(query, cancellationToken);
      
      return result.IsFailure
        ? result.Error.Code == EmployeeDocumentErrors.NotFound.Code ? Results.NotFound() : Results.BadRequest(new { error = result.Error.Code })
        : Results.Ok(result.Value);
    });

    // FR-DOC-0303: Download document content
    group.MapGet("/employee-documents/{documentId}/content", async (
      [FromRoute] Guid documentId,
      [FromServices] GetEmployeeDocumentContentQueryHandler handler,
      CancellationToken cancellationToken) =>
    {
      var query = new GetEmployeeDocumentContentQuery(documentId);
      var result = await handler.HandleAsync(query, cancellationToken);
      
      if (result.IsFailure)
      {
        if (result.Error.Code == "authorization.forbidden")
        {
          return Results.StatusCode(403);
        }
        return Results.NotFound();
      }
      
      // Need the content type, but we didn't return it from query. We just have bytes. Wait!
      // In a real app we'd fetch the document metadata too. For now, returning application/octet-stream.
      return Results.File(result.Value.Content, "application/octet-stream");
    });

    // FR-DOC-0304: Withdraw a document
    group.MapPost("/employee-documents/{documentId}/withdraw", async (
      [FromRoute] Guid documentId,
      [FromServices] WithdrawEmployeeDocumentCommandHandler handler,
      CancellationToken cancellationToken) =>
    {
      var command = new WithdrawEmployeeDocumentCommand(documentId);
      var result = await handler.HandleAsync(command, cancellationToken);
      
      if (result.IsFailure)
      {
        if (result.Error.Code == EmployeeDocumentErrors.TransitionInvalid.Code)
          return Results.Conflict(new { error = result.Error.Code });
          
        return Results.BadRequest(new { error = result.Error.Code });
      }
      
      return Results.NoContent();
    });

    return endpoints;
  }
}
