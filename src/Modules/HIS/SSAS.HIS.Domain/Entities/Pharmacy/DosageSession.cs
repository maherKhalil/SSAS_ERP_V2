using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DosageSession : Entity<string>, ITenantOwnedEntity
{

    public DosageSession(string id) : base(id) { }
    public DosageSession() : base(Guid.NewGuid().ToString()) { }

    public string? DosageSessionName { get; set; }
    public string? DosageSessionTime { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
