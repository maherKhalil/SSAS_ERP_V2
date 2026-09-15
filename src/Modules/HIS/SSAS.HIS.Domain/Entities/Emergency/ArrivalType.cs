using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class ArrivalType : Entity<string>, ITenantOwnedEntity
{

    public ArrivalType(string id) : base(id) { }
    public ArrivalType() : base(Guid.NewGuid().ToString()) { }

    public string? ArrivalTypeDescArabic { get; set; }
    public string? ArrivalTypeDescEnglish { get; set; }
    public string? Status { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
