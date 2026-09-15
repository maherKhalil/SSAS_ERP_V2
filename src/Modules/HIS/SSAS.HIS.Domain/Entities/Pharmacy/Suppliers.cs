using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Suppliers : Entity<string>, ITenantOwnedEntity
{

    public Suppliers(string id) : base(id) { }
    public Suppliers() : base(Guid.NewGuid().ToString()) { }

    public string? SupplierCode { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierTypeID { get; set; }
    public string? CurrencyID { get; set; }
    public string? Status { get; set; }
    public string? ShippingModeID { get; set; }
    public string? DeliveryTermID { get; set; }
    public string? DeliveryTime { get; set; }
    public string? DeliveryTimeType { get; set; }
    public string? CreditLimit { get; set; }
    public string? CreditPeriod { get; set; }
    public string? CreditPeriodType { get; set; }
    public string? PaymentTime { get; set; }
    public string? PaymentTimeType { get; set; }
    public string? PaymentModeID { get; set; }
    public string? PaymentGroupID { get; set; }
    public string? PaymentTermsID { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? POBox { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? ZipCode { get; set; }
    public string? Email { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? MainAcc { get; set; }
    public string? SubAcc { get; set; }
    public string? CompanyID { get; set; }
    public string? SupplierNameAr { get; set; }
    public string? Parent_Id { get; set; }
    public string? AgentID { get; set; }
    public string? StopPayment { get; set; }
    public string? HospitalNumForSupplier { get; set; }
    public string? International_Local { get; set; }
    public string? Matchingmethod { get; set; }
    public string? allowtoacceptalternativitem { get; set; }
    public string? Stoppingsupplierpayments { get; set; }
    public string? Reason { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Differencepaymentdate { get; set; }
    public string? Prioritypayment { get; set; }
    public string? Aretaxesdeducted { get; set; }
    public string? AcceptedRounding { get; set; }
    public string? IsItPossibleToRequestQuote { get; set; }
    public string? EmployeeName { get; set; }
    public string? PhonNumber { get; set; }
    public string? MobileNumber { get; set; }
    public string? EmployeeAddress { get; set; }
    public string? EmployeeEmail { get; set; }
    public string? MainAccountToSupplier { get; set; }
    public string? CalcSuppliertax { get; set; }
    public string? IsDiscountNoticeGiven { get; set; }
    public string? AcceptGoodsWithoutPurchaseOrder { get; set; }
    public string? StopPurchaseOrders { get; set; }
    public string? DetermineTypeOfReceipt { get; set; }
    public string? IsSovereignSide { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public string? WithHoldingTax { get; set; }
    public Guid TenantId { get; set; }

}
