using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Drugs : Entity<string>, ITenantOwnedEntity
{

    public Drugs(string id) : base(id) { }
    public Drugs() : base(Guid.NewGuid().ToString()) { }

    public string? DrugCode { get; set; }
    public string? DrugName { get; set; }
    public string? BrandID { get; set; }
    public string? DrugFormID { get; set; }
    public string? DrugTypeID { get; set; }
    public string? BaseUnitID { get; set; }
    public string? DosageUnitID { get; set; }
    public string? DrugStrength { get; set; }
    public string? StorageConditions { get; set; }
    public string? Instractions { get; set; }
    public string? Notes { get; set; }
    public string? ClassificationId { get; set; }
    public string? OPSellingPrice { get; set; }
    public string? IPSellingPrice { get; set; }
    public string? Status { get; set; }
    public string? IsCombination { get; set; }
    public string? AdminModeId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? UnitTemplateID { get; set; }
    public string? BarCode { get; set; }
    public string? Batch { get; set; }
    public string? CostPrice { get; set; }
    public string? LocalBarCode { get; set; }
    public string? ServiceId { get; set; }
    public string? SFDA { get; set; }
    public string? IsProhibited { get; set; }
    public string? manufacturer_ID { get; set; }
    public string? GenericName_ID { get; set; }
    public string? DrugNameAr { get; set; }
    public string? HighRisk { get; set; }
    public string? NphiesCode { get; set; }
    public string? Istaxable { get; set; }
    public string? BrandType { get; set; }
    public string? AllergyTestNeeded { get; set; }
    public Guid TenantId { get; set; }

}
