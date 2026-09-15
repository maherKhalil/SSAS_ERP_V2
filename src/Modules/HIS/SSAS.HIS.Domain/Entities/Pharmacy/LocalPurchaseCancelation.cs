using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class LocalPurchaseCancelation : Entity<string>, ITenantOwnedEntity
{

    public LocalPurchaseCancelation(string id) : base(id) { }
    public LocalPurchaseCancelation() : base(Guid.NewGuid().ToString()) { }

    public string? CancelDate { get; set; }
    public string? CancelNumber { get; set; }
    public string? Narration { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
