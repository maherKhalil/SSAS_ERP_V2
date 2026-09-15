using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class IssueRequest : Entity<string>, ITenantOwnedEntity
{

    public IssueRequest(string id) : base(id) { }
    public IssueRequest() : base(Guid.NewGuid().ToString()) { }

    public string? RequestNumber { get; set; }
    public string? RequestDate { get; set; }
    public string? MainSubstoreID { get; set; }
    public string? SubSubstoreID { get; set; }
    public string? Status { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreationDate { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ModificationDate { get; set; }
    public string? CompanyID { get; set; }
    public string? Priority { get; set; }
    public string? EmpRequestID { get; set; }
    public Guid TenantId { get; set; }

}
