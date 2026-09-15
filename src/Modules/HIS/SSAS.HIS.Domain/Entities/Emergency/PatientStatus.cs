using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class PatientStatus : Entity<string>, ITenantOwnedEntity
{

    public PatientStatus(string id) : base(id) { }
    public PatientStatus() : base(Guid.NewGuid().ToString()) { }

    public string? PatientStatusTypeDescArabic { get; set; }
    public string? PatientStatusTypeDescEnglish { get; set; }
    public string? Status { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
