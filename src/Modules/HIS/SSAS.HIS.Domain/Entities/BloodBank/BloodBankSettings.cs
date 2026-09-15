using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodBankSettings : Entity<string>, ITenantOwnedEntity
{

    public BloodBankSettings(string id) : base(id) { }
    public BloodBankSettings() : base(Guid.NewGuid().ToString()) { }

    public string? Expiry { get; set; }
    public string? DonerPeriodBlock { get; set; }
    public string? DonerPeriodWorning { get; set; }
    public string? DPrice { get; set; }
    public string? DonationAccount { get; set; }
    public string? DonationPaidAccount { get; set; }
    public string? CompanyID { get; set; }
    public string? Minbloodbags { get; set; }
    public Guid TenantId { get; set; }

}
