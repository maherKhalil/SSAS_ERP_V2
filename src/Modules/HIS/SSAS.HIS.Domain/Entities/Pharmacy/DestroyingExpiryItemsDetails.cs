using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DestroyingExpiryItemsDetails : Entity<string>, ITenantOwnedEntity
{

    public DestroyingExpiryItemsDetails(string id) : base(id) { }
    public DestroyingExpiryItemsDetails() : base(Guid.NewGuid().ToString()) { }

    public string? DestroyingExpiryItemsHeaderId { get; set; }
    public string? ExpiryDate { get; set; }
    public string? DrugID { get; set; }
    public string? SubStoreBatchId { get; set; }
    public string? DestroyQTY { get; set; }
    public string? UnitPrice { get; set; }
    public string? Amount { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? SetEntryCode { get; set; }
    public Guid TenantId { get; set; }

}
