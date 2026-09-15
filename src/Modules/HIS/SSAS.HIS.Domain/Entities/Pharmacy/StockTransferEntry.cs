using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class StockTransferEntry : Entity<string>, ITenantOwnedEntity
{

    public StockTransferEntry(string id) : base(id) { }
    public StockTransferEntry() : base(Guid.NewGuid().ToString()) { }

    public string? StockTransferID { get; set; }
    public string? DrugID { get; set; }
    public string? IssuedQTY { get; set; }
    public string? DespatchQTY { get; set; }
    public string? OrderedQTY { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
