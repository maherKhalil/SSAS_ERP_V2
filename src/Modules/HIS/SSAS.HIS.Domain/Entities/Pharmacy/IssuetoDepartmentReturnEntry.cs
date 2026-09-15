using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class IssuetoDepartmentReturnEntry : Entity<string>, ITenantOwnedEntity
{

    public IssuetoDepartmentReturnEntry(string id) : base(id) { }
    public IssuetoDepartmentReturnEntry() : base(Guid.NewGuid().ToString()) { }

    public string? IssuetoDepartmentReturnID { get; set; }
    public string? IssueToDepartementEntryID { get; set; }
    public string? IssueReturnQty { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
