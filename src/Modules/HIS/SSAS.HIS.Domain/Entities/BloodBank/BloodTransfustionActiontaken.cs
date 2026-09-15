using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodTransfustionActiontaken : Entity<string>, ITenantOwnedEntity
{

    public BloodTransfustionActiontaken(string id) : base(id) { }
    public BloodTransfustionActiontaken() : base(Guid.NewGuid().ToString()) { }

    public string? BloodTransfusionId { get; set; }
    public string? TimeofAction { get; set; }
    public string? DetailsofAction { get; set; }
    public string? Comments { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public Guid TenantId { get; set; }

}
