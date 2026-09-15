using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodBankLPODetail : Entity<string>, ITenantOwnedEntity
{

    public BloodBankLPODetail(string id) : base(id) { }
    public BloodBankLPODetail() : base(Guid.NewGuid().ToString()) { }

    public string? LPOHeaderId { get; set; }
    public string? BloodGroupId { get; set; }
    public string? OrderedQTY { get; set; }
    public string? BonusQTY { get; set; }
    public string? AcceptQTY { get; set; }
    public string? PrevAcceptQTY { get; set; }
    public string? Amount { get; set; }
    public string? Price { get; set; }
    public string? CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? CompanyId { get; set; }
    public Guid TenantId { get; set; }

}
