using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PharmacySupplierInvoicePayment : Entity<string>, ITenantOwnedEntity
{

    public PharmacySupplierInvoicePayment(string id) : base(id) { }
    public PharmacySupplierInvoicePayment() : base(Guid.NewGuid().ToString()) { }

    public string? GRNHeaderID { get; set; }
    public string? SupplierID { get; set; }
    public string? CurrencyID { get; set; }
    public string? ForiegnValue { get; set; }
    public string? ConversionValue { get; set; }
    public string? LocalValue { get; set; }
    public string? CashHeaderID { get; set; }
    public string? PharmacyHeaderID { get; set; }
    public string? PaymentDate { get; set; }
    public string? PaymentType { get; set; }
    public string? CompanyID { get; set; }
    public string? EntryCode { get; set; }
    public Guid TenantId { get; set; }

}
