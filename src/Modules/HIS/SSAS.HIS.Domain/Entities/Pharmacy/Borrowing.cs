using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Borrowing : Entity<string>, ITenantOwnedEntity
{

    public Borrowing(string id) : base(id) { }
    public Borrowing() : base(Guid.NewGuid().ToString()) { }

    public string? NameEN { get; set; }
    public string? NameAR { get; set; }
    public string? Code { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? BranchId { get; set; }
    public Guid TenantId { get; set; }

}
