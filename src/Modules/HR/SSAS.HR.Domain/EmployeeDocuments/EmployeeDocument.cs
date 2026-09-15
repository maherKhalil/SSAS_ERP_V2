using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.EmployeeDocuments;

public sealed class EmployeeDocument
  : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  private EmployeeDocument(
    Guid documentId,
    Guid employeeId,
    EmployeeDocumentType documentType,
    string fileName,
    string normalizedFileName,
    string contentType,
    long byteCount,
    byte[] contentHash,
    string? contentLocation,
    string actor,
    DateTimeOffset occurredUtc) : base(documentId)
  {
    EmployeeId = employeeId;
    DocumentType = documentType;
    FileName = fileName;
    NormalizedFileName = normalizedFileName;
    ContentType = contentType;
    ByteCount = byteCount;
    ContentHash = contentHash;
    ContentLocation = contentLocation;
    Status = EmployeeDocumentStatus.Active;
    CreatedBy = actor;
    CreatedUtc = occurredUtc;
  }

  private EmployeeDocument() : base(Guid.Empty)
  {
    FileName = string.Empty;
    NormalizedFileName = string.Empty;
    ContentType = string.Empty;
    ContentHash = [];
  }

  public Guid TenantId { get; set; }
  public Guid CompanyId { get; set; }

  // BRULE-DOC-0607
  public Guid EmployeeId { get; private set; }

  public EmployeeDocumentType DocumentType { get; private set; }

  public string FileName { get; private set; }
  public string NormalizedFileName { get; private set; }

  public string ContentType { get; private set; }

  public long ByteCount { get; private set; }

  // BRULE-DOC-0608
  public byte[] ContentHash { get; private set; }

  public string? ContentLocation { get; private set; }

  public EmployeeDocumentStatus Status { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }
  public DateTimeOffset ModifiedUtc { get; private set; }
  public string? CreatedBy { get; private set; }
  public string? ModifiedBy { get; private set; }
  public byte[] RowVersion { get; private set; } = [];

  public static Result<EmployeeDocument> Create(
    Guid employeeId,
    EmployeeDocumentType documentType,
    string fileName,
    string normalizedFileName,
    string contentType,
    long byteCount,
    byte[] contentHash,
    string? contentLocation,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (string.IsNullOrWhiteSpace(actor))
    {
      return Result.Failure<EmployeeDocument>(EmployeeDocumentErrors.InvalidActor);
    }
    
    // DEC-DOC-0011: Size ceiling 10 MB per document
    if (byteCount > 10 * 1024 * 1024)
    {
      return Result.Failure<EmployeeDocument>(EmployeeDocumentErrors.TooLarge);
    }

    var document = new EmployeeDocument(
      Guid.NewGuid(), employeeId, documentType, fileName, normalizedFileName, contentType, byteCount, contentHash, contentLocation, actor, occurredUtc);
      
    // Optionally RaiseDomainEvent here
    document.RaiseDomainEvent(new Events.EmployeeDocumentUploaded(
      eventId, occurredUtc, document.Id, document.TenantId, document.CompanyId, document.EmployeeId, documentType, byteCount));

    return Result.Success(document);
  }

  public Result Withdraw(string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status == EmployeeDocumentStatus.Withdrawn)
    {
      return Result.Failure(EmployeeDocumentErrors.TransitionInvalid);
    }

    if (string.IsNullOrWhiteSpace(actor))
    {
      return Result.Failure(EmployeeDocumentErrors.InvalidActor);
    }

    Status = EmployeeDocumentStatus.Withdrawn;
    
    RaiseDomainEvent(new Events.EmployeeDocumentWithdrawn(
      eventId, occurredUtc, Id, TenantId, CompanyId, EmployeeId, actor));

    return Result.Success();
  }
  
  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }
}
