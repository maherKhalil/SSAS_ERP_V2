using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class ShiftType : Entity<string>, ITenantOwnedEntity
{

    public ShiftType(string id) : base(id) { }
    public ShiftType() : base(Guid.NewGuid().ToString()) { }

    public string? NameArabic { get; set; }
    public string? NameEnglish { get; set; }
    public string? Status { get; set; }
    public string? TimeFrom { get; set; }
    public string? TimeTo { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
