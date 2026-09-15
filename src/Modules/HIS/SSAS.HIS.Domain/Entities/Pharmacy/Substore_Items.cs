using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Substore_Items : Entity<string>, ITenantOwnedEntity
{

    public Substore_Items(string id) : base(id) { }
    public Substore_Items() : base(Guid.NewGuid().ToString()) { }

    public string? SubstoreID { get; set; }
    public string? ItemID { get; set; }
    public string? Quantity { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
