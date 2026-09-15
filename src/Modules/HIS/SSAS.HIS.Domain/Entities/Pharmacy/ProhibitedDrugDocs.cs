using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class ProhibitedDrugDocs : Entity<string>, ITenantOwnedEntity
{

    public ProhibitedDrugDocs(string id) : base(id) { }
    public ProhibitedDrugDocs() : base(Guid.NewGuid().ToString()) { }

    public string? InvoiceID { get; set; }
    public string? ImageName { get; set; }
    public Guid TenantId { get; set; }

}
