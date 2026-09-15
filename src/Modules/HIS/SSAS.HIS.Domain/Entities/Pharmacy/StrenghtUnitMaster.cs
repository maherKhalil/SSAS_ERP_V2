using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class StrenghtUnitMaster : Entity<string>, ITenantOwnedEntity
{

    public StrenghtUnitMaster(string id) : base(id) { }
    public StrenghtUnitMaster() : base(Guid.NewGuid().ToString()) { }

    public string? Code { get; set; }
    public string? NameArabic { get; set; }
    public string? NameEnglish { get; set; }
    public string? Status { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
