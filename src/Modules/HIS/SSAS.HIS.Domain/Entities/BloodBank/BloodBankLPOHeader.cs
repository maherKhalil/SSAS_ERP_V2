using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodBankLPOHeader : Entity<string>, ITenantOwnedEntity
{

    public BloodBankLPOHeader(string id) : base(id) { }
    public BloodBankLPOHeader() : base(Guid.NewGuid().ToString()) { }

    public string? InventoryId { get; set; }
    public string? SupplierId { get; set; }
    public string? LPOCode { get; set; }
    public string? PurchaseOrderNo { get; set; }
    public string? LPODate { get; set; }
    public string? Status { get; set; }
    public string? CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? CompanyId { get; set; }
    public string? IsEmergency { get; set; }
    public string? Remarks { get; set; }
    public Guid TenantId { get; set; }

}
