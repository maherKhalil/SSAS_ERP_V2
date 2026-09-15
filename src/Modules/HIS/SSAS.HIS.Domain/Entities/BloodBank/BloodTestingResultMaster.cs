using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodTestingResultMaster : Entity<string>, ITenantOwnedEntity
{

    public BloodTestingResultMaster(string id) : base(id) { }
    public BloodTestingResultMaster() : base(Guid.NewGuid().ToString()) { }

    public string? BagID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? firstResult { get; set; }
    public string? SecondResult { get; set; }
    public Guid TenantId { get; set; }

}
