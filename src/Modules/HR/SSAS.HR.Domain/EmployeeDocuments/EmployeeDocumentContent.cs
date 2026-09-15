using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.EmployeeDocuments;

public sealed class EmployeeDocumentContent
{
  private EmployeeDocumentContent(Guid documentId, Guid tenantId, byte[] content)
  {
    DocumentId = documentId;
    TenantId = tenantId;
    Content = content;
  }

  private EmployeeDocumentContent() { }

  public Guid DocumentId { get; private set; }
  
  public Guid TenantId { get; set; }

  public byte[] Content { get; private set; } = [];

  public static EmployeeDocumentContent Create(Guid documentId, Guid tenantId, byte[] content)
  {
    return new EmployeeDocumentContent(documentId, tenantId, content);
  }
}
