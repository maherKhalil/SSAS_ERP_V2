using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugClassDet : Entity<string>, ITenantOwnedEntity
{

    public DrugClassDet(string id) : base(id) { }
    public DrugClassDet() : base(Guid.NewGuid().ToString()) { }

    public string? DrugClassID { get; set; }
    public string? DrugID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public Guid TenantId { get; set; }

}
