using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class IssuetoDepartmentReturn : Entity<string>, ITenantOwnedEntity
{

    public IssuetoDepartmentReturn(string id) : base(id) { }
    public IssuetoDepartmentReturn() : base(Guid.NewGuid().ToString()) { }

    public string? StockID { get; set; }
    public string? IssueReturnNumber { get; set; }
    public string? IssueReturnDate { get; set; }
    public string? IssueNumber { get; set; }
    public string? IssueDate { get; set; }
    public string? Remarks { get; set; }
    public string? Status { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? SetEntryCode { get; set; }
    public Guid TenantId { get; set; }

}
