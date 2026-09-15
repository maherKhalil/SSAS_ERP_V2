using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugClass : Entity<string>, ITenantOwnedEntity
{

    public DrugClass(string id) : base(id) { }
    public DrugClass() : base(Guid.NewGuid().ToString()) { }

    public string? ClassName { get; set; }
    public string? ClassNameAr { get; set; }
    public string? Priority { get; set; }
    public string? CompanyID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? Serial { get; set; }
    public Guid TenantId { get; set; }

}
