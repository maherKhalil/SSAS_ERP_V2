using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class LPOApprovingAuthority : Entity<string>, ITenantOwnedEntity
{

    public LPOApprovingAuthority(string id) : base(id) { }
    public LPOApprovingAuthority() : base(Guid.NewGuid().ToString()) { }

    public string? UserID { get; set; }
    public string? LevelID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
