using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class IssuetoDepartment : Entity<string>, ITenantOwnedEntity
{

    public IssuetoDepartment(string id) : base(id) { }
    public IssuetoDepartment() : base(Guid.NewGuid().ToString()) { }

    public string? MainStockID { get; set; }
    public string? IssueNumber { get; set; }
    public string? IssueDate { get; set; }
    public string? StockID { get; set; }
    public string? Remarks { get; set; }
    public string? Status { get; set; }
    public string? IssueRequestID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? SetEntryCode { get; set; }
    public Guid TenantId { get; set; }

}
