using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class LocalPurchaseOrderHeader : Entity<string>, ITenantOwnedEntity
{

    public LocalPurchaseOrderHeader(string id) : base(id) { }
    public LocalPurchaseOrderHeader() : base(Guid.NewGuid().ToString()) { }

    public string? SubStoreID { get; set; }
    public string? SupplierID { get; set; }
    public string? PODate { get; set; }
    public string? CurrencyID { get; set; }
    public string? PONumber { get; set; }
    public string? ShippingModeID { get; set; }
    public string? ShippingTermsID { get; set; }
    public string? DeliveryTime { get; set; }
    public string? DeliveryTimeType { get; set; }
    public string? PaymentModeID { get; set; }
    public string? PaymentTermsID { get; set; }
    public string? Status { get; set; }
    public string? TotalExpenses { get; set; }
    public string? TotalAmount { get; set; }
    public string? NetAmount { get; set; }
    public string? DiscountAmount { get; set; }
    public string? DiscountPrecint { get; set; }
    public string? Remarks { get; set; }
    public string? IsEmergency { get; set; }
    public string? LocalPurchaseCancelationId { get; set; }
    public string? UnifiedPurchCommission { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? TaxValue { get; set; }
    public string? Direct { get; set; }
    public string? openingBalance { get; set; }
    public string? issueToDepartment { get; set; }
    public string? PurchRequestID { get; set; }
    public Guid TenantId { get; set; }

}
