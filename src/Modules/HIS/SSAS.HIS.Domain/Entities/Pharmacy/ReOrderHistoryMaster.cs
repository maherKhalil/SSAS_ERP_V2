using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class ReOrderHistoryMaster : Entity<string>, ITenantOwnedEntity
{

    public ReOrderHistoryMaster(string id) : base(id) { }
    public ReOrderHistoryMaster() : base(Guid.NewGuid().ToString()) { }

    public string? purchaseReqId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? Code { get; set; }
    public string? slowMovement { get; set; }
    public Guid TenantId { get; set; }

}
