using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class LegalStatusMaster : Entity<string>, ITenantOwnedEntity
{

    public LegalStatusMaster(string id) : base(id) { }
    public LegalStatusMaster() : base(Guid.NewGuid().ToString()) { }

    public string? Code { get; set; }
    public string? NameArabic { get; set; }
    public string? NameEnglish { get; set; }
    public string? Status { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
