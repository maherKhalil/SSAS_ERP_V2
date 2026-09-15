using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugTemplate : Entity<string>, ITenantOwnedEntity
{

    public DrugTemplate(string id) : base(id) { }
    public DrugTemplate() : base(Guid.NewGuid().ToString()) { }

    public string? DrugId { get; set; }
    public string? GenericId { get; set; }
    public string? Dosage { get; set; }
    public string? DosageBaseUnitId { get; set; }
    public string? DosageQnt { get; set; }
    public string? RouteId { get; set; }
    public string? FrequId { get; set; }
    public string? Duration { get; set; }
    public string? DurationTypeId { get; set; }
    public string? AdminSiteId { get; set; }
    public string? DrugStengthId { get; set; }
    public string? DrugFormId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
