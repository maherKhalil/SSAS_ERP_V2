using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DosageFrequencyLink : Entity<string>, ITenantOwnedEntity
{

    public DosageFrequencyLink(string id) : base(id) { }
    public DosageFrequencyLink() : base(Guid.NewGuid().ToString()) { }

    public string? DosageId { get; set; }
    public string? FrequencyId { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
