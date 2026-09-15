using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DispenseDrugsDetails : Entity<string>, ITenantOwnedEntity
{

    public DispenseDrugsDetails(string id) : base(id) { }
    public DispenseDrugsDetails() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? IPnumber { get; set; }
    public string? OPnumber { get; set; }
    public string? ReceiptNo { get; set; }
    public string? ReceiptDate { get; set; }
    public string? DrugID { get; set; }
    public string? QTY { get; set; }
    public string? Amount { get; set; }
    public string? ExpiryDate { get; set; }
    public string? BatchNO { get; set; }
    public string? Instructions { get; set; }
    public string? PaymentID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
