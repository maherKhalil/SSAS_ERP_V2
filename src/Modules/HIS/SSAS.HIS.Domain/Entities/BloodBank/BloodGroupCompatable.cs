using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodGroupCompatable : Entity<string>, ITenantOwnedEntity
{

    public BloodGroupCompatable(string id) : base(id) { }
    public BloodGroupCompatable() : base(Guid.NewGuid().ToString()) { }

    public string? BloodGroupID { get; set; }
    public string? BloodGroupCompatableID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? given { get; set; }
    public string? Taken { get; set; }
    public Guid TenantId { get; set; }

}
