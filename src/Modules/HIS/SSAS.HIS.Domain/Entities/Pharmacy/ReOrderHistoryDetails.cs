using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class ReOrderHistoryDetails : Entity<string>, ITenantOwnedEntity
{

    public ReOrderHistoryDetails(string id) : base(id) { }
    public ReOrderHistoryDetails() : base(Guid.NewGuid().ToString()) { }

    public string? DrugID { get; set; }
    public string? MasterID { get; set; }
    public string? StockOnHand { get; set; }
    public string? Shortage { get; set; }
    public string? awaitQty { get; set; }
    public string? ReOrderQTY { get; set; }
    public string? ReorderUnit { get; set; }
    public Guid TenantId { get; set; }

}
