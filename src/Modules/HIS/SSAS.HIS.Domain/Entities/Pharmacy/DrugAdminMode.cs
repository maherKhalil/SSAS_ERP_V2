using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugAdminMode : Entity<string>, ITenantOwnedEntity
{

    public DrugAdminMode(string id) : base(id) { }
    public DrugAdminMode() : base(Guid.NewGuid().ToString()) { }

    public string? DrugId { get; set; }
    public string? AdminModeId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
