using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DestroyingExpiryItemsHeader : Entity<string>, ITenantOwnedEntity
{

    public DestroyingExpiryItemsHeader(string id) : base(id) { }
    public DestroyingExpiryItemsHeader() : base(Guid.NewGuid().ToString()) { }

    public string? SubStoreID { get; set; }
    public string? DestroyNO { get; set; }
    public string? Amount { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
