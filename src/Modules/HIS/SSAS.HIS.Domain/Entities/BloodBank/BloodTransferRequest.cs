using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodTransferRequest : Entity<string>, ITenantOwnedEntity
{

    public BloodTransferRequest(string id) : base(id) { }
    public BloodTransferRequest() : base(Guid.NewGuid().ToString()) { }

    public string? TransferRequestCode { get; set; }
    public string? RequestDate { get; set; }
    public string? PatientID { get; set; }
    public string? BloodProuductID { get; set; }
    public string? Quantity { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
