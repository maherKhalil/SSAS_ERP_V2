using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugTypes : Entity<string>, ITenantOwnedEntity
{

    public DrugTypes(string id) : base(id) { }
    public DrugTypes() : base(Guid.NewGuid().ToString()) { }

    public string? TypeNameAr { get; set; }
    public string? TypeName { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
