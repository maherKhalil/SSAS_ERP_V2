using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class TransferRequestDispense : Entity<string>, ITenantOwnedEntity
{

    public TransferRequestDispense(string id) : base(id) { }
    public TransferRequestDispense() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? BloodProductID { get; set; }
    public string? BloodTypeID { get; set; }
    public string? Quantity { get; set; }
    public string? LocationID { get; set; }
    public string? OrderedBy { get; set; }
    public string? RequestDate { get; set; }
    public string? TransferRequestID { get; set; }
    public string? OrderStatus { get; set; }
    public string? BagNo { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
