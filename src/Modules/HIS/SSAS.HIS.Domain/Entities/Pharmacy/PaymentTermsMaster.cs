using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PaymentTermsMaster : Entity<string>, ITenantOwnedEntity
{

    public PaymentTermsMaster(string id) : base(id) { }
    public PaymentTermsMaster() : base(Guid.NewGuid().ToString()) { }

    public string? PaymentTermCode { get; set; }
    public string? PaymentTermDesc { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? NameKa { get; set; }
    public Guid TenantId { get; set; }

}
