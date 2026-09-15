using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DispenseDrugsHeader : Entity<string>, ITenantOwnedEntity
{

    public DispenseDrugsHeader(string id) : base(id) { }
    public DispenseDrugsHeader() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? IPnumber { get; set; }
    public string? OPnumber { get; set; }
    public string? PrescriptionNo { get; set; }
    public string? ReceiptNo { get; set; }
    public string? ReceiptDate { get; set; }
    public string? DoctorID { get; set; }
    public string? SponserID { get; set; }
    public string? ConvertToselF { get; set; }
    public string? PrescriptionDate { get; set; }
    public string? PaymentTypeID { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
