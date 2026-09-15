using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class ExpenseMaster : Entity<string>, ITenantOwnedEntity
{

    public ExpenseMaster(string id) : base(id) { }
    public ExpenseMaster() : base(Guid.NewGuid().ToString()) { }

    public string? ExpenseName { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? AccountID { get; set; }
    public Guid TenantId { get; set; }

}
