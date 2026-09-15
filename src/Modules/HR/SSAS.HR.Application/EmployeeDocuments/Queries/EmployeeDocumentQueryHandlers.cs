using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.EmployeeDocuments;
using System.Text.Json.Serialization;

namespace SSAS.HR.Application.EmployeeDocuments.Queries;

public sealed record EmployeeDocumentDto(
  Guid DocumentId,
  Guid EmployeeId,
  string DocumentType,
  string FileName,
  string ContentType,
  long ByteCount,
  byte[] ContentHash,
  string Status,
  DateTimeOffset UploadedUtc,
  string UploadedBy,
  byte[] RowVersion);

public sealed record GetEmployeeDocumentsQuery(
  Guid EmployeeId);

public sealed class GetEmployeeDocumentsQueryHandler(
  IEmployeeDocumentRepository documents,
  IEmployeeRepository employees,
  IEmployeeDocumentScopeResolver scope,
  ICurrentTenant currentTenant)
{
  public async Task<Result<List<EmployeeDocumentDto>>> HandleAsync(
    GetEmployeeDocumentsQuery query, CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId)
    {
      return Result.Failure<List<EmployeeDocumentDto>>(EmployeeDocumentErrors.InvalidActor);
    }
    
    var employee = await employees.GetByIdAsync(query.EmployeeId, cancellationToken);
    if (employee is null || employee.TenantId != tenantId)
    {
      return Result.Failure<List<EmployeeDocumentDto>>(EmployeeDocumentErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      HrPermissionNames.ViewEmployeeDocuments, employee.CompanyId, employee.BranchId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<List<EmployeeDocumentDto>>(EmployeeDocumentErrors.NotFound);
    }
    
    var employeeDocs = await documents.GetByEmployeeIdAsync(query.EmployeeId, cancellationToken);
    
    var docs = employeeDocs
      .Select(d => new EmployeeDocumentDto(
        d.Id,
        d.EmployeeId,
        d.DocumentType.ToString(),
        d.FileName,
        d.ContentType,
        d.ByteCount,
        d.ContentHash,
        d.Status.ToString(),
        d.CreatedUtc,
        d.CreatedBy ?? "",
        d.RowVersion))
      .ToList();

    return Result.Success(docs);
  }
}

public sealed record GetEmployeeDocumentContentQuery(
  Guid DocumentId);

public sealed class GetEmployeeDocumentContentQueryHandler(
  IEmployeeDocumentRepository documents,
  IEmployeeRepository employees,
  IEmployeeDocumentScopeResolver scope,
  ICurrentTenant currentTenant)
{
  public async Task<Result<EmployeeDocumentContent>> HandleAsync(
    GetEmployeeDocumentContentQuery query, CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId)
    {
      return Result.Failure<EmployeeDocumentContent>(EmployeeDocumentErrors.InvalidActor);
    }

    var document = await documents.GetByIdAsync(query.DocumentId, cancellationToken);
    if (document is null || document.TenantId != tenantId)
    {
      return Result.Failure<EmployeeDocumentContent>(EmployeeDocumentErrors.NotFound);
    }

    var employee = await employees.GetByIdAsync(document.EmployeeId, cancellationToken);
    if (employee is null || employee.TenantId != tenantId)
    {
      return Result.Failure<EmployeeDocumentContent>(EmployeeDocumentErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      HrPermissionNames.DownloadEmployeeDocuments, document.CompanyId, employee.BranchId, cancellationToken);
    if (authorized.IsFailure)
    {
      // 403 authorization.forbidden
      return Result.Failure<EmployeeDocumentContent>(new Error("authorization.forbidden", "Not authorized to download content"));
    }

    var content = await documents.GetContentAsync(query.DocumentId, cancellationToken);
    if (content is null)
    {
      return Result.Failure<EmployeeDocumentContent>(EmployeeDocumentErrors.NotFound);
    }

    return Result.Success(content);
  }
}
