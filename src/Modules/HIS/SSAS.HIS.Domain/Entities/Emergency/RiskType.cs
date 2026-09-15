using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class RiskType : Entity<string>, ITenantOwnedEntity
{

    public RiskType(string id) : base(id) { }
    public RiskType() : base(Guid.NewGuid().ToString()) { }

    public string? DescriptionArabic { get; set; }
    public string? DescriptionEnglish { get; set; }
    public string? Status { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
