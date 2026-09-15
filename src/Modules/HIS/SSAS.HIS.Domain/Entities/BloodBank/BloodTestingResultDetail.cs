using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodTestingResultDetail : Entity<string>, ITenantOwnedEntity
{

    public BloodTestingResultDetail(string id) : base(id) { }
    public BloodTestingResultDetail() : base(Guid.NewGuid().ToString()) { }

    public string? BagID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? TestID { get; set; }
    public string? Result { get; set; }
    public Guid TenantId { get; set; }

}
