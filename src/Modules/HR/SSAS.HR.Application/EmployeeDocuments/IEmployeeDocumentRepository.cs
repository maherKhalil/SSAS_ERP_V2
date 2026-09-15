using SSAS.HR.Domain.EmployeeDocuments;

namespace SSAS.HR.Application.EmployeeDocuments;

public interface IEmployeeDocumentRepository
{
  Task<EmployeeDocument?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken = default);
  
  Task<List<EmployeeDocument>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
  
  Task AddAsync(EmployeeDocument document, CancellationToken cancellationToken = default);
  
  Task AddContentAsync(EmployeeDocumentContent content, CancellationToken cancellationToken = default);
  
  Task<EmployeeDocumentContent?> GetContentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
