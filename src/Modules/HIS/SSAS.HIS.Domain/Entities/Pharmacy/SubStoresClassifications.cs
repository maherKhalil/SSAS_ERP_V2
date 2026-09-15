using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class SubStoresClassifications : Entity<string>, ITenantOwnedEntity
{

    public SubStoresClassifications(string id) : base(id) { }
    public SubStoresClassifications() : base(Guid.NewGuid().ToString()) { }

    public string? ClassificationID { get; set; }
    public string? StoreID { get; set; }
    public Guid TenantId { get; set; }

}
