using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class StdDosage : Entity<string>, ITenantOwnedEntity
{

    public StdDosage(string id) : base(id) { }
    public StdDosage() : base(Guid.NewGuid().ToString()) { }

    public string? DrugID { get; set; }
    public string? AgeFrom { get; set; }
    public string? AgeTo { get; set; }
    public string? Dosage { get; set; }
    public string? FrequencyID { get; set; }
    public string? Period { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
