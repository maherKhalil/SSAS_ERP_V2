using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class OpeningStockDetails : Entity<string>, ITenantOwnedEntity
{

    public OpeningStockDetails(string id) : base(id) { }
    public OpeningStockDetails() : base(Guid.NewGuid().ToString()) { }

    public string? OpeningStockID { get; set; }
    public string? DrugID { get; set; }
    public string? BatchID { get; set; }
    public string? ExpiryDate { get; set; }
    public string? BatchNo { get; set; }
    public string? PhysicQTY { get; set; }
    public string? StockInHand { get; set; }
    public string? DifferenceQTY { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
