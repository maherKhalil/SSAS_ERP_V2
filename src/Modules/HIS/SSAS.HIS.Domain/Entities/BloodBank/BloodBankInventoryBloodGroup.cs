using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodBankInventoryBloodGroup : Entity<string>, ITenantOwnedEntity
{

    public BloodBankInventoryBloodGroup(string id) : base(id) { }
    public BloodBankInventoryBloodGroup() : base(Guid.NewGuid().ToString()) { }

    public string? BloodBankInventoryId { get; set; }
    public string? BloodGroupId { get; set; }
    public Guid TenantId { get; set; }

}
