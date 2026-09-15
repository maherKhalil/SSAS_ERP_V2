using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class RefundDrugsHeader : Entity<string>, ITenantOwnedEntity
{

    public RefundDrugsHeader(string id) : base(id) { }
    public RefundDrugsHeader() : base(Guid.NewGuid().ToString()) { }

    public string? ReceiptNo { get; set; }
    public string? ReturnReceiptNo { get; set; }
    public string? ReturnReceiptDate { get; set; }
    public string? ReceiptDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
