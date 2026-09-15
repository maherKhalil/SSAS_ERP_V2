using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class IssuetoInventoryEntry : Entity<string>, ITenantOwnedEntity
{

    public IssuetoInventoryEntry(string id) : base(id) { }
    public IssuetoInventoryEntry() : base(Guid.NewGuid().ToString()) { }

    public string? IssuetoInventorytId { get; set; }
    public string? DBloodBagsId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
