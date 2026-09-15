using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodGroup : Entity<string>, ITenantOwnedEntity
{

    public BloodGroup(string id) : base(id) { }
    public BloodGroup() : base(Guid.NewGuid().ToString()) { }

    public string? BloodGroupCode { get; set; }
    public string? GroupNameLatin { get; set; }
    public string? GroupNameLocal { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
