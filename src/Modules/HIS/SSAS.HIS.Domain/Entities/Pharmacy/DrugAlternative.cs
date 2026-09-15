using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugAlternative : Entity<string>, ITenantOwnedEntity
{

    public DrugAlternative(string id) : base(id) { }
    public DrugAlternative() : base(Guid.NewGuid().ToString()) { }

    public string? MainDrug { get; set; }
    public string? ReplacedDrug { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
