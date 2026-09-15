using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodBanks : Entity<string>, ITenantOwnedEntity
{

    public BloodBanks(string id) : base(id) { }
    public BloodBanks() : base(Guid.NewGuid().ToString()) { }

    public string? code { get; set; }
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? IsActive { get; set; }
    public string? PersonOnCharge { get; set; }
    public string? StoreID { get; set; }
    public Guid TenantId { get; set; }

}
