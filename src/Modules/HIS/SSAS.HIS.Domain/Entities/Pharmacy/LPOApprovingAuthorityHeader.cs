using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class LPOApprovingAuthorityHeader : Entity<string>, ITenantOwnedEntity
{

    public LPOApprovingAuthorityHeader(string id) : base(id) { }
    public LPOApprovingAuthorityHeader() : base(Guid.NewGuid().ToString()) { }

    public string? LevelName { get; set; }
    public string? LevelAmount { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
