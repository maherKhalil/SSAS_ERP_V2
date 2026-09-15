using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PurchaseReturnDetails : Entity<string>, ITenantOwnedEntity
{

    public PurchaseReturnDetails(string id) : base(id) { }
    public PurchaseReturnDetails() : base(Guid.NewGuid().ToString()) { }

    public string? PurchaseReturnHeaderId { get; set; }
    public string? SubStoreBatchesId { get; set; }
    public string? ReturnQTY { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? discount { get; set; }
    public string? Price { get; set; }
    public Guid TenantId { get; set; }

}
