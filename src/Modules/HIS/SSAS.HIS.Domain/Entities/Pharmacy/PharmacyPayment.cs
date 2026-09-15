using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PharmacyPayment : Entity<string>, ITenantOwnedEntity
{

    public PharmacyPayment(string id) : base(id) { }
    public PharmacyPayment() : base(Guid.NewGuid().ToString()) { }

    public string? Date { get; set; }
    public string? SupplierID { get; set; }
    public string? chequeNumber { get; set; }
    public string? Value { get; set; }
    public string? chequeStatus { get; set; }
    public string? DueDate { get; set; }
    public string? MainAccount_Supplier { get; set; }
    public string? SubAccount_Supplier { get; set; }
    public string? MainAccount_Bank { get; set; }
    public string? SubAccount_Bank { get; set; }
    public string? MainAccount_PaymentPaper { get; set; }
    public string? SubAccount_PaymentPaper { get; set; }
    public string? CreationDate { get; set; }
    public string? CreatedBy { get; set; }
    public string? Modificationdate { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ForiegnValue { get; set; }
    public string? ConversionValue { get; set; }
    public string? CurrencyID { get; set; }
    public string? SetEntryCode { get; set; }
    public string? PaymentMode { get; set; }
    public string? SupplierDuesId { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
