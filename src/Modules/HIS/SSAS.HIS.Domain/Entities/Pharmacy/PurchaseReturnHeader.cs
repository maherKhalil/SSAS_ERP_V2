using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PurchaseReturnHeader : Entity<string>, ITenantOwnedEntity
{

    public PurchaseReturnHeader(string id) : base(id) { }
    public PurchaseReturnHeader() : base(Guid.NewGuid().ToString()) { }

    public string? PurchaseReturnNumber { get; set; }
    public string? ReturnDate { get; set; }
    public string? TotalAmount { get; set; }
    public string? Remarks { get; set; }
    public string? SubstoreID { get; set; }
    public string? Status { get; set; }
    public string? GrnNo { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? EntryCode { get; set; }
    public Guid TenantId { get; set; }

}
