using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class TransfuionProcessing : Entity<string>, ITenantOwnedEntity
{

    public TransfuionProcessing(string id) : base(id) { }
    public TransfuionProcessing() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? Reaction { get; set; }
    public string? TransferRequestID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? OrderStatues { get; set; }
    public string? InventoryID { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
