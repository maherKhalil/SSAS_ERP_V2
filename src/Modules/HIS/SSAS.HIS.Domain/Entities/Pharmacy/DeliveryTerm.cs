using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DeliveryTerm : Entity<string>, ITenantOwnedEntity
{

    public DeliveryTerm(string id) : base(id) { }
    public DeliveryTerm() : base(Guid.NewGuid().ToString()) { }

    public string? DeliveryTermName { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
