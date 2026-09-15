using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class EmergencyOrganization : Entity<string>, ITenantOwnedEntity
{

    public EmergencyOrganization(string id) : base(id) { }
    public EmergencyOrganization() : base(Guid.NewGuid().ToString()) { }

    public string? DescriptionArabic { get; set; }
    public string? DescriptionEnglish { get; set; }
    public string? MainOrganization { get; set; }
    public string? Status { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
