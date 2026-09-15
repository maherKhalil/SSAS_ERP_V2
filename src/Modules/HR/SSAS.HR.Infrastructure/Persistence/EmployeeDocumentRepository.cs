using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Application.EmployeeDocuments;
using SSAS.HR.Domain.EmployeeDocuments;

namespace SSAS.HR.Infrastructure.Persistence;

internal sealed class EmployeeDocumentRepository(ITenantDbContextAccessor contextAccessor) : IEmployeeDocumentRepository
{
  public async Task<EmployeeDocument?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    return await context.Set<EmployeeDocument>()
      .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
  }

  public async Task<List<EmployeeDocument>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    return await context.Set<EmployeeDocument>()
      .AsNoTracking()
      .Where(d => d.EmployeeId == employeeId)
      .ToListAsync(cancellationToken);
  }

  public async Task AddAsync(EmployeeDocument document, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    context.Set<EmployeeDocument>().Add(document);
  }
  
  public async Task AddContentAsync(EmployeeDocumentContent content, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    context.Set<EmployeeDocumentContent>().Add(content);
  }

  public async Task<EmployeeDocumentContent?> GetContentAsync(Guid documentId, CancellationToken cancellationToken = default)
  {
    var context = await contextAccessor.GetRequiredAsync(cancellationToken);
    return await context.Set<EmployeeDocumentContent>()
      .AsNoTracking()
      .FirstOrDefaultAsync(c => c.DocumentId == documentId, cancellationToken);
  }
}
