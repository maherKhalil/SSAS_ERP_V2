using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class GRNDetailsBatchesTransactions : Entity<string>, ITenantOwnedEntity
{

    public GRNDetailsBatchesTransactions(string id) : base(id) { }
    public GRNDetailsBatchesTransactions() : base(Guid.NewGuid().ToString()) { }

    public string? GRNDetailsID { get; set; }
    public string? SubStoreID { get; set; }
    public string? ReceivedQty { get; set; }
    public string? AcceptedQty { get; set; }
    public string? ExcessQty { get; set; }
    public string? Bonus { get; set; }
    public string? AvailableQty { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? IssueBatchID { get; set; }
    public string? BatchId { get; set; }
    public Guid TenantId { get; set; }

}
