using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class ContraDrugs : Entity<string>, ITenantOwnedEntity
{

    public ContraDrugs(string id) : base(id) { }
    public ContraDrugs() : base(Guid.NewGuid().ToString()) { }

    public string? DrugID { get; set; }
    public string? ContraDrugID { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
