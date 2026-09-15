using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.EmployeeDocuments;
using SSAS.HR.Domain.Employees;
using System.Security.Cryptography;

namespace SSAS.HR.Application.EmployeeDocuments.Commands;

public sealed record UploadEmployeeDocumentCommand(
  Guid EmployeeId,
  EmployeeDocumentType DocumentType,
  string FileName,
  string ContentType,
  byte[] Content);

public sealed class UploadEmployeeDocumentCommandHandler(
  IEmployeeDocumentRepository documents,
  IEmployeeRepository employees,
  IEmployeeDocumentScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result<Guid>> HandleAsync(
    UploadEmployeeDocumentCommand command, CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<Guid>(EmployeeDocumentErrors.InvalidActor);
    }
    
    // AC-DOC-0017: Magic bytes verification
    if (!VerifyMagicBytes(command.Content, command.ContentType))
    {
      return Result.Failure<Guid>(EmployeeDocumentErrors.ContentTypeRejected);
    }

    var employee = await employees.GetByIdAsync(command.EmployeeId, cancellationToken);
    if (employee is null || employee.TenantId != tenantId)
    {
      return Result.Failure<Guid>(EmployeeDocumentErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      HrPermissionNames.UploadEmployeeDocuments, employee.CompanyId, employee.BranchId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure<Guid>(EmployeeDocumentErrors.NotFound);
    }

    var contentHash = SHA256.HashData(command.Content);

    // Normalize file name as just the string for this example
    var document = EmployeeDocument.Create(
      command.EmployeeId,
      command.DocumentType,
      command.FileName,
      command.FileName.ToUpperInvariant(),
      command.ContentType,
      command.Content.Length,
      contentHash,
      null, // ContentLocation is null for in-database
      currentUser.UserId,
      Guid.NewGuid(),
      clock.UtcNow);

    if (document.IsFailure)
    {
      return Result.Failure<Guid>(document.Error);
    }

    document.Value.TenantId = tenantId;
    document.Value.CompanyId = employee.CompanyId;

    var contentEntity = EmployeeDocumentContent.Create(document.Value.Id, tenantId, command.Content);

    await documents.AddAsync(document.Value, cancellationToken);
    await documents.AddContentAsync(contentEntity, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure
      ? Result.Failure<Guid>(saved.Error)
      : Result.Success(document.Value.Id);
  }
  
  private static bool VerifyMagicBytes(byte[] content, string contentType)
  {
    if (content.Length < 4) return false;

    // VERY basic magic bytes check for the allowlisted types
    if (contentType == "application/pdf" && content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46)
      return true;
      
    if (contentType == "image/png" && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
      return true;
      
    if ((contentType == "image/jpeg" || contentType == "image/jpg") && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
      return true;
      
    if (contentType == "text/plain")
      return true;

    return false;
  }
}

public sealed record WithdrawEmployeeDocumentCommand(
  Guid DocumentId);

public sealed class WithdrawEmployeeDocumentCommandHandler(
  IEmployeeDocumentRepository documents,
  IEmployeeRepository employees,
  IEmployeeDocumentScopeResolver scope,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(
    WithdrawEmployeeDocumentCommand command, CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId || string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure(EmployeeDocumentErrors.InvalidActor);
    }

    var document = await documents.GetByIdAsync(command.DocumentId, cancellationToken);
    if (document is null || document.TenantId != tenantId)
    {
      return Result.Failure(EmployeeDocumentErrors.NotFound);
    }
    
    var employee = await employees.GetByIdAsync(document.EmployeeId, cancellationToken);
    if (employee is null || employee.TenantId != tenantId)
    {
      return Result.Failure(EmployeeDocumentErrors.NotFound);
    }

    var authorized = await scope.AuthorizeAsync(
      HrPermissionNames.WithdrawEmployeeDocuments, document.CompanyId, employee.BranchId, cancellationToken);
    if (authorized.IsFailure)
    {
      return Result.Failure(EmployeeDocumentErrors.NotFound);
    }

    var result = document.Withdraw(currentUser.UserId, Guid.NewGuid(), clock.UtcNow);
    if (result.IsFailure)
    {
      return result;
    }

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    return saved;
  }
}
