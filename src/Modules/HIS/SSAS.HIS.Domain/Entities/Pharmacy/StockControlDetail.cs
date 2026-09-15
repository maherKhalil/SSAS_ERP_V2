using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class StockControlDetail : Entity<string>, ITenantOwnedEntity
{

    public StockControlDetail(string id) : base(id) { }
    public StockControlDetail() : base(Guid.NewGuid().ToString()) { }

    public string? StockControlID { get; set; }
    public string? Qty { get; set; }
    public string? Price { get; set; }
    public string? Balance { get; set; }
    public string? UnitID { get; set; }
    public string? ConvertionFactor { get; set; }
    public string? GRNDetailID { get; set; }
    public string? GRNDetailBalance { get; set; }
    public string? SignValue { get; set; }
    public string? TransactionDate { get; set; }
    public string? TransactionType { get; set; }
    public string? TransactionNotes { get; set; }
    public string? UserID { get; set; }
    public Guid TenantId { get; set; }

}
