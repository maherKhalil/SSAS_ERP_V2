using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class SupplierforDrug : Entity<string>, ITenantOwnedEntity
{

    public SupplierforDrug(string id) : base(id) { }
    public SupplierforDrug() : base(Guid.NewGuid().ToString()) { }

    public string? DrugID { get; set; }
    public string? SupplierID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
