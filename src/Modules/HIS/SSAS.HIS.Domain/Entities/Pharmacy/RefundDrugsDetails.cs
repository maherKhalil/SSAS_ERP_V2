using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class RefundDrugsDetails : Entity<string>, ITenantOwnedEntity
{

    public RefundDrugsDetails(string id) : base(id) { }
    public RefundDrugsDetails() : base(Guid.NewGuid().ToString()) { }

    public string? ReturnReceiptHeaderId { get; set; }
    public string? ReceptDetailsId { get; set; }
    public string? ReturnQTY { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
