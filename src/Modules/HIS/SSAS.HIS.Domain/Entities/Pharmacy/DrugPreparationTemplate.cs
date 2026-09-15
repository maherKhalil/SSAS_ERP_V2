using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugPreparationTemplate : Entity<string>, ITenantOwnedEntity
{

    public DrugPreparationTemplate(string id) : base(id) { }
    public DrugPreparationTemplate() : base(Guid.NewGuid().ToString()) { }

    public string? PreparationDrugID { get; set; }
    public string? SubDrugID { get; set; }
    public string? QTY { get; set; }
    public string? UnitID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
