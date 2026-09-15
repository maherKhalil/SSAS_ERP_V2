using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class StockTransfer : Entity<string>, ITenantOwnedEntity
{

    public StockTransfer(string id) : base(id) { }
    public StockTransfer() : base(Guid.NewGuid().ToString()) { }

    public string? RequestedStoreID { get; set; }
    public string? RequestedDate { get; set; }
    public string? IssuingStoreID { get; set; }
    public string? RefNo { get; set; }
    public string? Status { get; set; }
    public string? RequestNo { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
