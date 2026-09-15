using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.EmployeeDocuments;

public static class EmployeeDocumentErrors
{
  public static readonly Error NotFound = new(
    "employee_document.not_found", "The employee document was not found or is outside the authorized scope.");
    
  public static readonly Error InvalidActor = new(
    "employee_document.invalid_actor", "The actor is invalid or missing.");
    
  public static readonly Error TransitionInvalid = new(
    "employee_document.transition_invalid", "The document is already in the requested state or the transition is not allowed.");
    
  public static readonly Error TooLarge = new(
    "employee_document.too_large", "The document exceeds the maximum allowed size (10MB).");
    
  public static readonly Error ContentTypeRejected = new(
    "employee_document.content_type_rejected", "The document content type is not allowed or magic bytes disagree.");
}
