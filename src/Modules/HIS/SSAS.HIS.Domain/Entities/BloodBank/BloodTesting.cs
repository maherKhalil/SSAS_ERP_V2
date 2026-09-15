using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class BloodTesting : Entity<string>, ITenantOwnedEntity
{

    public BloodTesting(string id) : base(id) { }
    public BloodTesting() : base(Guid.NewGuid().ToString()) { }

    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? Description { get; set; }
    public string? Code { get; set; }
    public string? TestType { get; set; }
    public string? LabSection { get; set; }
    public string? ServiceId { get; set; }
    public Guid TenantId { get; set; }

}
