using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class RequestSuppliesDetails : Entity<string>, ITenantOwnedEntity
{

    public RequestSuppliesDetails(string id) : base(id) { }
    public RequestSuppliesDetails() : base(Guid.NewGuid().ToString()) { }

    public string? RequestSuppliesHeaderID { get; set; }
    public string? DrugID { get; set; }
    public string? UnitConversionID { get; set; }
    public string? ReturnedQty { get; set; }
    public string? Quantity { get; set; }
    public string? IsIncluded { get; set; }
    public string? SubstoreBatchId { get; set; }
    public string? IsApproved { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? Price { get; set; }
    public string? Amount { get; set; }
    public string? AmountBefore { get; set; }
    public string? AmountAfter { get; set; }
    public string? SponserId { get; set; }
    public string? EntryCode { get; set; }
    public string? Paid { get; set; }
    public string? Discount { get; set; }
    public string? ApprovedId { get; set; }
    public string? Cash { get; set; }
    public string? Visa { get; set; }
    public string? VisaReceipt { get; set; }
    public string? PateintPaidAmount { get; set; }
    public string? SponserPaidAmount { get; set; }
    public string? ISCash { get; set; }
    public string? ServiceID { get; set; }
    public string? PrescriptionIdDetails { get; set; }
    public string? insuranceId { get; set; }
    public string? PatVat { get; set; }
    public string? SpoVat { get; set; }
    public string? patientlimitdtl { get; set; }
    public string? Dosage { get; set; }
    public string? FrequencyID { get; set; }
    public Guid TenantId { get; set; }

}
