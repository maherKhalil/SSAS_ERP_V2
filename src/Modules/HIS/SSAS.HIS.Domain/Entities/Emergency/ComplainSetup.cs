using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class ComplainSetup : Entity<string>, ITenantOwnedEntity
{

    public ComplainSetup(string id) : base(id) { }
    public ComplainSetup() : base(Guid.NewGuid().ToString()) { }

    public string? ComplainDescArabic { get; set; }
    public string? ComplainDescEnglish { get; set; }
    public string? Status { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
