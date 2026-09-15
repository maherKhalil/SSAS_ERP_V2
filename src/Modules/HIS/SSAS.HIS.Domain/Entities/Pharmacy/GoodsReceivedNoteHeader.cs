using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class GoodsReceivedNoteHeader : Entity<string>, ITenantOwnedEntity
{

    public GoodsReceivedNoteHeader(string id) : base(id) { }
    public GoodsReceivedNoteHeader() : base(Guid.NewGuid().ToString()) { }

    public string? LPOId { get; set; }
    public string? GRNNO { get; set; }
    public string? GRNDate { get; set; }
    public string? PaymentTermsID { get; set; }
    public string? DeliveryNoteNo { get; set; }
    public string? DeliveryNoteDate { get; set; }
    public string? InvoiceNO { get; set; }
    public string? InvoiceDate { get; set; }
    public string? TotalExpensesAmount { get; set; }
    public string? TotallandedAmount { get; set; }
    public string? TotalGRNValueFC { get; set; }
    public string? TotalGRNValueLC { get; set; }
    public string? GRNPreparedDate { get; set; }
    public string? Remarks { get; set; }
    public string? Status { get; set; }
    public string? ISOpeneingStock { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? TaxValue { get; set; }
    public string? TransType_ID { get; set; }
    public string? Direct { get; set; }
    public string? OpeningBalance { get; set; }
    public string? issueToDepartment { get; set; }
    public string? StockID { get; set; }
    public string? SetEntryCode { get; set; }
    public string? SupplierId { get; set; }
    public string? Holdingtax { get; set; }
    public string? VatValue { get; set; }
    public Guid TenantId { get; set; }

}
