using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class DischargeType : Entity<string>, ITenantOwnedEntity
{

    public DischargeType(string id) : base(id) { }
    public DischargeType() : base(Guid.NewGuid().ToString()) { }

    public string? DischargeTypeDescArabic { get; set; }
    public string? DischargeTypeDescEnglish { get; set; }
    public string? Status { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
