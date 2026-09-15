using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugForms : Entity<string>, ITenantOwnedEntity
{

    public DrugForms(string id) : base(id) { }
    public DrugForms() : base(Guid.NewGuid().ToString()) { }

    public string? FormName { get; set; }
    public string? FormNameArabic { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
