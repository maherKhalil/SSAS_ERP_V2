using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PaymentSupplier : Entity<string>, ITenantOwnedEntity
{

    public PaymentSupplier(string id) : base(id) { }
    public PaymentSupplier() : base(Guid.NewGuid().ToString()) { }

    public string? SupplierID { get; set; }
    public string? CurrencyID { get; set; }
    public string? ForiegnValue { get; set; }
    public string? ConversionValue { get; set; }
    public string? LocalValue { get; set; }
    public string? Description { get; set; }
    public string? Sub_SupplierAccount { get; set; }
    public string? Main_SupplierAccount { get; set; }
    public string? Sub_BoxAccount { get; set; }
    public string? Main_BoxAccount { get; set; }
    public string? PaymentDate { get; set; }
    public string? PaymentType { get; set; }
    public string? Checkno { get; set; }
    public string? EntryCodes { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
